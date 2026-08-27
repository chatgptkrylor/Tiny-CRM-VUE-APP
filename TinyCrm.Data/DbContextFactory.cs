using System;

namespace TinyCrm.Data
{
    // Central factory for creating short-lived DbContext instances.
    // The web application uses the default constructor, which resolves
    // the "TinyCrmEntities" connection string from configuration.
    // Tests override the factory to point at a dedicated test database.
    public static class DbContextFactory
    {
        private static Func<TinyCrmEntities> _factory = () => new TinyCrmEntities();

        public static TinyCrmEntities Create()
        {
            return _factory();
        }

        public static void SetFactory(Func<TinyCrmEntities> factory)
        {
            _factory = factory ?? (() => new TinyCrmEntities());
        }
    }
}
