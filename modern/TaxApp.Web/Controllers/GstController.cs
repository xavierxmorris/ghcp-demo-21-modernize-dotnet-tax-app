using Microsoft.AspNetCore.Mvc;
using TaxApp.Web.Helpers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers;

public sealed class GstController(IGstCalculator calculator) : Controller
{
    [HttpGet]
    public IActionResult Index(GstRequest request)
    {
        var model = new GstViewModel
        {
            Request = request,
            Modes = SelectLists.GstModes(request.Mode)
        };

        if (!request.Amount.HasValue || !ModelState.IsValid)
        {
            return View(model);
        }

        model.Result = calculator.Calculate(request.Amount.Value, request.Mode);
        return View(model);
    }
}
