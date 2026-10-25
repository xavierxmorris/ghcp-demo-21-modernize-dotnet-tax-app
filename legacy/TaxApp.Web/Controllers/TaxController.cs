using System.Web.Mvc;
using log4net;
using TaxApp.Web.Helpers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers
{
    public class TaxController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TaxController));

        private readonly ITaxCalculator _calculator;

        public TaxController()
            : this(new TaxCalculator())
        {
        }

        public TaxController(ITaxCalculator calculator)
        {
            _calculator = calculator;
        }

        [HttpGet]
        public ActionResult Estimate(TaxEstimateRequest request)
        {
            request = request ?? new TaxEstimateRequest();
            if (string.IsNullOrEmpty(request.TaxYear))
            {
                request.TaxYear = AppSettings.DefaultTaxYear;
            }

            if (!TaxTables.IsSupported(request.TaxYear))
            {
                ModelState.AddModelError("TaxYear", "Tax year " + request.TaxYear + " is not supported.");
            }

            var model = new TaxEstimateViewModel
            {
                Request = request,
                TaxYears = SelectLists.TaxYears(request.TaxYear)
            };

            var hasInput = request.Income.HasValue || request.Deductions.HasValue || !ModelState.IsValid;
            if (!hasInput)
            {
                return View(model);
            }

            if (!request.Income.HasValue && ModelState.IsValid)
            {
                ModelState.AddModelError("Income", "Enter your gross income.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Result = _calculator.Estimate(request.TaxYear, request.Income.Value, request.Deductions ?? 0m);
            Log.InfoFormat("Estimate calculated for {0}", request.TaxYear);
            return View(model);
        }

        [HttpGet]
        public ActionResult EstimateJson(string taxYear, decimal? income, decimal? deductions)
        {
            var year = string.IsNullOrEmpty(taxYear) ? AppSettings.DefaultTaxYear : taxYear;
            var deducted = deductions ?? 0m;

            if (!TaxTables.IsSupported(year)
                || !income.HasValue
                || income.Value < 0m || income.Value > TaxLimits.MaxAmount
                || deducted < 0m || deducted > TaxLimits.MaxAmount)
            {
                return new HttpStatusCodeResult(400, "Invalid tax year, income or deductions.");
            }

            return Json(_calculator.Estimate(year, income.Value, deducted), JsonRequestBehavior.AllowGet);
        }
    }
}
