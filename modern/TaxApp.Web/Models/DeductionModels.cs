using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TaxApp.Web.Models;

public enum DeductionCategory
{
    WorkTravel,
    HomeOffice,
    SelfEducation,
    GiftsAndDonations,
    ToolsAndEquipment,
    Other
}

public sealed class Deduction
{
    public Guid Id { get; set; }

    public string Description { get; set; } = string.Empty;

    public DeductionCategory Category { get; set; }

    public decimal Amount { get; set; }

    public DateTime AddedUtc { get; set; }
}

public sealed class DeductionInput
{
    [Required(ErrorMessage = "Enter a description.")]
    [StringLength(100, ErrorMessage = "Description must be 100 characters or fewer.")]
    public string? Description { get; set; }

    public DeductionCategory Category { get; set; }

    [Required(ErrorMessage = "Enter an amount.")]
    [Range(0.01, 1000000, ErrorMessage = "Amount must be between $0.01 and $1,000,000.")]
    public decimal? Amount { get; set; }
}

public sealed class DeductionsViewModel
{
    public required IReadOnlyList<Deduction> Items { get; init; }

    public required decimal Total { get; init; }

    public required DeductionInput Input { get; init; }

    public required IEnumerable<SelectListItem> Categories { get; init; }
}
