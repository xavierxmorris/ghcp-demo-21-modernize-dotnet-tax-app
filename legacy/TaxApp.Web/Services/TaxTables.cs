using System.Collections.Generic;
using System.Linq;

namespace TaxApp.Web.Services
{
    /// <summary>One band of the resident individual income tax scale: tax = BaseTax + Rate x (income - LowerBound).</summary>
    public sealed class TaxBracket
    {
        public TaxBracket(decimal lowerBound, decimal baseTax, decimal rate)
        {
            LowerBound = lowerBound;
            BaseTax = baseTax;
            Rate = rate;
        }

        public decimal LowerBound { get; private set; }

        public decimal BaseTax { get; private set; }

        public decimal Rate { get; private set; }
    }

    /// <summary>
    /// Australian resident individual tax scales published by the ATO (retrieved 2026-10-26).
    /// Illustrative only: no offsets, no Medicare levy thresholds, no HELP. The tables are hard-coded on purpose,
    /// which is a very common thing to find in a legacy app.
    /// </summary>
    public static class TaxTables
    {
        private static readonly Dictionary<string, TaxBracket[]> Scales = new Dictionary<string, TaxBracket[]>
        {
            {
                "2025-26",
                new[]
                {
                    new TaxBracket(0m, 0m, 0m),
                    new TaxBracket(18200m, 0m, 0.16m),
                    new TaxBracket(45000m, 4288m, 0.30m),
                    new TaxBracket(135000m, 31288m, 0.37m),
                    new TaxBracket(190000m, 51638m, 0.45m)
                }
            },
            {
                "2026-27",
                new[]
                {
                    new TaxBracket(0m, 0m, 0m),
                    new TaxBracket(18200m, 0m, 0.15m),
                    new TaxBracket(45000m, 4020m, 0.30m),
                    new TaxBracket(135000m, 31020m, 0.37m),
                    new TaxBracket(190000m, 51370m, 0.45m)
                }
            }
        };

        public static IEnumerable<string> SupportedYears
        {
            get { return Scales.Keys.OrderByDescending(year => year).ToList(); }
        }

        public static bool IsSupported(string taxYear)
        {
            return taxYear != null && Scales.ContainsKey(taxYear);
        }

        public static TaxBracket[] For(string taxYear)
        {
            return Scales[taxYear];
        }
    }
}
