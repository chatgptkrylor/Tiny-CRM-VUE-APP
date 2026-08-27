using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinyCrm.Data.Repositories;

namespace TinyCrm.Tests
{
    // Integration tests for the user repository against the
    // seeded LocalDB database "TinyCrmTests" (see TestDatabaseSetup).
    [TestClass]
    public class UserRepositoryTests
    {
        private readonly UserRepository _users = new UserRepository();

        [TestMethod]
        public void FindUser_ReturnsSeededAdmin()
        {
            var u = _users.FindUser("admin");
            Assert.IsNotNull(u);
            Assert.AreEqual("admin", u.Username, true);
        }

        [TestMethod]
        public void FindUser_IsCaseInsensitive()
        {
            Assert.IsNotNull(_users.FindUser("ADMIN"));
        }

        [TestMethod]
        public void FindUser_ReturnsNullForUnknown()
        {
            Assert.IsNull(_users.FindUser("nobody_xyz"));
        }

        [TestMethod]
        public void FindUser_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(_users.FindUser(null));
            Assert.IsNull(_users.FindUser(""));
        }
    }
}
