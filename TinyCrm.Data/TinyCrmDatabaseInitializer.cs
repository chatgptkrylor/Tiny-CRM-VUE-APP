using System.Data.Entity;

namespace TinyCrm.Data
{
    // Creates the database from the EDMX store model if it does not
    // exist yet and seeds it with the initial demo data (the same
    // seed data the old in-memory DataStore used to provide).
    public class TinyCrmDatabaseInitializer : CreateDatabaseIfNotExists<TinyCrmEntities>
    {
        protected override void Seed(TinyCrmEntities context)
        {
            DatabaseSeeder.Seed(context);
        }
    }
}
