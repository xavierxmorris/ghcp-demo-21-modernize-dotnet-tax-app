using System.Web.Mvc;
using TaxApp.Web.Helpers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers
{
    public class GstController : Controller
    {
        private readonly IGstCalculator _calculator;

        public GstController()
            : this(new GstCalculator())
        {
        }

        public GstController(IGstCalculator calculator)
        {
            _calculator = calculator;
        }

        [HttpGet]
        public ActionResult Index(GstRequest request)
        {
            request = request ?? new GstRequest();
            var model = new GstViewModel
            {
                Request = request,
                Modes = SelectLists.GstModes(request.Mode)
            };

            if (!request.Amount.HasValue && ModelState.IsValid)
            {
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Result = _calculator.Calculate(request.Amount.Value, request.Mode);
            return View(model);
        }
    }
}
