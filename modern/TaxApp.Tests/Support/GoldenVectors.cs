using System.Globalization;
using System.Text.Json;

namespace TaxApp.Tests.Support;

/// <summary>Reads Golden/tax-vectors.json, the golden master shared with the legacy app and the HTTP contract script.</summary>
public static class GoldenVectors
{
    private static readonly Lazy<JsonElement> Document = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Golden", "tax-vectors.json");
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream).RootElement.Clone();
    });

    public static TheoryData<string> EstimateNames => Names("estimate");

    public static TheoryData<string> GstNames => Names("gst");

    public static TheoryData<string> ReturnNames => Names("return");

    public static JsonElement Estimate(string name) => Find("estimate", name);

    public static JsonElement Gst(string name) => Find("gst", name);

    public static JsonElement Return(string name) => Find("return", name);

    public static decimal Money(JsonElement element) => decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture);

    private static TheoryData<string> Names(string section)
    {
        var data = new TheoryData<string>();
        foreach (var vector in Document.Value.GetProperty(section).EnumerateArray())
        {
            data.Add(vector.GetProperty("name").GetString()!);
        }

        return data;
    }

    private static JsonElement Find(string section, string name) =>
        Document.Value.GetProperty(section).EnumerateArray().Single(v => v.GetProperty("name").GetString() == name);
}
