using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace TaxApp.Web.Models
{
    public enum GstMode
    {
        AddGst = 0,
        ExtractGst = 1
    }

    public class GstRequest
    {
        [Display(Name = "Amount")]
        [Range(0, 100000000, ErrorMessage = "Amount must be between $0 and $100,000,000.")]
        public decimal? Amount { get; set; }

        [Display(Name = "Calculation")]
        public GstMode Mode { get; set; }
    }

    public class GstResult
    {
        public GstMode Mode { get; set; }

        public decimal Amount { get; set; }

        public decimal GstAmount { get; set; }

        public decimal AmountExclGst { get; set; }

        public decimal AmountInclGst { get; set; }
    }

    public class GstViewModel
    {
        public GstRequest Request { get; set; }

        public GstResult Result { get; set; }

        public IEnumerable<SelectListItem> Modes { get; set; }
    }
}
