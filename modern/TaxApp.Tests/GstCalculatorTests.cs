using TaxApp.Tests.Support;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Tests;

public class GstCalculatorTests
{
    private readonly GstCalculator _calculator = new(0.10m);

    [Theory]
    [MemberData(nameof(GoldenVectors.GstNames), MemberType = typeof(GoldenVectors))]
    public void Calculate_matches_the_golden_master(string name)
    {
        var vector = GoldenVectors.Gst(name);
        var expected = vector.GetProperty("expected");
        var mode = Enum.Parse<GstMode>(vector.GetProperty("mode").GetString()!);

        var actual = _calculator.Calculate(GoldenVectors.Money(vector.GetProperty("amount")), mode);

        Assert.Equal(GoldenVectors.Money(expected.GetProperty("gstAmount")), actual.GstAmount);
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("amountExclGst")), actual.AmountExclGst);
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("amountInclGst")), actual.AmountInclGst);
    }

    [Fact]
    public void Adding_then_extracting_gst_round_trips_for_a_round_price()
    {
        var added = _calculator.Calculate(100m, GstMode.AddGst);
        var extracted = _calculator.Calculate(added.AmountInclGst, GstMode.ExtractGst);

        Assert.Equal(100m, extracted.AmountExclGst);
        Assert.Equal(added.GstAmount, extracted.GstAmount);
    }

    [Fact]
    public void A_negative_amount_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(-0.01m, GstMode.AddGst));
}
