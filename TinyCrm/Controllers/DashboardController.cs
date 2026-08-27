using System;
using System.Linq;
using System.Web.Mvc;
using TinyCrm.Data.Repositories;
using TinyCrm.Models;

namespace TinyCrm.Controllers
{
    public class DashboardController : Controller
    {
        private readonly CustomerRepository _customers = new CustomerRepository();
        private readonly InteractionRepository _interactions = new InteractionRepository();

        public ActionResult Index()
        {
            var customers = _customers.GetAll();
            var interactions = _interactions.GetAll();

            var model = new DashboardViewModel
            {
                TotalCustomers = customers.Count,
                TotalInteractions = interactions.Count
            };

            foreach (CustomerStatus status in Enum.GetValues(typeof(CustomerStatus)))
            {
                model.CustomersByStatus[status] = customers.Count(c => c.Status == status);
            }

            foreach (InteractionType type in Enum.GetValues(typeof(InteractionType)))
            {
                model.InteractionsByType[type] = interactions.Count(i => i.Type == type);
            }

            model.RecentInteractions = interactions
                .OrderByDescending(i => i.InteractionDate)
                .Take(5)
                .ToList();

            var cutoff = DateTime.Today.AddDays(-30);
            model.NeedsFollowUps = customers.Count(c =>
                c.LastInteractionDate == null || c.LastInteractionDate < cutoff);

            return View(model);
        }
    }
}