using System;
using System.Security.Cryptography;
using System.Text;

namespace TinyCrm.Infrastructure
{
    public static class PasswordHasher
    {
        public static string Hash(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool Verify(string password, string hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;
            return string.Equals(Hash(password), hash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
