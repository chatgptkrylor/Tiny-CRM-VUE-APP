using Microsoft.AspNetCore.Identity;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Data;

public static class DatabaseSeeder
{
    public static void Seed(TinyCrmDbContext db, IPasswordHasher<User> hasher)
    {
        if (db.Users.Any() || db.Customers.Any()) return;

        var admin = new User { Username = "admin", DisplayName = "Administrator" };
        admin.PasswordHash = hasher.HashPassword(admin, "admin123");
        var demo = new User { Username = "demo", DisplayName = "Demo User" };
        demo.PasswordHash = hasher.HashPassword(demo, "demo123");
        db.Users.AddRange(admin, demo);

        var now = DateTime.Now;
        var customers = new List<Customer>
        {
            new() { Name = "Acme Corp",    Company = "John Smith",      Email = "john@acme.example",   Phone = "+1 555 100 2000", Status = CustomerStatus.Customer, Notes = "Early adopter, renewal due next quarter.", CreatedAt = now },
            new() { Name = "Globex Inc",   Company = "Alice Cooper",    Email = "alice@globex.example",Phone = "+1 555 100 2001", Status = CustomerStatus.Lead,     Notes = "Prospect from trade show.",               CreatedAt = now },
            new() { Name = "Initech",      Company = "Peter Gibbons",   Email = "peter@initech.example",Phone = "+1 555 100 2002",Status = CustomerStatus.Customer, Notes = "On annual plan.",                         CreatedAt = now },
            new() { Name = "Umbrella Ltd", Company = "Alice Abernathy", Email = "alice@umbrella.example",Phone = "+1 555 100 2003",Status = CustomerStatus.Contact,  Notes = "In evaluation, win-back in progress.",    CreatedAt = now },
            new() { Name = "Soylent Co",   Company = "Robert Paulson",  Email = "bob@soylent.example", Phone = "+1 555 100 2004", Status = CustomerStatus.Lead,     Notes = "",                                        CreatedAt = now },
        };
        db.Customers.AddRange(customers);
        db.SaveChanges();

        db.Interactions.AddRange(
            new Interaction { CustomerId = customers[0].Id, Type = InteractionType.Call,    Subject = "Onboarding call",           Notes = "Walked through main features.", InteractionDate = now.AddDays(-3),  CreatedAt = now },
            new Interaction { CustomerId = customers[0].Id, Type = InteractionType.Email,   Subject = "Pricing follow-up",         Notes = "Sent revised quote.",           InteractionDate = now.AddDays(-2),  CreatedAt = now },
            new Interaction { CustomerId = customers[1].Id, Type = InteractionType.Meeting, Subject = "Discovery meeting",         Notes = "Requirements gathering.",       InteractionDate = now.AddDays(-1),  CreatedAt = now },
            new Interaction { CustomerId = customers[2].Id, Type = InteractionType.Email,   Subject = "Support ticket #42",        Notes = "Reset credentials.",            InteractionDate = now.AddDays(-5),  CreatedAt = now },
            new Interaction { CustomerId = customers[3].Id, Type = InteractionType.Call,    Subject = "Win-back call",             Notes = "Offered discount to return.",   InteractionDate = now.AddDays(-40), CreatedAt = now },
            new Interaction { CustomerId = customers[4].Id, Type = InteractionType.Note,    Subject = "Imported from spreadsheet", Notes = "No contact yet.",               InteractionDate = now.AddDays(-10), CreatedAt = now });
        db.SaveChanges();

        RecalculateLastInteractionDates(db);
        db.SaveChanges();
    }

    public static void RecalculateLastInteractionDates(TinyCrmDbContext db)
    {
        var last = db.Interactions
            .GroupBy(i => i.CustomerId)
            .Select(g => new { CustomerId = g.Key, Max = g.Max(i => i.InteractionDate) })
            .ToDictionary(x => x.CustomerId, x => x.Max);

        foreach (var c in db.Customers)
            c.LastInteractionDate = last.TryGetValue(c.Id, out var d) ? d : null;
    }
}
