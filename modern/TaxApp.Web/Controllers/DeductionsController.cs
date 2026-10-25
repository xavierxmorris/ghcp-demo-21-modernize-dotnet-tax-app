using Microsoft.AspNetCore.Mvc;
using TaxApp.Web.Helpers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers;

public sealed class DeductionsController(IDeductionRepository repository, ILogger<DeductionsController> logger) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(BuildModel(new DeductionInput()));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(DeductionInput input)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", BuildModel(input));
        }

        repository.Add(input.Description!, input.Category, input.Amount!.Value);
        logger.LogInformation("Deduction added (category {Category})", input.Category);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(Guid id)
    {
        if (repository.Remove(id))
        {
            logger.LogInformation("Deduction removed ({Id})", id);
        }

        return RedirectToAction(nameof(Index));
    }

    private DeductionsViewModel BuildModel(DeductionInput input)
    {
        var items = repository.GetAll().OrderBy(d => d.AddedUtc).ToList();
        return new DeductionsViewModel
        {
            Items = items,
            Total = items.Sum(d => d.Amount),
            Input = input,
            Categories = SelectLists.Categories(input.Category)
        };
    }
}
