using System;
using System.Linq;
using System.Web.Mvc;
using TinyCrm.Data.Repositories;
using TinyCrm.Models;

namespace TinyCrm.Controllers
{
    public class CustomersController : Controller
    {
        private readonly CustomerRepository _customers = new CustomerRepository();

        public ActionResult Index(string search, string status)
        {
            ViewBag.Search = search;
            ViewBag.Status = status;

            var list = _customers.GetAll().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                list = list.Where(c =>
                    (c.Name != null && c.Name.Contains(s)) ||
                    (c.Email != null && c.Email.Contains(s)) ||
                    (c.Company != null && c.Company.Contains(s)));
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
                _customers.AddCustomer(model);
                TempData["Message"] = "Customer added.";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public ActionResult Edit(int? id)
        {
            if (!id.HasValue) return HttpNotFound();
            var customer = _customers.GetCustomer(id.Value);
            if (customer == null) return HttpNotFound();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Customer model)
        {
            if (ModelState.IsValid)
            {
                if (!_customers.UpdateCustomer(model)) return HttpNotFound();
                TempData["Message"] = "Customer updated.";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public ActionResult Details(int? id)
        {
            if (!id.HasValue) return HttpNotFound();
            var customer = _customers.GetCustomer(id.Value);
            if (customer == null) return HttpNotFound();
            return View(customer);
        }

        public ActionResult Delete(int? id)
        {
            if (!id.HasValue) return HttpNotFound();
            var customer = _customers.GetCustomer(id.Value);
            if (customer == null) return HttpNotFound();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, FormCollection collection)
        {
            _customers.DeleteCustomer(id);
            TempData["Message"] = "Customer deleted.";
            return RedirectToAction("Index");
        }
    }
}