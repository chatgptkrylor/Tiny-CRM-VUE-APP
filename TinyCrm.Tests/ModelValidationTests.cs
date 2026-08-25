using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinyCrm.Models;

namespace TinyCrm.Tests
{
    [TestClass]
    public class ModelValidationTests
    {
        private bool Validate(object o)
        {
            var ctx = new ValidationContext(o);
            var results = new List<ValidationResult>();
            return Validator.TryValidateObject(o, ctx, results, true);
        }

        [TestMethod]
        public void Customer_WithRequiredFields_IsValid()
        {
            var c = new Customer
            {
                Name = "ACME",
                Email = "a@b.com",
                Phone = "123",
                Status = CustomerStatus.Customer
            };
            Assert.IsTrue(Validate(c));
        }

        [TestMethod]
        public void Customer_MissingName_IsInvalid()
        {
            var c = new Customer { Name = null, Status = CustomerStatus.Customer };
            Assert.IsFalse(Validate(c));
        }

        [TestMethod]
        public void Customer_NameTooShort_IsInvalid()
        {
            var c = new Customer { Name = "X", Status = CustomerStatus.Customer };
            Assert.IsFalse(Validate(c));
        }

        [TestMethod]
        public void Customer_BadEmail_IsInvalid()
        {
            var c = new Customer { Name = "ACME", Email = "notanemail", Status = CustomerStatus.Customer };
            Assert.IsFalse(Validate(c));
        }

        [TestMethod]
        public void Customer_NameTooLong_IsInvalid()
        {
            var c = new Customer { Name = new string('a', 101), Status = CustomerStatus.Customer };
            Assert.IsFalse(Validate(c));
        }

        [TestMethod]
        public void Interaction_WithRequiredFields_IsValid()
        {
            var i = new Interaction
            {
                Type = InteractionType.Call,
                Subject = "Hello there",
                InteractionDate = DateTime.Today,
                CustomerId = 1
            };
            Assert.IsTrue(Validate(i));
        }

        [TestMethod]
        public void Interaction_MissingSubject_IsInvalid()
        {
            var i = new Interaction { Type = InteractionType.Call, Subject = null, InteractionDate = DateTime.Today, CustomerId = 1 };
            Assert.IsFalse(Validate(i));
        }

        [TestMethod]
        public void Interaction_SubjectTooShort_IsInvalid()
        {
            var i = new Interaction { Type = InteractionType.Call, Subject = "ab", InteractionDate = DateTime.Today, CustomerId = 1 };
            Assert.IsFalse(Validate(i));
        }

        [TestMethod]
        public void Interaction_SubjectTooLong_IsInvalid()
        {
            var i = new Interaction { Type = InteractionType.Call, Subject = new string('a', 201), InteractionDate = DateTime.Today, CustomerId = 1 };
            Assert.IsFalse(Validate(i));
        }
    }
}
