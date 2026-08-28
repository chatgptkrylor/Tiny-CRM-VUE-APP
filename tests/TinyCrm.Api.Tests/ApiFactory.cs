using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Xunit;

namespace TinyCrm.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestConnection =
        @"Server=(localdb)\MSSQLLocalDB;Database=TinyCrmVueTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    // Dropped ONCE, here, because a single factory is shared by every test class
    // (see ApiCollection). Dropping from inside a test would race with other classes.
    public ApiFactory() => DropTestDatabase();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:TinyCrmVue", TestConnection);
    }

    // Drop the test database explicitly, by name, against master.
    // Deliberately NOT resolved through DI: a DbContext built from a temporary
    // service provider can still carry the PRODUCTION connection string, which
    // would drop TinyCrmVue. Never risk that.
    public static void DropTestDatabase()
    {
        var csb = new SqlConnectionStringBuilder(TestConnection);
        var dbName = csb.InitialCatalog;

        // Hard guard: refuse to drop anything that is not the test database.
        if (dbName != "TinyCrmVueTests")
            throw new InvalidOperationException($"Refusing to drop '{dbName}'. Expected 'TinyCrmVueTests'.");

        csb.InitialCatalog = "master";
        using var conn = new SqlConnection(csb.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            IF DB_ID('{dbName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{dbName}];
            END";
        cmd.ExecuteNonQuery();
    }
}

// One factory for the whole assembly: the database is created and seeded once.
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }
