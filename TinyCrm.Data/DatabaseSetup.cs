using System.Data.Entity;

namespace TinyCrm.Data
{
    // Called once at application startup (Global.asax) to register the
    // database initializer and make sure the database exists and is
    // seeded before the first request is served.
    public static class DatabaseSetup
    {
        private static readonly object Lock = new object();
        private static bool _initialized;

        public static void Initialize()
        {
            lock (Lock)
            {
                if (_initialized) return;
                Database.SetInitializer(new TinyCrmDatabaseInitializer());
                using (var context = DbContextFactory.Create())
                {
                    context.Database.Initialize(false);
                }
                _initialized = true;
            }
        }
    }
}
