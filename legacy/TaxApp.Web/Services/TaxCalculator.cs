using System;
using TaxApp.Web.Models;

namespace TaxApp.Web.Services
{
    public interface ITaxCalculator
    {
        TaxEstimate Estimate(string taxYear, decimal income, decimal deductions);
    }

    public class TaxCalculator : ITaxCalculator
    {
        private readonly decimal _medicareLevyRate;

        public TaxCalculator() : this(AppSettings.MedicareLevyRate)
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
                throw new ArgumentException("Unsupported tax year: " + taxYear, "taxYear");
            }

            if (income < 0m)
            {
                throw new ArgumentOutOfRangeException("income", "Income cannot be negative.");
            }

            if (deductions < 0m)
            {
                throw new ArgumentOutOfRangeException("deductions", "Deductions cannot be negative.");
            }

            var taxable = Math.Max(0m, income - deductions);
            var incomeTax = Round(TaxOn(taxYear, taxable));
            var medicare = Round(taxable * _medicareLevyRate);
            var total = incomeTax + medicare;

            return new TaxEstimate
            {
                TaxYear = taxYear,
                Income = income,
                Deductions = deductions,
                TaxableIncome = taxable,
                IncomeTax = incomeTax,
                MedicareLevy = medicare,
                TotalTax = total,
                NetIncome = income - total,
                EffectiveRate = income > 0m ? Round(total * 100m / income) : 0m
            };
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

        // Explicit rounding rule: .005 rounds up (away from zero), unlike the .NET default which rounds to even.
        private static decimal Round(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
