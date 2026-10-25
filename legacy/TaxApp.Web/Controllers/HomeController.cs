using System.Web.Mvc;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(new HomeViewModel
            {
                DefaultTaxYear = AppSettings.DefaultTaxYear,
                SupportedTaxYears = TaxTables.SupportedYears
            });
        }
    }
}
