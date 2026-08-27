using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using TinyCrm.Infrastructure;
using TinyCrm.Models;

namespace TinyCrm.Data
{
    // Seeds an empty TinyCrm database with the initial demo data:
    // 2 users (admin/admin123, demo/demo123), 5 customers and 6 interactions.
    // Mirrors the seed data previously provided by the in-memory DataStore.
    public static class DatabaseSeeder
    {
        public static void Seed(TinyCrmEntities context)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (context.Users.Any() || context.Customers.Any()) return;

            context.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = PasswordHasher.Hash("admin123"),
                DisplayName = "Administrator"
            });
            context.Users.Add(new User
            {
                Username = "demo",
                PasswordHash = PasswordHasher.Hash("demo123"),
                DisplayName = "Demo User"
            });

            var c1 = context.Customers.Add(new Customer
            {
                Name = "Acme Corp",
                Company = "John Smith",
                Email = "john@acme.example",
                Phone = "+1 555 100 2000",
                Status = CustomerStatus.Customer,
                Notes = "Early adopter, renewal due next quarter.",
                CreatedAt = DateTime.Now
            });
            var c2 = context.Customers.Add(new Customer
            {
                Name = "Globex Inc",
                Company = "Alice Cooper",
                Email = "alice@globex.example",
                Phone = "+1 555 100 2001",
                Status = CustomerStatus.Lead,
                Notes = "Prospect from trade show.",
                CreatedAt = DateTime.Now
            });
            var c3 = context.Customers.Add(new Customer
            {
                Name = "Initech",
                Company = "Peter Gibbons",
                Email = "peter@initech.example",
                Phone = "+1 555 100 2002",
                Status = CustomerStatus.Customer,
                Notes = "On annual plan.",
                CreatedAt = DateTime.Now
            });
            var c4 = context.Customers.Add(new Customer
            {
                Name = "Umbrella Ltd",
                Company = "Alice Abernathy",
                Email = "alice@umbrella.example",
                Phone = "+1 555 100 2003",
                Status = CustomerStatus.Contact,
                Notes = "In evaluation, win-back in progress.",
                CreatedAt = DateTime.Now
            });
            var c5 = context.Customers.Add(new Customer
            {
                Name = "Soylent Co",
                Company = "Robert Paulson",
                Email = "bob@soylent.example",
                Phone = "+1 555 100 2004",
                Status = CustomerStatus.Lead,
                Notes = "",
                CreatedAt = DateTime.Now
            });

            context.SaveChanges();

            context.Interactions.Add(new Interaction
            {
                CustomerId = c1.Id,
                Type = InteractionType.Call,
                Subject = "Onboarding call",
                Notes = "Walked through main features.",
                InteractionDate = DateTime.Now.AddDays(-3),
                CreatedAt = DateTime.Now
            });
            context.Interactions.Add(new Interaction
            {
                CustomerId = c1.Id,
                Type = InteractionType.Email,
                Subject = "Pricing follow-up",
                Notes = "Sent revised quote.",
                InteractionDate = DateTime.Now.AddDays(-2),
                CreatedAt = DateTime.Now
            });
            context.Interactions.Add(new Interaction
            {
                CustomerId = c2.Id,
                Type = InteractionType.Meeting,
                Subject = "Discovery meeting",
                Notes = "Requirements gathering.",
                InteractionDate = DateTime.Now.AddDays(-1),
                CreatedAt = DateTime.Now
            });
            context.Interactions.Add(new Interaction
            {
                CustomerId = c3.Id,
                Type = InteractionType.Email,
                Subject = "Support ticket #42",
                Notes = "Reset credentials.",
                InteractionDate = DateTime.Now.AddDays(-5),
                CreatedAt = DateTime.Now
            });
            context.Interactions.Add(new Interaction
            {
                CustomerId = c4.Id,
                Type = InteractionType.Call,
                Subject = "Win-back call",
                Notes = "Offered discount to return.",
                InteractionDate = DateTime.Now.AddDays(-40),
                CreatedAt = DateTime.Now
            });
            context.Interactions.Add(new Interaction
            {
                CustomerId = c5.Id,
                Type = InteractionType.Note,
                Subject = "Imported from spreadsheet",
                Notes = "No contact yet.",
                InteractionDate = DateTime.Now.AddDays(-10),
                CreatedAt = DateTime.Now
            });

            context.SaveChanges();

            RecalculateLastInteractionDates(context);
            context.SaveChanges();
        }

        public static void RecalculateLastInteractionDates(TinyCrmEntities context)
        {
            var lastDates = context.Interactions
                .GroupBy(i => i.CustomerId)
                .Select(g => new { CustomerId = g.Key, Last = g.Max(i => i.InteractionDate) })
                .ToDictionary(x => x.CustomerId, x => x.Last);

            foreach (var customer in context.Customers)
            {
                DateTime last;
                customer.LastInteractionDate = lastDates.TryGetValue(customer.Id, out last) ? last : (DateTime?)null;
            }
        }
    }
}
