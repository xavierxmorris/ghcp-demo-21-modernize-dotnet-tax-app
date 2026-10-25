namespace TaxApp.Web.Services;

/// <summary>One band of the resident individual income tax scale: tax = BaseTax + Rate x (income - LowerBound).</summary>
public sealed record TaxBracket(decimal LowerBound, decimal BaseTax, decimal Rate);

/// <summary>
/// Australian resident individual tax scales published by the ATO (retrieved 2026-10-26).
/// Illustrative only: no offsets, no Medicare levy thresholds, no HELP.
/// </summary>
public static class TaxTables
{
    private static readonly Dictionary<string, TaxBracket[]> Scales = new()
    {
        ["2025-26"] =
        [
            new(0m, 0m, 0m),
            new(18200m, 0m, 0.16m),
            new(45000m, 4288m, 0.30m),
            new(135000m, 31288m, 0.37m),
            new(190000m, 51638m, 0.45m)
        ],
        ["2026-27"] =
        [
            new(0m, 0m, 0m),
            new(18200m, 0m, 0.15m),
            new(45000m, 4020m, 0.30m),
            new(135000m, 31020m, 0.37m),
            new(190000m, 51370m, 0.45m)
        ]
    };

    public static IEnumerable<string> SupportedYears => Scales.Keys.OrderByDescending(year => year).ToList();

    public static bool IsSupported(string? taxYear) => taxYear is not null && Scales.ContainsKey(taxYear);

    public static TaxBracket[] For(string taxYear) => Scales[taxYear];
}
