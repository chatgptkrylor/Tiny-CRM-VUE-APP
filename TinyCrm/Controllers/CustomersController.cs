using System;
using System.Linq;
using System.Web.Mvc;
using TinyCrm.Models;
using TinyCrm.Models.Repositories;

namespace TinyCrm.Controllers
{
    public class CustomersController : Controller
    {
        public ActionResult Index(string search, string status)
        {
            ViewBag.Search = search;
            ViewBag.Status = status;

            var list = DataStore.Customers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                list = list.Where(c => c.Name.Contains(s) || c.Email.Contains(s) || c.Company.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                CustomerStatus st;
                if (Enum.TryParse(status, out st))
                {
                    list = list.Where(c => c.Status == st);
                }
            }

            return View(list.ToList());
        }

        public ActionResult Create()
        {
            return View(new Customer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Customer model)
        {
            if (ModelState.IsValid)
            {
                DataStore.AddCustomer(model);
                TempData["Message"] = "Customer added.";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public ActionResult Edit(int id)
        {
            var customer = DataStore.GetCustomer(id);
            if (customer == null) return HttpNotFound();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Customer model)
        {
            if (ModelState.IsValid)
            {
                if (!DataStore.UpdateCustomer(model)) return HttpNotFound();
                TempData["Message"] = "Customer updated.";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public ActionResult Details(int id)
        {
            var customer = DataStore.GetCustomer(id);
            if (customer == null) return HttpNotFound();
            return View(customer);
        }

        public ActionResult Delete(int id)
        {
            var customer = DataStore.GetCustomer(id);
            if (customer == null) return HttpNotFound();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, FormCollection collection)
        {
            DataStore.DeleteCustomer(id);
            TempData["Message"] = "Customer deleted.";
            return RedirectToAction("Index");
        }
    }
}