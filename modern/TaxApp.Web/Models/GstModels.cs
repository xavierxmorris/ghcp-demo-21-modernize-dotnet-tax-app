using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TaxApp.Web.Models;

public enum GstMode
{
    AddGst = 0,
    ExtractGst = 1
}

public sealed class GstRequest
{
    [Display(Name = "Amount")]
    [Range(0, 100000000, ErrorMessage = "Amount must be between $0 and $100,000,000.")]
    public decimal? Amount { get; set; }

    [Display(Name = "Calculation")]
    public GstMode Mode { get; set; }
}

public sealed record GstResult(GstMode Mode, decimal Amount, decimal GstAmount, decimal AmountExclGst, decimal AmountInclGst);

public sealed class GstViewModel
{
    public required GstRequest Request { get; init; }

    public GstResult? Result { get; set; }

    public required IEnumerable<SelectListItem> Modes { get; init; }
}
