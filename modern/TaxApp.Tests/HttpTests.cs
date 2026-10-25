using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using TaxApp.Tests.Support;

namespace TaxApp.Tests;

/// <summary>Starts the real app in memory with an isolated deductions file.</summary>
public sealed class TaxAppFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "taxapp-modern-http-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("TaxApp:DeductionsPath", Path.Combine(_directory, "deductions.json"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}

public sealed partial class HttpTests(TaxAppFactory factory) : IClassFixture<TaxAppFactory>
{
    private HttpClient NewClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    [GeneratedRegex("data-testid=\"(?<id>[^\"]+)\"[^>]*data-value=\"(?<value>[^\"]*)\"")]
    private static partial Regex TestIdValue();

    private static Dictionary<string, string> Values(string html) =>
        TestIdValue().Matches(html).ToDictionary(m => m.Groups["id"].Value, m => m.Groups["value"].Value);

    [Fact]
    public async Task Home_page_names_the_app_and_reports_the_modern_runtime()
    {
        var html = await NewClient().GetStringAsync("/");

        Assert.Contains("data-testid=\"app-title\">Tax App<", html);
        Assert.Contains("Running on .NET 10", html);
    }

    [Fact]
    public async Task Estimator_page_shows_the_golden_figures_for_a_vector()
    {
        var vector = GoldenVectors.Estimate("middle income with deductions");

        var html = await NewClient().GetStringAsync("/Tax/Estimate?TaxYear=2026-27&Income=95000&Deductions=5000");
        var values = Values(html);

        var expected = vector.GetProperty("expected");
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("totalTax")), decimal.Parse(values["total-tax"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("taxableIncome")), decimal.Parse(values["taxable-income"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task The_JSON_endpoint_keeps_the_legacy_PascalCase_property_names()
    {
        var response = await NewClient().GetAsync("/Tax/EstimateJson?taxYear=2026-27&income=95000");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"TotalTax\":20920", json);
        Assert.DoesNotContain("\"totalTax\"", json);
    }

    [Fact]
    public async Task Posting_a_deduction_without_an_antiforgery_token_is_rejected()
    {
        var response = await NewClient().PostAsync("/Deductions/Add", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Description", "No token"),
            new KeyValuePair<string, string>("Category", "Other"),
            new KeyValuePair<string, string>("Amount", "1.00")
        ]));

        Assert.True((int)response.StatusCode >= 400);
    }

    [Fact]
    public async Task A_deduction_can_be_added_and_removed_with_a_valid_token()
    {
        var client = NewClient();
        var list = await client.GetStringAsync("/Deductions");
        var token = Regex.Match(list, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var before = Regex.Matches(list, "data-testid=\"deduction-row\"").Count;

        var add = await client.PostAsync("/Deductions/Add", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("Description", "Probe"),
            new KeyValuePair<string, string>("Category", "HomeOffice"),
            new KeyValuePair<string, string>("Amount", "12.34")
        ]));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);

        var afterAdd = await client.GetStringAsync("/Deductions");
        Assert.Equal(before + 1, Regex.Matches(afterAdd, "data-testid=\"deduction-row\"").Count);
        var id = Regex.Matches(afterAdd, "data-testid=\"deduction-row\"[^>]*data-id=\"([^\"]+)\"").Last().Groups[1].Value;

        var remove = await client.PostAsync("/Deductions/Delete", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("id", id)
        ]));
        Assert.Equal(HttpStatusCode.Redirect, remove.StatusCode);

        var afterRemove = await client.GetStringAsync("/Deductions");
        Assert.Equal(before, Regex.Matches(afterRemove, "data-testid=\"deduction-row\"").Count);
    }

    [Fact]
    public async Task An_unknown_url_is_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await NewClient().GetAsync("/does-not-exist")).StatusCode);
}
