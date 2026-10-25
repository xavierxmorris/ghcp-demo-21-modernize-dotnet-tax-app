using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc.Rendering;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Helpers;

public static class MoneyFormatter
{
    private static readonly CultureInfo AustralianCulture = new("en-AU");

    /// <summary>Culture-neutral value for data-value attributes and machine checks.</summary>
    public static string Raw(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Display(decimal value) => value.ToString("C2", AustralianCulture);

    public static string Percent(decimal value) => Raw(value) + "%";
}

public static class EnumDisplay
{
    public static string Name(DeductionCategory category) => category switch
    {
        DeductionCategory.WorkTravel => "Work-related travel",
        DeductionCategory.HomeOffice => "Home office",
        DeductionCategory.SelfEducation => "Self-education",
        DeductionCategory.GiftsAndDonations => "Gifts and donations",
        DeductionCategory.ToolsAndEquipment => "Tools and equipment",
        _ => "Other"
    };

    public static string Name(GstMode mode) => mode == GstMode.AddGst ? "Add GST to a price" : "Extract GST from a price";
}

public static class SelectLists
{
    public static IEnumerable<SelectListItem> TaxYears(string? selected) =>
        TaxTables.SupportedYears
            .Select(year => new SelectListItem { Value = year, Text = year, Selected = year == selected })
            .ToList();

    public static IEnumerable<SelectListItem> Categories(DeductionCategory selected) =>
        Enum.GetValues<DeductionCategory>()
            .Select(c => new SelectListItem { Value = c.ToString(), Text = EnumDisplay.Name(c), Selected = c == selected })
            .ToList();

    public static IEnumerable<SelectListItem> GstModes(GstMode selected) =>
        Enum.GetValues<GstMode>()
            .Select(m => new SelectListItem { Value = m.ToString(), Text = EnumDisplay.Name(m), Selected = m == selected })
            .ToList();
}

/// <summary>Shows which runtime is serving the page, so before and after are visible in the UI.</summary>
public static class RuntimeInfo
{
    public static string Description => RuntimeInformation.FrameworkDescription;
}
