using System.Web.Mvc;
using TaxApp.Web.Helpers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers
{
    public class ReturnController : Controller
    {
        private readonly ITaxCalculator _calculator;
        private readonly IDeductionRepository _deductions;

        public ReturnController()
            : this(new TaxCalculator(), new DeductionRepository())
        {
        }

        public ReturnController(ITaxCalculator calculator, IDeductionRepository deductions)
        {
            _calculator = calculator;
            _deductions = deductions;
        }

        [HttpGet]
        public ActionResult Index(ReturnRequest request)
        {
            request = request ?? new ReturnRequest();
            if (string.IsNullOrEmpty(request.TaxYear))
            {
                request.TaxYear = AppSettings.DefaultTaxYear;
            }

            if (!TaxTables.IsSupported(request.TaxYear))
            {
                ModelState.AddModelError("TaxYear", "Tax year " + request.TaxYear + " is not supported.");
            }

            var model = new ReturnViewModel
            {
                Request = request,
                TaxYears = SelectLists.TaxYears(request.TaxYear)
            };

            var hasInput = request.Income.HasValue || request.Withheld.HasValue || !ModelState.IsValid;
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

            var estimate = _calculator.Estimate(request.TaxYear, request.Income.Value, _deductions.Total());
            model.Summary = ReturnSummary.From(estimate, request.Withheld ?? 0m);
            return View(model);
        }
    }
}
