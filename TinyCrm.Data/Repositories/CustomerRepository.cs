using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using TinyCrm.Models;

namespace TinyCrm.Data.Repositories
{
    // EF6-based middle tier for customers. Replaces the old static
    // in-memory DataStore. Short-lived DbContext per operation.
    public class CustomerRepository
    {
        public IList<Customer> GetAll()
        {
            using (var ctx = DbContextFactory.Create())
            {
                return ctx.Customers.AsNoTracking()
                    .OrderBy(c => c.Id)
                    .ToList();
            }
        }

        public Customer GetCustomer(int id)
        {
            using (var ctx = DbContextFactory.Create())
            {
                var customer = ctx.Customers.AsNoTracking()
                    .Include(c => c.Interactions)
                    .FirstOrDefault(c => c.Id == id);
                if (customer == null) return null;

                customer.Interactions = customer.Interactions
                    .OrderByDescending(i => i.InteractionDate)
                    .ToList();
                foreach (var i in customer.Interactions)
                {
                    i.CustomerName = customer.Name;
                }
                return customer;
            }
        }

        public Customer AddCustomer(Customer c)
        {
            using (var ctx = DbContextFactory.Create())
            {
                var entity = new Customer
                {
                    Name = c.Name,
                    Company = c.Company,
                    Email = c.Email,
                    Phone = c.Phone,
                    Status = c.Status,
                    Notes = c.Notes,
                    CreatedAt = DateTime.Now
                };
                ctx.Customers.Add(entity);
                ctx.SaveChanges();
                return entity;
            }
        }

        public bool UpdateCustomer(Customer updated)
        {
            using (var ctx = DbContextFactory.Create())
            {
                var customer = ctx.Customers.FirstOrDefault(c => c.Id == updated.Id);
                if (customer == null) return false;

                customer.Name = updated.Name;
                customer.Company = updated.Company;
                customer.Email = updated.Email;
                customer.Phone = updated.Phone;
                customer.Status = updated.Status;
                customer.Notes = updated.Notes;
                ctx.SaveChanges();
                return true;
            }
        }

        public bool DeleteCustomer(int id)
        {
            using (var ctx = DbContextFactory.Create())
            {
                // Interactions are loaded so EF performs client-side cascade;
                // the database FK also cascades as a backstop.
                var customer = ctx.Customers.Include(c => c.Interactions)
                    .FirstOrDefault(c => c.Id == id);
                if (customer == null) return false;

                ctx.Customers.Remove(customer);
                ctx.SaveChanges();
                return true;
            }
        }
    }
}
