using Microsoft.VisualStudio.TestTools.UnitTesting;
using TinyCrm.Infrastructure;

namespace TinyCrm.Tests
{
    [TestClass]
    public class PasswordHasherTests
    {
        [TestMethod]
        public void Hash_ReturnsNonEmptyHex()
        {
            var h = PasswordHasher.Hash("admin123");
            Assert.IsFalse(string.IsNullOrEmpty(h));
            Assert.AreEqual(64, h.Length);
        }

        [TestMethod]
        public void Hash_IsDeterministic()
        {
            Assert.AreEqual(PasswordHasher.Hash("abc"), PasswordHasher.Hash("abc"));
        }

        [TestMethod]
        public void Hash_DiffersForDifferentInputs()
        {
            Assert.AreNotEqual(PasswordHasher.Hash("a"), PasswordHasher.Hash("b"));
        }

        [TestMethod]
        public void Verify_MatchesCorrectPassword()
        {
            Assert.IsTrue(PasswordHasher.Verify("admin123", PasswordHasher.Hash("admin123")));
        }

        [TestMethod]
        public void Verify_RejectsWrongPassword()
        {
            Assert.IsFalse(PasswordHasher.Verify("wrong", PasswordHasher.Hash("admin123")));
        }

        [TestMethod]
        public void Verify_RejectsEmptyHash()
        {
            Assert.IsFalse(PasswordHasher.Verify("x", ""));
        }

        [TestMethod]
        public void Hash_EmptyInput_ReturnsEmpty()
        {
            Assert.AreEqual("", PasswordHasher.Hash(""));
        }
    }
}
