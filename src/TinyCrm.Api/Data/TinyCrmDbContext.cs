using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Data;

public class TinyCrmDbContext : DbContext
{
    public TinyCrmDbContext(DbContextOptions<TinyCrmDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Company).HasMaxLength(150);
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Notes).HasMaxLength(500);
            // Cascade is explicit: this is the exact defect that broke the EF6 migration,
            // where the conceptual model omitted it and deletes failed on the non-nullable FK.
            e.HasMany(x => x.Interactions)
             .WithOne(i => i.Customer!)
             .HasForeignKey(i => i.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Interaction>(e =>
        {
            e.Property(x => x.Subject).IsRequired().HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Username).IsRequired().HasMaxLength(50);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(256);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Username).IsUnique();
        });
    }
}
