using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace TaxApp.Web.Models
{
    public static class TaxLimits
    {
        public const decimal MaxAmount = 10000000m;
    }

    public class HomeViewModel
    {
        public string DefaultTaxYear { get; set; }

        public IEnumerable<string> SupportedTaxYears { get; set; }
    }

    public class TaxEstimateRequest
    {
        [Display(Name = "Tax year")]
        public string TaxYear { get; set; }

        [Display(Name = "Gross income")]
        [Range(0, 10000000, ErrorMessage = "Gross income must be between $0 and $10,000,000.")]
        public decimal? Income { get; set; }

        [Display(Name = "Deductions")]
        [Range(0, 10000000, ErrorMessage = "Deductions must be between $0 and $10,000,000.")]
        public decimal? Deductions { get; set; }
    }

    public class TaxEstimateViewModel
    {
        public TaxEstimateRequest Request { get; set; }

        public TaxEstimate Result { get; set; }

        public IEnumerable<SelectListItem> TaxYears { get; set; }
    }
}
