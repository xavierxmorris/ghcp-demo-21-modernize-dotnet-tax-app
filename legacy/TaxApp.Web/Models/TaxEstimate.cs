namespace TaxApp.Web.Models
{
    public class TaxEstimate
    {
        public string TaxYear { get; set; }

        public decimal Income { get; set; }

        public decimal Deductions { get; set; }

        public decimal TaxableIncome { get; set; }

        public decimal IncomeTax { get; set; }

        public decimal MedicareLevy { get; set; }

        public decimal TotalTax { get; set; }

        public decimal NetIncome { get; set; }

        public decimal EffectiveRate { get; set; }
    }
}
