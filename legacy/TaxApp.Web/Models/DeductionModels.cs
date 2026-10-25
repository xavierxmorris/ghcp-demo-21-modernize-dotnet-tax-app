using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace TaxApp.Web.Models
{
    public enum DeductionCategory
    {
        WorkTravel,
        HomeOffice,
        SelfEducation,
        GiftsAndDonations,
        ToolsAndEquipment,
        Other
    }

    public class Deduction
    {
        public Guid Id { get; set; }

        public string Description { get; set; }

        public DeductionCategory Category { get; set; }

        public decimal Amount { get; set; }

        public DateTime AddedUtc { get; set; }
    }

    public class DeductionInput
    {
        [Required(ErrorMessage = "Enter a description.")]
        [StringLength(100, ErrorMessage = "Description must be 100 characters or fewer.")]
        public string Description { get; set; }

        public DeductionCategory Category { get; set; }

        [Required(ErrorMessage = "Enter an amount.")]
        [Range(0.01, 1000000, ErrorMessage = "Amount must be between $0.01 and $1,000,000.")]
        public decimal? Amount { get; set; }
    }

    public class DeductionsViewModel
    {
        public IList<Deduction> Items { get; set; }

        public decimal Total { get; set; }

        public DeductionInput Input { get; set; }

        public IEnumerable<SelectListItem> Categories { get; set; }
    }
}
