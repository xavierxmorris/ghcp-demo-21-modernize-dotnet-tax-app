using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TaxApp.Web.Models;

public sealed class ReturnRequest
{
    [Display(Name = "Tax year")]
    public string? TaxYear { get; set; }

    [Display(Name = "Gross income")]
    [Range(0, 10000000, ErrorMessage = "Gross income must be between $0 and $10,000,000.")]
    public decimal? Income { get; set; }

    [Display(Name = "Tax withheld")]
    [Range(0, 10000000, ErrorMessage = "Tax withheld must be between $0 and $10,000,000.")]
    public decimal? Withheld { get; set; }
}

public sealed record ReturnSummary(TaxEstimate Estimate, decimal Withheld, bool IsRefund, decimal ResultAmount)
{
    public static ReturnSummary From(TaxEstimate estimate, decimal withheld)
    {
        var balance = withheld - estimate.TotalTax;
        return new ReturnSummary(estimate, withheld, balance >= 0m, Math.Abs(balance));
    }
}

public sealed class ReturnViewModel
{
    public required ReturnRequest Request { get; init; }

    public ReturnSummary? Summary { get; set; }

    public required IEnumerable<SelectListItem> TaxYears { get; init; }
}
