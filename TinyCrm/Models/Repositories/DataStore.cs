using System;
using System.Collections.Generic;
using System.Linq;
using TinyCrm.Infrastructure;

namespace TinyCrm.Models.Repositories
{
    public static class DataStore
    {
        private static readonly object Lock = new object();
        private static List<Customer> _customers;
        private static List<Interaction> _interactions;
        private static List<User> _users;
        private static int _customerSeq;
        private static int _interactionSeq;
        private static bool _seeded;

        public static IList<Customer> Customers
        {
            get { EnsureSeeded(); lock (Lock) { return _customers.ToList(); } }
        }

        public static IList<Interaction> Interactions
        {
            get { EnsureSeeded(); lock (Lock) { return _interactions.ToList(); } }
        }

        public static IList<User> Users
        {
            get { EnsureSeeded(); lock (Lock) { return _users.ToList(); } }
        }

        public static void Seed()
        {
            EnsureSeeded();
        }

        private static void EnsureSeeded()
        {
            lock (Lock)
            {
                if (_seeded) return;
                _customers = new List<Customer>();
                _interactions = new List<Interaction>();
                _users = new List<User>();
                _customerSeq = 0;
                _interactionSeq = 0;

                _users.Add(new User { Id = 1, Username = "admin", PasswordHash = PasswordHasher.Hash("admin123"), DisplayName = "Administrator" });
                _users.Add(new User { Id = 2, Username = "demo", PasswordHash = PasswordHasher.Hash("demo123"), DisplayName = "Demo User" });

                SeedCustomers();
                _seeded = true;
            }
        }

        private static void SeedCustomers()
        {
            var c1 = AddCustomerInternal("Acme Corp", "John Smith", "john@acme.example", "+1 555 100 2000", CustomerStatus.Customer, "Early adopter, renewal due next quarter.");
            var c2 = AddCustomerInternal("Globex Inc", "Alice Cooper", "alice@globex.example", "+1 555 100 2001", CustomerStatus.Lead, "Prospect from trade show.");
            var c3 = AddCustomerInternal("Initech", "Peter Gibbons", "peter@initech.example", "+1 555 100 2002", CustomerStatus.Customer, "On annual plan.");
            var c4 = AddCustomerInternal("Umbrella Ltd", "Alice Abernathy", "alice@umbrella.example", "+1 555 100 2003", CustomerStatus.Contact, "In evaluation, win-back in progress.");
            var c5 = AddCustomerInternal("Soylent Co", "Robert Paulson", "bob@soylent.example", "+1 555 100 2004", CustomerStatus.Lead, "");

            AddInteractionInternal(c1.Id, InteractionType.Call, "Onboarding call", "Walked through main features.", DateTime.Now.AddDays(-3));
            AddInteractionInternal(c1.Id, InteractionType.Email, "Pricing follow-up", "Sent revised quote.", DateTime.Now.AddDays(-2));
            AddInteractionInternal(c2.Id, InteractionType.Meeting, "Discovery meeting", "Requirements gathering.", DateTime.Now.AddDays(-1));
            AddInteractionInternal(c3.Id, InteractionType.Email, "Support ticket #42", "Reset credentials.", DateTime.Now.AddDays(-5));
            AddInteractionInternal(c4.Id, InteractionType.Call, "Win-back call", "Offered discount to return.", DateTime.Now.AddDays(-40));
            AddInteractionInternal(c5.Id, InteractionType.Note, "Imported from spreadsheet", "No contact yet.", DateTime.Now.AddDays(-10));

            RecalculateLastInteractionDates();
        }

        public static Customer AddCustomer(Customer c)
        {
            EnsureSeeded();
            lock (Lock)
            {
                return AddCustomerInternal(c.Name, c.Company, c.Email, c.Phone, c.Status, c.Notes);
            }
        }

        private static Customer AddCustomerInternal(string name, string company, string email, string phone, CustomerStatus status, string notes)
        {
            var c = new Customer
            {
                Id = ++_customerSeq,
                Name = name,
                Company = company,
                Email = email,
                Phone = phone,
                Status = status,
                Notes = notes,
                CreatedAt = DateTime.Now
            };
            _customers.Add(c);
            return c;
        }

        public static Customer GetCustomer(int id)
        {
            EnsureSeeded();
            lock (Lock)
            {
                var c = _customers.FirstOrDefault(x => x.Id == id);
                if (c == null) return null;
                var copy = new Customer
                {
                    Id = c.Id,
                    Name = c.Name,
                    Company = c.Company,
                    Email = c.Email,
                    Phone = c.Phone,
                    Status = c.Status,
                    Notes = c.Notes,
                    CreatedAt = c.CreatedAt,
                    LastInteractionDate = c.LastInteractionDate
                };
                copy.Interactions = _interactions.Where(i => i.CustomerId == id).OrderByDescending(i => i.InteractionDate).ToList();
                foreach (var i in copy.Interactions)
                {
                    i.CustomerName = c.Name;
                }
                return copy;
            }
        }

        public static bool UpdateCustomer(Customer updated)
        {
            EnsureSeeded();
            lock (Lock)
            {
                var c = _customers.FirstOrDefault(x => x.Id == updated.Id);
                if (c == null) return false;
                c.Name = updated.Name;
                c.Company = updated.Company;
                c.Email = updated.Email;
                c.Phone = updated.Phone;
                c.Status = updated.Status;
                c.Notes = updated.Notes;
                return true;
            }
        }

        public static bool DeleteCustomer(int id)
        {
            EnsureSeeded();
            lock (Lock)
            {
                var removed = _customers.RemoveAll(x => x.Id == id) > 0;
                _interactions.RemoveAll(i => i.CustomerId == id);
                return removed;
            }
        }

        public static Interaction AddInteraction(Interaction i)
        {
            EnsureSeeded();
            lock (Lock)
            {
                return AddInteractionInternal(i.CustomerId, i.Type, i.Subject, i.Notes, i.InteractionDate);
            }
        }

        private static Interaction AddInteractionInternal(int customerId, InteractionType type, string subject, string notes, DateTime date)
        {
            var i = new Interaction
            {
                Id = ++_interactionSeq,
                CustomerId = customerId,
                Type = type,
                Subject = subject,
                Notes = notes,
                InteractionDate = date,
                CreatedAt = DateTime.Now,
                CustomerName = _customers.FirstOrDefault(c => c.Id == customerId)?.Name
            };
            _interactions.Add(i);
            return i;
        }

        public static bool DeleteInteraction(int id)
        {
            EnsureSeeded();
            lock (Lock)
            {
                return _interactions.RemoveAll(x => x.Id == id) > 0;
            }
        }

        public static void RecalculateLastInteractionDates()
        {
            foreach (var c in _customers)
            {
                var last = _interactions.Where(i => i.CustomerId == c.Id).OrderByDescending(i => i.InteractionDate).FirstOrDefault();
                c.LastInteractionDate = last?.InteractionDate;
            }
        }

        public static User FindUser(string username)
        {
            EnsureSeeded();
            lock (Lock) { return _users.FirstOrDefault(u => string.Equals(u.Username, username, System.StringComparison.OrdinalIgnoreCase)); }
        }
    }
}