using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaxApp.Tests.Support;
using TaxApp.Web.Controllers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Tests;

/// <summary>Controller tests mirroring the legacy characterization tests, so the same behavior is pinned on both sides.</summary>
public class ControllerTests
{
    private static readonly TaxCalculator Calculator = new(0.02m);
    private static readonly IOptions<TaxOptions> Options = Microsoft.Extensions.Options.Options.Create(new TaxOptions());

    private static TaxController NewTaxController() => new(Calculator, Options, NullLogger<TaxController>.Instance);

    [Fact]
    public void Estimate_shows_a_blank_form_on_first_visit()
    {
        var controller = NewTaxController();

        var view = Assert.IsType<ViewResult>(controller.Estimate(new TaxEstimateRequest()));

        var model = Assert.IsType<TaxEstimateViewModel>(view.Model);
        Assert.Null(model.Result);
        Assert.True(controller.ModelState.IsValid);
        Assert.Equal(["2026-27", "2025-26"], model.TaxYears.Select(i => i.Value));
        Assert.Equal("2026-27", model.Request.TaxYear);
    }

    [Fact]
    public void Estimate_calculates_when_income_is_supplied()
    {
        var controller = NewTaxController();

        var view = Assert.IsType<ViewResult>(controller.Estimate(
            new TaxEstimateRequest { TaxYear = "2026-27", Income = 95000m, Deductions = 5000m }));

        var model = Assert.IsType<TaxEstimateViewModel>(view.Model);
        Assert.Equal(19320m, model.Result!.TotalTax);
        Assert.Equal(90000m, model.Result.TaxableIncome);
    }

    [Fact]
    public void Estimate_reports_an_unsupported_tax_year_without_calculating()
    {
        var controller = NewTaxController();

        var view = Assert.IsType<ViewResult>(controller.Estimate(
            new TaxEstimateRequest { TaxYear = "1999-00", Income = 50000m }));

        Assert.Null(((TaxEstimateViewModel)view.Model!).Result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("TaxYear"));
    }

    [Fact]
    public void Estimate_does_not_calculate_when_model_binding_failed()
    {
        var controller = NewTaxController();
        controller.ModelState.AddModelError("Income", "The value 'abc' is not valid for Income.");

        var view = Assert.IsType<ViewResult>(controller.Estimate(new TaxEstimateRequest()));

        Assert.Null(((TaxEstimateViewModel)view.Model!).Result);
    }

    [Fact]
    public void Estimate_asks_for_income_when_only_deductions_are_given()
    {
        var controller = NewTaxController();

        var view = Assert.IsType<ViewResult>(controller.Estimate(new TaxEstimateRequest { Deductions = 100m }));

        Assert.Null(((TaxEstimateViewModel)view.Model!).Result);
        Assert.True(controller.ModelState.ContainsKey("Income"));
    }

    [Fact]
    public void EstimateJson_returns_the_estimate_for_a_GET()
    {
        var controller = NewTaxController();

        var json = Assert.IsType<JsonResult>(controller.EstimateJson("2026-27", 95000m, null));

        var estimate = Assert.IsType<TaxEstimate>(json.Value);
        Assert.Equal(20920m, estimate.TotalTax);
    }

    [Theory]
    [InlineData("2026-27", null, null)]
    [InlineData("2026-27", -1, null)]
    [InlineData("2026-27", 100, -5)]
    [InlineData("1999-00", 100, null)]
    [InlineData("2026-27", 10000001, null)]
    public void EstimateJson_rejects_bad_input_with_400(string taxYear, int? income, int? deductions)
    {
        var controller = NewTaxController();

        Assert.IsType<BadRequestResult>(controller.EstimateJson(taxYear, income, deductions));
    }

    [Fact]
    public void Gst_shows_a_blank_form_then_a_result()
    {
        var controller = new GstController(new GstCalculator(0.10m));

        var blank = Assert.IsType<ViewResult>(controller.Index(new GstRequest()));
        Assert.Null(((GstViewModel)blank.Model!).Result);

        var calculated = Assert.IsType<ViewResult>(controller.Index(new GstRequest { Amount = 110m, Mode = GstMode.ExtractGst }));
        Assert.Equal(10m, ((GstViewModel)calculated.Model!).Result!.GstAmount);
    }

