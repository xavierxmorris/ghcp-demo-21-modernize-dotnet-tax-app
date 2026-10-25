using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TaxApp.Web.Models;

public static class TaxLimits
{
    public const decimal MaxAmount = 10_000_000m;
}

public sealed class HomeViewModel
{
    public required string DefaultTaxYear { get; init; }

    public required IEnumerable<string> SupportedTaxYears { get; init; }
}

public sealed record TaxEstimate(
    string TaxYear,
    decimal Income,
    decimal Deductions,
    decimal TaxableIncome,
    decimal IncomeTax,
    decimal MedicareLevy,
    decimal TotalTax,
    decimal NetIncome,
    decimal EffectiveRate);

public sealed class TaxEstimateRequest
{
    [Display(Name = "Tax year")]
    public string? TaxYear { get; set; }

    [Display(Name = "Gross income")]
    [Range(0, 10000000, ErrorMessage = "Gross income must be between $0 and $10,000,000.")]
    public decimal? Income { get; set; }

    [Display(Name = "Deductions")]
    [Range(0, 10000000, ErrorMessage = "Deductions must be between $0 and $10,000,000.")]
    public decimal? Deductions { get; set; }
}

public sealed class TaxEstimateViewModel
{
    public required TaxEstimateRequest Request { get; init; }

    public TaxEstimate? Result { get; set; }

    public required IEnumerable<SelectListItem> TaxYears { get; init; }
}
