using System;
using System.Web.Mvc;
using TinyCrm.Models;
using TinyCrm.Models.Repositories;

namespace TinyCrm.Controllers
{
    public class InteractionsController : Controller
    {
        public ActionResult Create(int? customerId)
        {
            if (!customerId.HasValue) return HttpNotFound();
            var customer = DataStore.GetCustomer(customerId.Value);
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
                DataStore.AddInteraction(model);
                TempData["Message"] = "Interaction logged.";
                return RedirectToAction("Details", "Customers", new { id = model.CustomerId });
            }

            ViewBag.CustomerName = DataStore.GetCustomer(model.CustomerId)?.Name ?? "Customer";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var interaction = default(Interaction);
            foreach (var i in DataStore.Interactions)
            {
                if (i.Id == id)
                {
                    interaction = i;
                    break;
                }
            }

            if (interaction == null)
            {
                return RedirectToAction("Index", "Customers");
            }

            var customerId = interaction.CustomerId;
            DataStore.DeleteInteraction(id);
            TempData["Message"] = "Interaction deleted.";
            return RedirectToAction("Details", "Customers", new { id = customerId });
        }
    }
}