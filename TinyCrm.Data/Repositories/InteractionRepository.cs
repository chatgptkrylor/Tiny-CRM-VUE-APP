using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using TinyCrm.Models;

namespace TinyCrm.Data.Repositories
{
    // EF6-based middle tier for interactions. Replaces the old static
    // in-memory DataStore. Short-lived DbContext per operation.
    public class InteractionRepository
    {
        public IList<Interaction> GetAll()
        {
            using (var ctx = DbContextFactory.Create())
            {
                return ctx.Interactions.AsNoTracking()
                    .OrderBy(i => i.Id)
                    .ToList();
            }
        }

        public Interaction GetInteraction(int id)
        {
            using (var ctx = DbContextFactory.Create())
            {
                return ctx.Interactions.AsNoTracking()
                    .FirstOrDefault(i => i.Id == id);
            }
        }

        public Interaction AddInteraction(Interaction i)
        {
            using (var ctx = DbContextFactory.Create())
            {
                // Mirror the old DataStore leniency: unknown customers are ignored.
                var customer = ctx.Customers.FirstOrDefault(c => c.Id == i.CustomerId);
                if (customer == null) return null;

                var entity = new Interaction
                {
                    CustomerId = i.CustomerId,
                    Type = i.Type,
                    Subject = i.Subject,
                    Notes = i.Notes,
                    InteractionDate = i.InteractionDate,
                    CreatedAt = DateTime.Now
                };
                ctx.Interactions.Add(entity);
                ctx.SaveChanges();

                RecalculateForCustomer(ctx, entity.CustomerId);
                ctx.SaveChanges();

                entity.CustomerName = customer.Name;
                return entity;
            }
        }

        public bool DeleteInteraction(int id)
        {
            using (var ctx = DbContextFactory.Create())
            {
                var interaction = ctx.Interactions.FirstOrDefault(i => i.Id == id);
                if (interaction == null) return false;

                var customerId = interaction.CustomerId;
                ctx.Interactions.Remove(interaction);
                ctx.SaveChanges();

                RecalculateForCustomer(ctx, customerId);
                ctx.SaveChanges();
                return true;
            }
        }

        // Recalculates LastInteractionDate for every customer.
        // Kept for parity with the old DataStore API.
        public void RecalculateLastInteractionDates()
        {
            using (var ctx = DbContextFactory.Create())
            {
                DatabaseSeeder.RecalculateLastInteractionDates(ctx);
                ctx.SaveChanges();
            }
        }

        private static void RecalculateForCustomer(TinyCrmEntities ctx, int customerId)
        {
            var customer = ctx.Customers.FirstOrDefault(c => c.Id == customerId);
            if (customer == null) return;

            customer.LastInteractionDate = ctx.Interactions
                .Where(i => i.CustomerId == customerId)
                .Select(i => (DateTime?)i.InteractionDate)
                .Max();
        }
    }
}
