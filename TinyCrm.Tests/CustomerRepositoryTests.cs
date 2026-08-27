using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinyCrm.Data.Repositories;
using TinyCrm.Models;

namespace TinyCrm.Tests
{
    // Integration tests for the EF6 middle tier. They run against the
    // seeded LocalDB database "TinyCrmTests" (see TestDatabaseSetup).
    [TestClass]
    public class CustomerRepositoryTests
    {
        private readonly CustomerRepository _customers = new CustomerRepository();
        private readonly InteractionRepository _interactions = new InteractionRepository();

        [TestMethod]
        public void GetAll_ReturnsSeededCustomers()
        {
            Assert.IsTrue(_customers.GetAll().Count >= 5);
        }

        [TestMethod]
        public void AddCustomer_IncrementsCountAndIsRetrievable()
        {
            int countBefore = _customers.GetAll().Count;
            var added = _customers.AddCustomer(new Customer
            {
                Name = "Test Customer",
                Company = "Co",
                Email = "t@e.com",
                Phone = "123",
                Status = CustomerStatus.Lead,
                Notes = "x"
            });
            Assert.IsTrue(added.Id > 0);
            Assert.AreEqual(countBefore + 1, _customers.GetAll().Count);

            var fetched = _customers.GetCustomer(added.Id);
            Assert.IsNotNull(fetched);
            Assert.AreEqual("Test Customer", fetched.Name);
        }

        [TestMethod]
        public void GetCustomer_ReturnsNullForMissing()
        {
            Assert.IsNull(_customers.GetCustomer(-9999));
        }

        [TestMethod]
        public void GetCustomer_PopulatesInteractionsSortedDesc()
        {
            var seeded = _customers.GetAll()
                .FirstOrDefault(c => _customers.GetCustomer(c.Id).Interactions.Count > 0);
            Assert.IsNotNull(seeded, "Expected at least one seeded customer with interactions.");

            var interactions = _customers.GetCustomer(seeded.Id).Interactions;
            Assert.IsTrue(interactions.Count > 0);
            for (int i = 1; i < interactions.Count; i++)
            {
                Assert.IsTrue(interactions[i - 1].InteractionDate >= interactions[i].InteractionDate,
                    "Interactions should be sorted by InteractionDate descending.");
            }
        }

        [TestMethod]
        public void GetCustomer_SetsInteractionCustomerName()
        {
            var seeded = _customers.GetAll()
                .FirstOrDefault(c => _customers.GetCustomer(c.Id).Interactions.Count > 0);
            Assert.IsNotNull(seeded);

            var fetched = _customers.GetCustomer(seeded.Id);
            Assert.IsTrue(fetched.Interactions.All(i => i.CustomerName == fetched.Name));
        }

        [TestMethod]
        public void UpdateCustomer_ChangesFields()
        {
            var added = _customers.AddCustomer(new Customer
            {
                Name = "Update Me",
                Company = "Co",
                Email = "u@e.com",
                Phone = "123",
                Status = CustomerStatus.Lead,
                Notes = "x"
            });
            added.Name = "Updated Name";
            Assert.IsTrue(_customers.UpdateCustomer(added));

            var fetched = _customers.GetCustomer(added.Id);
            Assert.IsNotNull(fetched);
            Assert.AreEqual("Updated Name", fetched.Name);
        }

        [TestMethod]
        public void UpdateCustomer_MissingId_ReturnsFalse()
        {
            Assert.IsFalse(_customers.UpdateCustomer(
                new Customer { Id = -9999, Name = "X", Status = CustomerStatus.Lead }));
        }

        [TestMethod]
        public void DeleteCustomer_RemovesCustomerAndInteractions()
        {
            var added = _customers.AddCustomer(new Customer
            {
                Name = "Delete Me",
                Company = "Co",
                Email = "d@e.com",
                Phone = "123",
                Status = CustomerStatus.Lead,
                Notes = "x"
            });
            _interactions.AddInteraction(new Interaction
            {
                CustomerId = added.Id,
                Type = InteractionType.Call,
                Subject = "Test interaction",
                InteractionDate = DateTime.Today
            });

            int id = added.Id;
            _customers.DeleteCustomer(id);
            Assert.IsNull(_customers.GetCustomer(id));
            Assert.IsFalse(_interactions.GetAll().Any(i => i.CustomerId == id));
        }

        [TestMethod]
        public void DeleteCustomer_MissingId_ReturnsFalse()
        {
            Assert.IsFalse(_customers.DeleteCustomer(-9999));
        }
    }
}