    [Theory]
    [MemberData(nameof(GoldenVectors.ReturnNames), MemberType = typeof(GoldenVectors))]
    public void Return_summary_matches_the_golden_master(string name)
    {
        var vector = GoldenVectors.Return(name);
        var controller = new ReturnController(
            Calculator,
            new InMemoryDeductionRepository(GoldenVectors.Money(vector.GetProperty("deductionsTotal"))),
            Options);

        var view = Assert.IsType<ViewResult>(controller.Index(new ReturnRequest
        {
            TaxYear = vector.GetProperty("taxYear").GetString(),
            Income = GoldenVectors.Money(vector.GetProperty("income")),
            Withheld = GoldenVectors.Money(vector.GetProperty("withheld"))
        }));

        var expected = vector.GetProperty("expected");
        var summary = ((ReturnViewModel)view.Model!).Summary!;
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("totalTax")), summary.Estimate.TotalTax);
        Assert.Equal(expected.GetProperty("isRefund").GetBoolean(), summary.IsRefund);
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("resultAmount")), summary.ResultAmount);
    }

    [Fact]
    public void Deductions_index_lists_items_oldest_first_with_a_total()
    {
        var repository = new InMemoryDeductionRepository();
        repository.Add("First", DeductionCategory.Other, 10m);
        repository.Add("Second", DeductionCategory.HomeOffice, 15.5m);
        var controller = new DeductionsController(repository, NullLogger<DeductionsController>.Instance);

        var view = Assert.IsType<ViewResult>(controller.Index());

        var model = Assert.IsType<DeductionsViewModel>(view.Model);
        Assert.Equal(["First", "Second"], model.Items.Select(i => i.Description));
        Assert.Equal(25.5m, model.Total);
    }

    [Fact]
    public void Adding_a_valid_deduction_redirects_back_to_the_list()
    {
        var repository = new InMemoryDeductionRepository();
        var controller = new DeductionsController(repository, NullLogger<DeductionsController>.Instance);

        var result = Assert.IsType<RedirectToActionResult>(controller.Add(
            new DeductionInput { Description = "Desk", Category = DeductionCategory.HomeOffice, Amount = 250m }));

        Assert.Equal("Index", result.ActionName);
        Assert.Equal(250m, repository.Total());
    }

    [Fact]
    public void Adding_an_invalid_deduction_redisplays_the_form_and_saves_nothing()
    {
        var repository = new InMemoryDeductionRepository();
        var controller = new DeductionsController(repository, NullLogger<DeductionsController>.Instance);
        controller.ModelState.AddModelError("Description", "Enter a description.");

        var view = Assert.IsType<ViewResult>(controller.Add(new DeductionInput { Amount = 5m }));

        Assert.Equal("Index", view.ViewName);
        Assert.Equal(0m, repository.Total());
    }

    [Fact]
    public void Deleting_a_deduction_removes_it_and_redirects()
    {
        var repository = new InMemoryDeductionRepository();
        var item = repository.Add("Desk", DeductionCategory.HomeOffice, 250m);
        var controller = new DeductionsController(repository, NullLogger<DeductionsController>.Instance);

        var result = Assert.IsType<RedirectToActionResult>(controller.Delete(item.Id));

        Assert.Equal("Index", result.ActionName);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void Deleting_an_unknown_deduction_is_harmless()
    {
        var controller = new DeductionsController(new InMemoryDeductionRepository(), NullLogger<DeductionsController>.Instance);

        Assert.IsType<RedirectToActionResult>(controller.Delete(Guid.NewGuid()));
    }

    [Fact]
    public void Home_lists_the_supported_tax_years()
    {
        var view = Assert.IsType<ViewResult>(new HomeController(Options).Index());

        var model = Assert.IsType<HomeViewModel>(view.Model);
        Assert.Equal("2026-27", model.DefaultTaxYear);
        Assert.Equal(["2026-27", "2025-26"], model.SupportedTaxYears);
    }
}
