using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaxApp.Web.Helpers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers;

public sealed class ReturnController(ITaxCalculator calculator, IDeductionRepository deductions, IOptions<TaxOptions> options) : Controller
{
    [HttpGet]
    public IActionResult Index(ReturnRequest request)
    {
        if (string.IsNullOrEmpty(request.TaxYear))
        {
            request.TaxYear = options.Value.DefaultTaxYear;
        }

        if (!TaxTables.IsSupported(request.TaxYear))
        {
            ModelState.AddModelError("TaxYear", $"Tax year {request.TaxYear} is not supported.");
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

        var estimate = calculator.Estimate(request.TaxYear, request.Income!.Value, deductions.Total());
        model.Summary = ReturnSummary.From(estimate, request.Withheld ?? 0m);
        return View(model);
    }
}
