using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Helpers
{
    public static class SelectLists
    {
        public static IEnumerable<SelectListItem> TaxYears(string selected)
        {
            return TaxTables.SupportedYears
                .Select(year => new SelectListItem { Value = year, Text = year, Selected = year == selected })
                .ToList();
        }

        public static IEnumerable<SelectListItem> Categories(DeductionCategory selected)
        {
            return System.Enum.GetValues(typeof(DeductionCategory))
                .Cast<DeductionCategory>()
                .Select(c => new SelectListItem { Value = c.ToString(), Text = EnumDisplay.Name(c), Selected = c == selected })
                .ToList();
        }

        public static IEnumerable<SelectListItem> GstModes(GstMode selected)
        {
            return System.Enum.GetValues(typeof(GstMode))
                .Cast<GstMode>()
                .Select(m => new SelectListItem { Value = m.ToString(), Text = EnumDisplay.Name(m), Selected = m == selected })
                .ToList();
        }
    }
}
