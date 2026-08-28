using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TinyCrm.Api.Data;
using Xunit;

namespace TinyCrm.Api.Tests;

[Collection("api")]
public class SeedTests
{
    private readonly ApiFactory _factory;
    public SeedTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Seed_CreatesUsersCustomersAndInteractions()
    {
        _factory.CreateClient();   // boots the app: migrate + seed
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();

        Assert.Equal(2, db.Users.Count());
        Assert.Equal(5, db.Customers.Count());
        Assert.Equal(6, db.Interactions.Count());
    }

    [Fact]
    public void Seed_HashesPasswordsWithPbkdf2_NotSha256()
    {
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
        var admin = db.Users.Single(u => u.Username == "admin");

        // PBKDF2 hashes are Base64 and far longer than a 64-char SHA-256 hex string.
        Assert.NotEqual(64, admin.PasswordHash.Length);
        Assert.Contains("=", admin.PasswordHash);
    }

    [Fact]
    public void DeletingCustomer_CascadesToInteractions()
    {
        _factory.CreateClient();   // boots the app: migrate + seed
        int id;

        // Scope 1: create and save a customer with a child interaction. Both are
        // discarded with this scope, so nothing here is tracked afterwards.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();

            // Creates its own data: deleting a SEEDED customer would break the
            // "5 seeded customers" assertion in CustomersTests (shared database).
            var customer = new Models.Customer { Name = "Cascade Target", Status = Models.CustomerStatus.Lead, CreatedAt = DateTime.Now };
            customer.Interactions.Add(new Models.Interaction
            {
                Type = Models.InteractionType.Note,
                Subject = "Cascade child",
                InteractionDate = DateTime.Today,
                CreatedAt = DateTime.Now,
            });
            db.Customers.Add(customer);
            db.SaveChanges();
            id = customer.Id;
        }

        // Scope 2: a FRESH DbContext loads only the customer, no Include, so the
        // child interaction is never tracked here. Removing the customer in this
        // scope can only delete the interaction via the database's own
        // ON DELETE CASCADE - EF's client-side cascade needs the child tracked,
        // which it isn't. This is what actually pins the database behaviour.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
            var customer = db.Customers.Single(c => c.Id == id);
            db.Customers.Remove(customer);
            db.SaveChanges();
        }

        // Scope 3: a THIRD fresh DbContext confirms the interaction is really
        // gone from the database, not just untracked in memory.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
            Assert.Empty(db.Interactions.Where(i => i.CustomerId == id));
            Assert.Equal(5, db.Customers.Count());   // seed data untouched
        }
    }
}
