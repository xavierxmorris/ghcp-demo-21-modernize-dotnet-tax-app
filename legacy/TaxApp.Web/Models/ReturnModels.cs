using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace TaxApp.Web.Models
{
    public class ReturnRequest
    {
        [Display(Name = "Tax year")]
        public string TaxYear { get; set; }

        [Display(Name = "Gross income")]
        [Range(0, 10000000, ErrorMessage = "Gross income must be between $0 and $10,000,000.")]
        public decimal? Income { get; set; }

        [Display(Name = "Tax withheld")]
        [Range(0, 10000000, ErrorMessage = "Tax withheld must be between $0 and $10,000,000.")]
        public decimal? Withheld { get; set; }
    }

    public class ReturnSummary
    {
        public TaxEstimate Estimate { get; set; }

        public decimal Withheld { get; set; }

        public bool IsRefund { get; set; }

        public decimal ResultAmount { get; set; }

        public static ReturnSummary From(TaxEstimate estimate, decimal withheld)
        {
            var balance = withheld - estimate.TotalTax;
            return new ReturnSummary
            {
                Estimate = estimate,
                Withheld = withheld,
                IsRefund = balance >= 0m,
                ResultAmount = Math.Abs(balance)
            };
        }
    }

    public class ReturnViewModel
    {
        public ReturnRequest Request { get; set; }

        public ReturnSummary Summary { get; set; }

        public IEnumerable<SelectListItem> TaxYears { get; set; }
    }
}
