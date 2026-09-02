using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace TinyCrm.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    // Overridable so a CI box can point somewhere else; the database name must still
    // be the test one, which DropTestDatabase below enforces.
    public static readonly string TestConnection =
        Environment.GetEnvironmentVariable("TINYCRM_TEST_CONNECTION")
        ?? "Host=127.0.0.1;Port=5432;Database=tinycrmvuetests;Username=tinycrm;Password=TinyCrm@Local2026";

    // Dropped ONCE, here, because a single factory is shared by every test class
    // (see ApiCollection). Dropping from inside a test would race with other classes.
    public ApiFactory() => DropTestDatabase();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:TinyCrmVue", TestConnection);
    }

    // Drop the test database explicitly, by name, from the maintenance database.
    // Deliberately NOT resolved through DI: a DbContext built from a temporary
    // service provider can still carry the PRODUCTION connection string, which
    // would drop the real database. Never risk that.
    public static void DropTestDatabase()
    {
        var csb = new NpgsqlConnectionStringBuilder(TestConnection);
        var dbName = csb.Database;

        // Hard guard: refuse to drop anything that is not the test database.
        // Case-insensitive because Postgres folds unquoted identifiers to lower case.
        if (!string.Equals(dbName, "tinycrmvuetests", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to drop '{dbName}'. Expected 'tinycrmvuetests'.");

        csb.Database = "postgres";
        using var conn = new NpgsqlConnection(csb.ConnectionString);
        conn.Open();

        // Postgres refuses to drop a database that still has sessions attached, and the
        // previous test run's connection pool may still hold some. Evict them first.
        using (var kill = conn.CreateCommand())
        {
            kill.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                "WHERE datname = @db AND pid <> pg_backend_pid()";
            kill.Parameters.AddWithValue("db", dbName!);
            kill.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{dbName}\"";
        cmd.ExecuteNonQuery();
    }
}

// One factory for the whole assembly: the database is created and seeded once.
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }
