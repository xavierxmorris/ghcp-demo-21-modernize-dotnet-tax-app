using TaxApp.Tests.Support;
using TaxApp.Web.Services;

namespace TaxApp.Tests;

public class TaxCalculatorTests
{
    private readonly TaxCalculator _calculator = new(0.02m);

    [Theory]
    [MemberData(nameof(GoldenVectors.EstimateNames), MemberType = typeof(GoldenVectors))]
    public void Estimate_matches_the_golden_master(string name)
    {
        var vector = GoldenVectors.Estimate(name);
        var expected = vector.GetProperty("expected");

        var actual = _calculator.Estimate(
            vector.GetProperty("taxYear").GetString()!,
            GoldenVectors.Money(vector.GetProperty("income")),
            GoldenVectors.Money(vector.GetProperty("deductions")));

        Assert.Equal(GoldenVectors.Money(expected.GetProperty("taxableIncome")), actual.TaxableIncome);
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("incomeTax")), actual.IncomeTax);
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("medicareLevy")), actual.MedicareLevy);
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("totalTax")), actual.TotalTax);
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("netIncome")), actual.NetIncome);
        Assert.Equal(GoldenVectors.Money(expected.GetProperty("effectiveRate")), actual.EffectiveRate);
    }

    [Fact]
    public void Midpoints_round_away_from_zero_not_to_even()
    {
        // 18,200.30 -> tax 0.15 x 0.30 = 0.045. Banker's rounding would give 0.04; the app has always charged 0.05.
        Assert.Equal(0.05m, _calculator.Estimate("2026-27", 18200.30m, 0m).IncomeTax);

        // 12,345.25 x 2% = 246.905. Banker's rounding would give 246.90.
        Assert.Equal(246.91m, _calculator.Estimate("2026-27", 12345.25m, 0m).MedicareLevy);
    }

    [Fact]
    public void The_same_income_is_taxed_differently_in_each_supported_year()
    {
        Assert.Equal(19288m, _calculator.Estimate("2025-26", 95000m, 0m).IncomeTax);
        Assert.Equal(19020m, _calculator.Estimate("2026-27", 95000m, 0m).IncomeTax);
    }

    [Fact]
    public void Deductions_cannot_push_taxable_income_below_zero()
    {
        var result = _calculator.Estimate("2026-27", 1000m, 5000m);

        Assert.Equal(0m, result.TaxableIncome);
        Assert.Equal(0m, result.TotalTax);
        Assert.Equal(1000m, result.NetIncome);
    }

    [Fact]
    public void An_unsupported_tax_year_is_rejected() =>
        Assert.Throws<ArgumentException>(() => _calculator.Estimate("1999-00", 50000m, 0m));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1000, -1)]
    public void Negative_amounts_are_rejected(int income, int deductions) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Estimate("2026-27", income, deductions));

    [Fact]
    public void Supported_years_are_listed_newest_first() =>
        Assert.Equal(["2026-27", "2025-26"], TaxTables.SupportedYears);
}
