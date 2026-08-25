using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinyCrm.Models;
using TinyCrm.Models.Repositories;

namespace TinyCrm.Tests
{
    [TestClass]
    public class DataStoreTests
    {
        [TestMethod]
        public void SeedCustomersArePresent()
        {
            DataStore.Seed();
            Assert.IsTrue(DataStore.Customers.Count >= 5);
            Assert.IsTrue(DataStore.Interactions.Count >= 6);
        }

        [TestMethod]
        public void AddCustomer_IncrementsCountAndIsRetrievable()
        {
            int countBefore = DataStore.Customers.Count;
            var c = new Customer
            {
                Name = "Test Customer",
                Company = "Co",
                Email = "t@e.com",
                Phone = "123",
                Status = CustomerStatus.Lead,
                Notes = "x"
            };
            var added = DataStore.AddCustomer(c);
            Assert.IsTrue(added.Id > 0);
            Assert.AreEqual(countBefore + 1, DataStore.Customers.Count);
            var fetched = DataStore.GetCustomer(added.Id);
            Assert.IsNotNull(fetched);
            Assert.AreEqual("Test Customer", fetched.Name);
        }

        [TestMethod]
        public void GetCustomer_ReturnsNullForMissing()
        {
            Assert.IsNull(DataStore.GetCustomer(-9999));
        }

        [TestMethod]
        public void GetCustomer_PopulatesInteractionsSortedDesc()
        {
            var seeded = DataStore.Customers.FirstOrDefault(c => DataStore.GetCustomer(c.Id).Interactions.Count > 0);
            Assert.IsNotNull(seeded, "Expected at least one seeded customer with interactions.");
            var fetched = DataStore.GetCustomer(seeded.Id);
            var interactions = fetched.Interactions;
            Assert.IsTrue(interactions.Count > 0);
            for (int i = 1; i < interactions.Count; i++)
            {
                Assert.IsTrue(interactions[i - 1].InteractionDate >= interactions[i].InteractionDate,
                    "Interactions should be sorted by InteractionDate descending.");
            }
        }

        [TestMethod]
        public void UpdateCustomer_ChangesFields()
        {
            var c = new Customer
            {
                Name = "Update Me",
                Company = "Co",
                Email = "u@e.com",
                Phone = "123",
                Status = CustomerStatus.Lead,
                Notes = "x"
            };
            var added = DataStore.AddCustomer(c);
            added.Name = "Updated Name";
            Assert.IsTrue(DataStore.UpdateCustomer(added));
            var fetched = DataStore.GetCustomer(added.Id);
            Assert.IsNotNull(fetched);
            Assert.AreEqual("Updated Name", fetched.Name);
        }

        [TestMethod]
        public void UpdateCustomer_MissingId_ReturnsFalse()
        {
            Assert.IsFalse(DataStore.UpdateCustomer(new Customer { Id = -9999, Name = "X", Status = CustomerStatus.Lead }));
        }

        [TestMethod]
        public void DeleteCustomer_RemovesCustomerAndInteractions()
        {
            var c = new Customer
            {
                Name = "Delete Me",
                Company = "Co",
                Email = "d@e.com",
                Phone = "123",
                Status = CustomerStatus.Lead,
                Notes = "x"
            };
            var added = DataStore.AddCustomer(c);
            DataStore.AddInteraction(new Interaction
            {
                CustomerId = added.Id,
                Type = InteractionType.Call,
                Subject = "Test interaction",
                InteractionDate = DateTime.Today
            });
            int id = added.Id;
            DataStore.DeleteCustomer(id);
            Assert.IsNull(DataStore.GetCustomer(id));
            Assert.AreEqual(0, DataStore.Interactions.Count(i => i.CustomerId == id));
        }

        [TestMethod]
        public void AddInteraction_IncrementsAndIsListed()
        {
            var c = DataStore.Customers.First();
            int customerId = c.Id;
            int countBefore = DataStore.Interactions.Count(i => i.CustomerId == customerId);
            var added = DataStore.AddInteraction(new Interaction
            {
                CustomerId = customerId,
                Type = InteractionType.Email,
                Subject = "Test interaction listed",
                InteractionDate = DateTime.Today
            });
            Assert.IsTrue(added.Id > 0);
            Assert.AreEqual(countBefore + 1, DataStore.Interactions.Count(i => i.CustomerId == customerId));
            Assert.IsTrue(DataStore.Interactions.Any(i => i.Id == added.Id));
            Assert.IsTrue(DataStore.GetCustomer(customerId).Interactions.Any(i => i.Id == added.Id));
        }

        [TestMethod]
        public void DeleteInteraction_RemovesIt()
        {
            var c = DataStore.Customers.First();
            int customerId = c.Id;
            var added = DataStore.AddInteraction(new Interaction
            {
                CustomerId = customerId,
                Type = InteractionType.Note,
                Subject = "Delete this interaction",
                InteractionDate = DateTime.Today
            });
            int id = added.Id;
            Assert.IsTrue(DataStore.DeleteInteraction(id));
            Assert.IsFalse(DataStore.Interactions.Any(i => i.Id == id));
        }

        [TestMethod]
        public void FindUser_ReturnsSeededAdmin()
        {
            var u = DataStore.FindUser("admin");
            Assert.IsNotNull(u);
            Assert.AreEqual("admin", u.Username, true);
        }

        [TestMethod]
        public void FindUser_ReturnsNullForUnknown()
        {
            Assert.IsNull(DataStore.FindUser("nobody_xyz"));
        }

        [TestMethod]
        public void RecalculateLastInteractionDates_SetsLastInteractionDate()
        {
            var c = new Customer
            {
                Name = "Recalc Customer",
                Company = "Co",
                Email = "r@e.com",
                Phone = "123",
                Status = CustomerStatus.Lead,
                Notes = "x"
            };
            var added = DataStore.AddCustomer(c);
            var date = new DateTime(2024, 1, 15);
            DataStore.AddInteraction(new Interaction
            {
                CustomerId = added.Id,
                Type = InteractionType.Call,
                Subject = "Recalc interaction",
                InteractionDate = date
            });
            DataStore.RecalculateLastInteractionDates();
            var fetched = DataStore.Customers.First(x => x.Id == added.Id);
            Assert.AreEqual(date, fetched.LastInteractionDate);
        }
    }
}
