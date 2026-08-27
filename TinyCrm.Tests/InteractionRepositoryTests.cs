using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinyCrm.Data.Repositories;
using TinyCrm.Models;

namespace TinyCrm.Tests
{
    // Integration tests for the interaction repository against the
    // seeded LocalDB database "TinyCrmTests" (see TestDatabaseSetup).
    [TestClass]
    public class InteractionRepositoryTests
    {
        private readonly CustomerRepository _customers = new CustomerRepository();
        private readonly InteractionRepository _interactions = new InteractionRepository();

        [TestMethod]
        public void GetAll_ReturnsSeededInteractions()
        {
            Assert.IsTrue(_interactions.GetAll().Count >= 6);
        }

        [TestMethod]
        public void AddInteraction_IncrementsAndIsListed()
        {
            int customerId = _customers.GetAll().First().Id;
            int countBefore = _interactions.GetAll().Count(i => i.CustomerId == customerId);

            var added = _interactions.AddInteraction(new Interaction
            {
                CustomerId = customerId,
                Type = InteractionType.Email,
                Subject = "Test interaction listed",
                InteractionDate = DateTime.Today
            });

            Assert.IsNotNull(added);
            Assert.IsTrue(added.Id > 0);
            Assert.AreEqual(countBefore + 1, _interactions.GetAll().Count(i => i.CustomerId == customerId));
            Assert.IsTrue(_interactions.GetAll().Any(i => i.Id == added.Id));
            Assert.IsTrue(_customers.GetCustomer(customerId).Interactions.Any(i => i.Id == added.Id));
        }

        [TestMethod]
        public void AddInteraction_UnknownCustomer_ReturnsNull()
        {
            Assert.IsNull(_interactions.AddInteraction(new Interaction
            {
                CustomerId = -9999,
                Type = InteractionType.Call,
                Subject = "No such customer",
                InteractionDate = DateTime.Today
            }));
        }

        [TestMethod]
        public void DeleteInteraction_RemovesIt()
        {
            int customerId = _customers.GetAll().First().Id;
            var added = _interactions.AddInteraction(new Interaction
            {
                CustomerId = customerId,
                Type = InteractionType.Note,
                Subject = "Delete this interaction",
                InteractionDate = DateTime.Today
            });
            Assert.IsNotNull(added);

            Assert.IsTrue(_interactions.DeleteInteraction(added.Id));
            Assert.IsFalse(_interactions.GetAll().Any(i => i.Id == added.Id));
        }

        [TestMethod]
        public void DeleteInteraction_MissingId_ReturnsFalse()
        {
            Assert.IsFalse(_interactions.DeleteInteraction(-9999));
        }

        [TestMethod]
        public void RecalculateLastInteractionDates_SetsLastInteractionDate()
        {
            var added = _customers.AddCustomer(new Customer
            {
                Name = "Recalc Customer",
                Company = "Co",
                Email = "r@e.com",
                Phone = "123",
                Status = CustomerStatus.Lead,
                Notes = "x"
            });
            var date = new DateTime(2024, 1, 15);
            _interactions.AddInteraction(new Interaction
            {
                CustomerId = added.Id,
                Type = InteractionType.Call,
                Subject = "Recalc interaction",
                InteractionDate = date
            });
            _interactions.RecalculateLastInteractionDates();

            var fetched = _customers.GetAll().First(x => x.Id == added.Id);
            Assert.AreEqual(date, fetched.LastInteractionDate);
        }
    }
}
