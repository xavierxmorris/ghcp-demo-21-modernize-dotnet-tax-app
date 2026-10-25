namespace TaxApp.Web.Services;

/// <summary>Settings from appsettings.json (the legacy app read these from Web.config appSettings).</summary>
public sealed class TaxOptions
{
    public const string SectionName = "TaxApp";

    public string DefaultTaxYear { get; set; } = "2026-27";

    public decimal MedicareLevyRate { get; set; } = 0.02m;

    public decimal GstRate { get; set; } = 0.10m;

    public string DeductionsPath { get; set; } = "App_Data/deductions.json";

    public string DeductionsSeedPath { get; set; } = "App_Data/deductions.seed.json";
}
