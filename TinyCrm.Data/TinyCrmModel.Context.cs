using System.Data.Entity;
using TinyCrm.Models;

namespace TinyCrm.Data
{
    // DbContext for the TinyCrmModel.edmx model (EF 6, model-first).
    // The connection string "TinyCrmEntities" is resolved from the
    // application configuration file (Web.config / app.config).
    // Entity classes live in the TinyCrm.Models namespace, matching
    // the conceptual model namespace in the EDMX.
    public partial class TinyCrmEntities : DbContext
    {
        public TinyCrmEntities()
            : base("name=TinyCrmEntities")
        {
        }

        public TinyCrmEntities(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }

        public virtual DbSet<Customer> Customers { get; set; }

        public virtual DbSet<Interaction> Interactions { get; set; }

        public virtual DbSet<User> Users { get; set; }
    }
}
