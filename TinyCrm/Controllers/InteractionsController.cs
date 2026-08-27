using System;
using System.Web.Mvc;
using TinyCrm.Data.Repositories;
using TinyCrm.Models;

namespace TinyCrm.Controllers
{
    public class InteractionsController : Controller
    {
        private readonly CustomerRepository _customers = new CustomerRepository();
        private readonly InteractionRepository _interactions = new InteractionRepository();

        public ActionResult Create(int? customerId)
        {
            if (!customerId.HasValue) return HttpNotFound();
            var customer = _customers.GetCustomer(customerId.Value);
            if (customer == null)
            {
                return HttpNotFound();
            }

            var model = new Interaction
            {
                CustomerId = customerId.Value,
                InteractionDate = DateTime.Today
            };

            ViewBag.CustomerName = customer.Name;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Interaction model)
        {
            if (model.InteractionDate.Date > DateTime.Today)
            {
                ModelState.AddModelError("InteractionDate", "Interaction date cannot be in the future.");
            }

            if (ModelState.IsValid)
            {
                _interactions.AddInteraction(model);
                TempData["Message"] = "Interaction logged.";
                return RedirectToAction("Details", "Customers", new { id = model.CustomerId });
            }

            ViewBag.CustomerName = _customers.GetCustomer(model.CustomerId)?.Name ?? "Customer";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var interaction = _interactions.GetInteraction(id);

            if (interaction == null)
            {
                return RedirectToAction("Index", "Customers");
            }

            var customerId = interaction.CustomerId;
            _interactions.DeleteInteraction(id);
            TempData["Message"] = "Interaction deleted.";
            return RedirectToAction("Details", "Customers", new { id = customerId });
        }
    }
}