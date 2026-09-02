using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Models;
using TinyCrm.Api.Services;

namespace TinyCrm.Api.Data;

public class TinyCrmDbContext : DbContext
{
    // Static reference set once at startup.  Using a static avoids the DI resolution
    // complexity of injecting a scoped service into a DbContext whose constructor is
    // called by EF Core's internal factory.  The field is null in test assemblies that
    // do not call SetElasticSearchService.
    private static IElasticSearchService? _esService;
    public static void SetElasticSearchService(IElasticSearchService service) => _esService = service;

    public TinyCrmDbContext(DbContextOptions<TinyCrmDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<User> Users => Set<User>();

    // Single override for all write paths — replaces patching five separate SaveChangesAsync
    // call-sites across CustomersController and InteractionsController.
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot tracked entities BEFORE the save so we know what to sync.
        var customerEntries = ChangeTracker.Entries<Customer>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (Entity: e.Entity, e.State))
            .ToList();

        var interactionEntries = ChangeTracker.Entries<Interaction>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (Entity: e.Entity, e.State))
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Sync to Elasticsearch if the service is available.
        if (_esService is not null)
        {
            foreach (var (entity, state) in customerEntries)
            {
                if (state == EntityState.Deleted)
                    await _esService.RemoveCustomerAsync(entity.Id);
                else
                    await _esService.IndexCustomerAsync(entity);
            }

            // When interactions change, re-index the parent customer so the
            // denormalised ES document (interactionSubjects / interactionNotes)
            // stays current.
            var parentIds = interactionEntries
                .Select(e => e.Entity.CustomerId)
                .Concat(customerEntries
                    .Where(e => e.State == EntityState.Deleted)
                    .Select(e => e.Entity.Id))
                .Distinct()
                .ToList();

            foreach (var id in parentIds)
                await _esService.IndexCustomerAsync(new Customer { Id = id });
        }

        return result;
    }

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
