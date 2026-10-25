using System;
using System.Linq;
using System.Web.Mvc;
using log4net;
using TaxApp.Web.Helpers;
using TaxApp.Web.Models;
using TaxApp.Web.Services;

namespace TaxApp.Web.Controllers
{
    public class DeductionsController : Controller
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DeductionsController));

        private readonly IDeductionRepository _repository;

        public DeductionsController()
            : this(new DeductionRepository())
        {
        }

        public DeductionsController(IDeductionRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public ActionResult Index()
        {
            return View(BuildModel(new DeductionInput()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(DeductionInput input)
        {
            input = input ?? new DeductionInput();
            if (!ModelState.IsValid)
            {
                return View("Index", BuildModel(input));
            }

            _repository.Add(input.Description, input.Category, input.Amount.Value);
            Log.InfoFormat("Deduction added (category {0})", input.Category);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(Guid id)
        {
            if (_repository.Remove(id))
            {
                Log.InfoFormat("Deduction removed ({0})", id);
            }

            return RedirectToAction("Index");
        }

        private DeductionsViewModel BuildModel(DeductionInput input)
        {
            var items = _repository.GetAll().OrderBy(d => d.AddedUtc).ToList();
            return new DeductionsViewModel
            {
                Items = items,
                Total = items.Sum(d => d.Amount),
                Input = input,
                Categories = SelectLists.Categories(input.Category)
            };
        }
    }
}
