using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaxApp.Web.Helpers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers;

public sealed class TaxController(ITaxCalculator calculator, IOptions<TaxOptions> options, ILogger<TaxController> logger) : Controller
{
    [HttpGet]
    public IActionResult Estimate(TaxEstimateRequest request)
    {
        if (string.IsNullOrEmpty(request.TaxYear))
        {
            request.TaxYear = options.Value.DefaultTaxYear;
        }

        if (!TaxTables.IsSupported(request.TaxYear))
        {
            ModelState.AddModelError("TaxYear", $"Tax year {request.TaxYear} is not supported.");
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

        model.Result = calculator.Estimate(request.TaxYear, request.Income!.Value, request.Deductions ?? 0m);
        logger.LogInformation("Estimate calculated for {TaxYear}", request.TaxYear);
        return View(model);
    }

    [HttpGet]
    public IActionResult EstimateJson(string? taxYear, decimal? income, decimal? deductions)
    {
        var year = string.IsNullOrEmpty(taxYear) ? options.Value.DefaultTaxYear : taxYear;
        var deducted = deductions ?? 0m;

        if (!TaxTables.IsSupported(year)
            || !income.HasValue
            || income.Value < 0m || income.Value > TaxLimits.MaxAmount
            || deducted < 0m || deducted > TaxLimits.MaxAmount)
        {
            return BadRequest();
        }

        return Json(calculator.Estimate(year, income.Value, deducted));
    }
}
