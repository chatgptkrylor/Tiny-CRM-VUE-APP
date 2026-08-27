using System.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinyCrm.Data;
using TinyCrm.Data.Repositories;

namespace TinyCrm.Tests
{
    // Runs once for the whole test assembly: points the middle tier at a
    // dedicated LocalDB database (TinyCrmTests), recreates it, and seeds it.
    [TestClass]
    public class TestDatabaseSetup
    {
        public const string ConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Initial Catalog=TinyCrmTests;Integrated Security=True;Connect Timeout=30";

        private const string MasterConnectionString =
            "Server=(localdb)\\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Connect Timeout=30";

        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            using (var cn = new SqlConnection(MasterConnectionString))
            {
                cn.Open();
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'TinyCrmTests')
BEGIN
    ALTER DATABASE [TinyCrmTests] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [TinyCrmTests];
END
CREATE DATABASE [TinyCrmTests];";
                    cmd.ExecuteNonQuery();
                }
            }

            DbContextFactory.SetFactory(() => new TinyCrmEntities(ConnectionString));
            using (var db = DbContextFactory.Create())
            {
                DatabaseSeeder.Seed(db);
            }
        }

        [TestMethod]
        public void TestDatabase_IsSeeded()
        {
            Assert.IsTrue(new CustomerRepository().GetAll().Count >= 5);
        }
    }
}
