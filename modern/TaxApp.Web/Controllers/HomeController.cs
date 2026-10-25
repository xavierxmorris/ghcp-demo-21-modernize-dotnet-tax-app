using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers;

public sealed class HomeController(IOptions<TaxOptions> options) : Controller
{
    public IActionResult Index() =>
        View(new HomeViewModel
        {
            DefaultTaxYear = options.Value.DefaultTaxYear,
            SupportedTaxYears = TaxTables.SupportedYears
        });

    public IActionResult Error() => View();
}
