using System.Data.Entity;
using System.Linq;
using TinyCrm.Models;

namespace TinyCrm.Data.Repositories
{
    // EF6-based middle tier for users. Replaces the old static
    // in-memory DataStore. Short-lived DbContext per operation.
    public class UserRepository
    {
        // User name lookup is case-insensitive under the default
        // SQL Server collation, matching the old OrdinalIgnoreCase
        // in-memory comparison.
        public User FindUser(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            using (var ctx = DbContextFactory.Create())
            {
                return ctx.Users.AsNoTracking()
                    .FirstOrDefault(u => u.Username == username);
            }
        }
    }
}
