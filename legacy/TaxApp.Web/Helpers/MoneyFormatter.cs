using System.Globalization;
using TaxApp.Web.Models;

namespace TaxApp.Web.Helpers
{
    public static class MoneyFormatter
    {
        private static readonly CultureInfo AustralianCulture = new CultureInfo("en-AU");

        /// <summary>Culture-neutral value for data-value attributes and machine checks.</summary>
        public static string Raw(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string Display(decimal value)
        {
            return value.ToString("C2", AustralianCulture);
        }

        public static string Percent(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }
    }

    public static class EnumDisplay
    {
        public static string Name(DeductionCategory category)
        {
            switch (category)
            {
                case DeductionCategory.WorkTravel: return "Work-related travel";
                case DeductionCategory.HomeOffice: return "Home office";
                case DeductionCategory.SelfEducation: return "Self-education";
                case DeductionCategory.GiftsAndDonations: return "Gifts and donations";
                case DeductionCategory.ToolsAndEquipment: return "Tools and equipment";
                default: return "Other";
            }
        }

        public static string Name(GstMode mode)
        {
            return mode == GstMode.AddGst ? "Add GST to a price" : "Extract GST from a price";
        }
    }
}
