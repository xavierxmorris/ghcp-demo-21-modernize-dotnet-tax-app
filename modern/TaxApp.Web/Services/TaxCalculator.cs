using Microsoft.Extensions.Options;
using TaxApp.Web.Models;

namespace TaxApp.Web.Services;

public interface ITaxCalculator
{
    TaxEstimate Estimate(string taxYear, decimal income, decimal deductions);
}

public sealed class TaxCalculator : ITaxCalculator
{
    private readonly decimal _medicareLevyRate;

    public TaxCalculator(IOptions<TaxOptions> options)
        : this(options.Value.MedicareLevyRate)
    {
    }

    public TaxCalculator(decimal medicareLevyRate)
    {
        _medicareLevyRate = medicareLevyRate;
    }

    public TaxEstimate Estimate(string taxYear, decimal income, decimal deductions)
    {
        if (!TaxTables.IsSupported(taxYear))
        {
            throw new ArgumentException($"Unsupported tax year: {taxYear}", nameof(taxYear));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(income);
        ArgumentOutOfRangeException.ThrowIfNegative(deductions);

        var taxable = Math.Max(0m, income - deductions);
        var incomeTax = Round(TaxOn(taxYear, taxable));
        var medicare = Round(taxable * _medicareLevyRate);
        var total = incomeTax + medicare;

        return new TaxEstimate(
            taxYear,
            income,
            deductions,
            taxable,
            incomeTax,
            medicare,
            total,
            income - total,
            income > 0m ? Round(total * 100m / income) : 0m);
    }

    private static decimal TaxOn(string taxYear, decimal taxable)
    {
        var brackets = TaxTables.For(taxYear);
        for (var i = brackets.Length - 1; i >= 1; i--)
        {
            if (taxable > brackets[i].LowerBound)
            {
                return brackets[i].BaseTax + (taxable - brackets[i].LowerBound) * brackets[i].Rate;
            }
        }

        return 0m;
    }

    // Explicit rounding rule, unchanged from the legacy app: .005 rounds up (away from zero), unlike the default (to even).
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
