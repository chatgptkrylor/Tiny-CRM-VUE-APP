# Tiny CRM — .NET 10 + Vue/Vite Port: Phase 0–1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the ported stack and drive one feature — login + customer list — end-to-end through every layer, proving the integrations that actually break ports.

**Architecture:** ASP.NET Core 10 Web API (controllers + EF Core 10 over SQL Server LocalDB) serving a Vue 3 SPA. In development Vite (`:5173`) proxies `/api` to Kestrel (`:5174`); in production the built SPA is served from the API's `wwwroot`, giving a single origin so cookie auth needs no CORS and no token in `localStorage`.

**Tech Stack:** .NET 10.0.400, ASP.NET Core 10, EF Core 10 (SqlServer), xUnit + `WebApplicationFactory`, Vue 3 (`<script setup>` + TypeScript), Vite, vue-router, Playwright.

**Spec:** `docs/superpowers/specs/2026-08-27-dotnet10-vue-port-design.md`

## Global Constraints

- **Work ONLY inside `C:\Users\Administrator\Desktop\Tiny-CRM-VUE-APP`.** `C:\Users\Administrator\Desktop\Tiny-CRM-App` is read-only reference — never edit, rebuild, restart, or commit to it.
- **Never run the Playwright suite against `Tiny-CRM-App`** (`:54322`). It is destructive and would mutate that app's database.
- Application database: **`TinyCrmVue`**. Test database: **`TinyCrmVueTests`**. Never `TinyCrm` or `TinyCrmTests`.
- Ports: Vite **5173**, Kestrel **5174**. Never 54322.
- SDK pinned to **10.0.400** via `global.json` (D5).
- Build with `dotnet` — the VM's VS BuildTools MSBuild has a 0-byte Roslyn compiler and fails.
- Enums serialise as **strings** (`JsonStringEnumConverter`).
- `InteractionDate` stays **`DateTime`**; every interaction ordering is `(InteractionDate DESC, Id DESC)` (D2).
- Cookie: `HttpOnly`, `SameSite=Lax`, `SecurePolicy = SameAsRequest` (D4).
- Unauthenticated API calls return **401 JSON**, never an HTML redirect.
- Git commits are authored `chatgptkrylor <chatgptkrylor@gmail.com>`; **no AI/Claude attribution** in messages or files.

---

## File Structure

| File | Responsibility |
|---|---|
| `global.json` | Pin SDK 10.0.400 |
| `TinyCrmVue.sln` | Solution (separate from the MVC `TinyCrm.sln` already in this folder) |
| `src/TinyCrm.Api/Program.cs` | DI, auth, JSON, middleware, SPA fallback |
| `src/TinyCrm.Api/Models/{Customer,Interaction,User,Enums}.cs` | Entities |
| `src/TinyCrm.Api/Data/TinyCrmDbContext.cs` | DbContext + model configuration |
| `src/TinyCrm.Api/Data/DatabaseSeeder.cs` | Seed users/customers/interactions (PBKDF2) |
| `src/TinyCrm.Api/Dtos/*.cs` | Request/response contracts |
| `src/TinyCrm.Api/Controllers/AuthController.cs` | login / logout / me |
| `src/TinyCrm.Api/Controllers/CustomersController.cs` | list (Phase 1), CRUD (Phase 2) |
| `src/tiny-crm-web/vite.config.ts` | Dev proxy + build output to API wwwroot |
| `src/tiny-crm-web/src/api/client.ts` | fetch wrapper, 401 handling |
| `src/tiny-crm-web/src/auth.ts` | Reactive auth state (no Pinia) |
| `src/tiny-crm-web/src/router.ts` | Routes + guard |
| `src/tiny-crm-web/src/views/{LoginView,CustomersView}.vue` | Phase 1 views |
| `tests/TinyCrm.Api.Tests/*` | xUnit integration tests |
| `tests/e2e/PARITY-CHANGES.md` | Semantic-change log (spec §9.2) |

---

# PHASE 0 — Skeleton

### Task 1: Solution, SDK pin, API project skeleton

**Files:**
- Create: `global.json`, `TinyCrmVue.sln`, `src/TinyCrm.Api/`, `.gitignore` (append)

**Interfaces:**
- Consumes: nothing
- Produces: buildable `TinyCrm.Api` assembly; solution path `TinyCrmVue.sln`

- [ ] **Step 1: Confirm you are in the correct folder**

```powershell
cd C:\Users\Administrator\Desktop\Tiny-CRM-VUE-APP
# MUST print ...\Tiny-CRM-VUE-APP  — if it prints Tiny-CRM-App, STOP.
Get-Location
```

- [ ] **Step 2: Pin the SDK**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 3: Verify the pin resolves**

Run: `dotnet --version`
Expected: `10.0.400`

- [ ] **Step 4: Create solution and API project**

```powershell
dotnet new sln -n TinyCrmVue
dotnet new webapi -n TinyCrm.Api -o src\TinyCrm.Api --use-controllers
dotnet sln TinyCrmVue.sln add src\TinyCrm.Api\TinyCrm.Api.csproj
```

- [ ] **Step 5: Add EF Core and identity-hashing packages**

```powershell
cd src\TinyCrm.Api
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.Extensions.Identity.Core
cd ..\..
```

- [ ] **Step 6: Install the EF Core CLI**

```powershell
dotnet tool install --global dotnet-ef
dotnet ef --version
```
Expected: prints an EF Core 10.x version.

- [ ] **Step 7: Ignore build output (D6)**

Append to `.gitignore`:

```gitignore
# .NET 10 port
src/TinyCrm.Api/bin/
src/TinyCrm.Api/obj/
src/TinyCrm.Api/wwwroot/
tests/TinyCrm.Api.Tests/bin/
tests/TinyCrm.Api.Tests/obj/
src/tiny-crm-web/node_modules/
src/tiny-crm-web/dist/
```

- [ ] **Step 8: Verify it builds**

Run: `dotnet build TinyCrmVue.sln`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 9: Commit**

```powershell
git add global.json TinyCrmVue.sln src/TinyCrm.Api .gitignore
git -c user.name="chatgptkrylor" -c user.email="chatgptkrylor@gmail.com" commit -m "Add .NET 10 API skeleton and pin SDK"
```

---

### Task 2: Vue + Vite project with dev proxy

**Files:**
- Create: `src/tiny-crm-web/` (scaffold), `src/tiny-crm-web/vite.config.ts`

**Interfaces:**
- Consumes: Kestrel on `:5174` (Task 3 configures it)
- Produces: dev server on `:5173` proxying `/api`; `npm run build` → `../TinyCrm.Api/wwwroot`

- [ ] **Step 1: Scaffold the Vue app**

```powershell
cd src
npm create vite@latest tiny-crm-web -- --template vue-ts
cd tiny-crm-web
npm install
npm install vue-router@4
cd ..\..
```

- [ ] **Step 2: Configure proxy and build output**

Replace `src/tiny-crm-web/vite.config.ts`:

```ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5174',
        changeOrigin: false,
      },
    },
  },
  build: {
    outDir: '../TinyCrm.Api/wwwroot',
    emptyOutDir: true,
  },
})
```

`changeOrigin: false` keeps the Host header as `localhost`, so the auth cookie set by Kestrel is accepted by the browser (cookies ignore port).

- [ ] **Step 3: Verify the production build lands in wwwroot**

```powershell
cd src\tiny-crm-web
npm run build
cd ..\..
Test-Path src\TinyCrm.Api\wwwroot\index.html
```
Expected: `True`

- [ ] **Step 4: Commit**

```powershell
git add src/tiny-crm-web
git -c user.name="chatgptkrylor" -c user.email="chatgptkrylor@gmail.com" commit -m "Add Vue 3 + Vite frontend with dev proxy"
```

---

# PHASE 1 — Vertical slice: login + customer list

### Task 3: Entities, DbContext, migration

**Files:**
- Create: `src/TinyCrm.Api/Models/Enums.cs`, `Models/Customer.cs`, `Models/Interaction.cs`, `Models/User.cs`, `Data/TinyCrmDbContext.cs`
- Modify: `src/TinyCrm.Api/Program.cs`, `appsettings.Development.json`

**Interfaces:**
- Consumes: nothing
- Produces: `TinyCrmDbContext` with `DbSet<Customer> Customers`, `DbSet<Interaction> Interactions`, `DbSet<User> Users`; enums `CustomerStatus { Lead=0, Contact=1, Customer=2 }`, `InteractionType { Call=0, Email=1, Meeting=2, Note=3 }`

- [ ] **Step 1: Create the enums**

`src/TinyCrm.Api/Models/Enums.cs`:

```csharp
namespace TinyCrm.Api.Models;

public enum CustomerStatus { Lead = 0, Contact = 1, Customer = 2 }

public enum InteractionType { Call = 0, Email = 1, Meeting = 2, Note = 3 }
```

- [ ] **Step 2: Create the entities**

`src/TinyCrm.Api/Models/Customer.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace TinyCrm.Api.Models;

public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Company { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(50)]
    [RegularExpression(@"^[0-9+\-\s()]{0,50}$", ErrorMessage = "Phone may contain digits, spaces and + - ( ) only.")]
    public string? Phone { get; set; }

    public CustomerStatus Status { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastInteractionDate { get; set; }

    public List<Interaction> Interactions { get; set; } = new();
}
```

`src/TinyCrm.Api/Models/Interaction.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace TinyCrm.Api.Models;

public class Interaction
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public InteractionType Type { get; set; }

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Subject must be between 3 and 200 characters.")]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime InteractionDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`src/TinyCrm.Api/Models/User.cs`:

```csharp
namespace TinyCrm.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Create the DbContext**

`src/TinyCrm.Api/Data/TinyCrmDbContext.cs`:

```csharp
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
```

- [ ] **Step 4: Add the connection string**

`src/TinyCrm.Api/appsettings.Development.json` — add the `ConnectionStrings` block:

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "ConnectionStrings": {
    "TinyCrmVue": "Server=(localdb)\\MSSQLLocalDB;Database=TinyCrmVue;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

- [ ] **Step 5: Register DbContext, JSON enum handling, and Kestrel port**

Replace `src/TinyCrm.Api/Program.cs` with:

```csharp
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5174");

builder.Services.AddDbContext<TinyCrmDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("TinyCrmVue")));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the app in tests.
public partial class Program { }
```

- [ ] **Step 6: Create the migration**

```powershell
cd src\TinyCrm.Api
dotnet ef migrations add InitialCreate
cd ..\..
```
Expected: `Migrations/` folder created with `*_InitialCreate.cs`.

- [ ] **Step 7: Apply it and verify the database**

```powershell
sqllocaldb start MSSQLLocalDB
cd src\TinyCrm.Api
dotnet ef database update
cd ..\..
```

Verify the correct database was created — and that `TinyCrm` was untouched:

```powershell
$c = New-Object System.Data.SqlClient.SqlConnection "Server=(localdb)\MSSQLLocalDB;Integrated Security=True"
$c.Open()
$cmd = $c.CreateCommand()
$cmd.CommandText = "SELECT name FROM sys.databases WHERE name LIKE 'TinyCrm%' ORDER BY name"
$r = $cmd.ExecuteReader(); while ($r.Read()) { $r[0] }; $c.Close()
```
Expected: includes `TinyCrmVue`. `TinyCrm` may be listed but must be untouched.

- [ ] **Step 8: Commit**

```powershell
git add src/TinyCrm.Api
git -c user.name="chatgptkrylor" -c user.email="chatgptkrylor@gmail.com" commit -m "Add EF Core model, DbContext and initial migration"
```

---

### Task 4: Seeder with PBKDF2 hashing + test harness

**Files:**
- Create: `src/TinyCrm.Api/Data/DatabaseSeeder.cs`, `tests/TinyCrm.Api.Tests/TinyCrm.Api.Tests.csproj`, `tests/TinyCrm.Api.Tests/ApiFactory.cs`, `tests/TinyCrm.Api.Tests/SeedTests.cs`
- Modify: `src/TinyCrm.Api/Program.cs`

**Interfaces:**
- Consumes: `TinyCrmDbContext` (Task 3)
- Produces: `DatabaseSeeder.Seed(TinyCrmDbContext, IPasswordHasher<User>)`; `ApiFactory : WebApplicationFactory<Program>` pointing at `TinyCrmVueTests`

- [ ] **Step 1: Write the seeder**

`src/TinyCrm.Api/Data/DatabaseSeeder.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Data;

public static class DatabaseSeeder
{
    public static void Seed(TinyCrmDbContext db, IPasswordHasher<User> hasher)
    {
        if (db.Users.Any() || db.Customers.Any()) return;

        var admin = new User { Username = "admin", DisplayName = "Administrator" };
        admin.PasswordHash = hasher.HashPassword(admin, "admin123");
        var demo = new User { Username = "demo", DisplayName = "Demo User" };
        demo.PasswordHash = hasher.HashPassword(demo, "demo123");
        db.Users.AddRange(admin, demo);

        var now = DateTime.Now;
        var customers = new List<Customer>
        {
            new() { Name = "Acme Corp",    Company = "John Smith",      Email = "john@acme.example",   Phone = "+1 555 100 2000", Status = CustomerStatus.Customer, Notes = "Early adopter, renewal due next quarter.", CreatedAt = now },
            new() { Name = "Globex Inc",   Company = "Alice Cooper",    Email = "alice@globex.example",Phone = "+1 555 100 2001", Status = CustomerStatus.Lead,     Notes = "Prospect from trade show.",               CreatedAt = now },
            new() { Name = "Initech",      Company = "Peter Gibbons",   Email = "peter@initech.example",Phone = "+1 555 100 2002",Status = CustomerStatus.Customer, Notes = "On annual plan.",                         CreatedAt = now },
            new() { Name = "Umbrella Ltd", Company = "Alice Abernathy", Email = "alice@umbrella.example",Phone = "+1 555 100 2003",Status = CustomerStatus.Contact,  Notes = "In evaluation, win-back in progress.",    CreatedAt = now },
            new() { Name = "Soylent Co",   Company = "Robert Paulson",  Email = "bob@soylent.example", Phone = "+1 555 100 2004", Status = CustomerStatus.Lead,     Notes = "",                                        CreatedAt = now },
        };
        db.Customers.AddRange(customers);
        db.SaveChanges();

        db.Interactions.AddRange(
            new Interaction { CustomerId = customers[0].Id, Type = InteractionType.Call,    Subject = "Onboarding call",           Notes = "Walked through main features.", InteractionDate = now.AddDays(-3),  CreatedAt = now },
            new Interaction { CustomerId = customers[0].Id, Type = InteractionType.Email,   Subject = "Pricing follow-up",         Notes = "Sent revised quote.",           InteractionDate = now.AddDays(-2),  CreatedAt = now },
            new Interaction { CustomerId = customers[1].Id, Type = InteractionType.Meeting, Subject = "Discovery meeting",         Notes = "Requirements gathering.",       InteractionDate = now.AddDays(-1),  CreatedAt = now },
            new Interaction { CustomerId = customers[2].Id, Type = InteractionType.Email,   Subject = "Support ticket #42",        Notes = "Reset credentials.",            InteractionDate = now.AddDays(-5),  CreatedAt = now },
            new Interaction { CustomerId = customers[3].Id, Type = InteractionType.Call,    Subject = "Win-back call",             Notes = "Offered discount to return.",   InteractionDate = now.AddDays(-40), CreatedAt = now },
            new Interaction { CustomerId = customers[4].Id, Type = InteractionType.Note,    Subject = "Imported from spreadsheet", Notes = "No contact yet.",               InteractionDate = now.AddDays(-10), CreatedAt = now });
        db.SaveChanges();

        RecalculateLastInteractionDates(db);
        db.SaveChanges();
    }

    public static void RecalculateLastInteractionDates(TinyCrmDbContext db)
    {
        var last = db.Interactions
            .GroupBy(i => i.CustomerId)
            .Select(g => new { CustomerId = g.Key, Max = g.Max(i => i.InteractionDate) })
            .ToDictionary(x => x.CustomerId, x => x.Max);

        foreach (var c in db.Customers)
            c.LastInteractionDate = last.TryGetValue(c.Id, out var d) ? d : null;
    }
}
```

- [ ] **Step 2: Register the hasher and run migrate+seed at startup**

In `src/TinyCrm.Api/Program.cs`, add before `var app = builder.Build();`:

```csharp
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<TinyCrm.Api.Models.User>,
                              Microsoft.AspNetCore.Identity.PasswordHasher<TinyCrm.Api.Models.User>>();
```

and after `var app = builder.Build();`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
    db.Database.Migrate();
    DatabaseSeeder.Seed(db, scope.ServiceProvider
        .GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<TinyCrm.Api.Models.User>>());
}
```

- [ ] **Step 3: Create the test project**

```powershell
dotnet new xunit -n TinyCrm.Api.Tests -o tests\TinyCrm.Api.Tests
dotnet sln TinyCrmVue.sln add tests\TinyCrm.Api.Tests\TinyCrm.Api.Tests.csproj
cd tests\TinyCrm.Api.Tests
dotnet add reference ..\..\src\TinyCrm.Api\TinyCrm.Api.csproj
dotnet add package Microsoft.AspNetCore.Mvc.Testing
dotnet add package Microsoft.Data.SqlClient
cd ..\..
```

- [ ] **Step 4: Write the test factory (points at the TEST database)**

`tests/TinyCrm.Api.Tests/ApiFactory.cs`:

```csharp
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
```

Also create `tests/TinyCrm.Api.Tests/AssemblyInfo.cs` — xUnit parallelises test
collections by default, which would run tests against a database another class is
still setting up:

```csharp
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

- [ ] **Step 5: Write the failing seed test**

`tests/TinyCrm.Api.Tests/SeedTests.cs`:

```csharp
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TinyCrm.Api.Data;
using Xunit;

namespace TinyCrm.Api.Tests;

[Collection("api")]
public class SeedTests
{
    private readonly ApiFactory _factory;
    public SeedTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Seed_CreatesUsersCustomersAndInteractions()
    {
        _factory.CreateClient();   // boots the app: migrate + seed
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();

        Assert.Equal(2, db.Users.Count());
        Assert.Equal(5, db.Customers.Count());
        Assert.Equal(6, db.Interactions.Count());
    }

    [Fact]
    public void Seed_HashesPasswordsWithPbkdf2_NotSha256()
    {
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
        var admin = db.Users.Single(u => u.Username == "admin");

        // PBKDF2 hashes are Base64 and far longer than a 64-char SHA-256 hex string.
        Assert.NotEqual(64, admin.PasswordHash.Length);
        Assert.Contains("=", admin.PasswordHash);
    }

    [Fact]
    public void DeletingCustomer_CascadesToInteractions()
    {
        _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();

        // Creates its own data: deleting a SEEDED customer would break the
        // "5 seeded customers" assertion in CustomersTests (shared database).
        var customer = new Models.Customer { Name = "Cascade Target", Status = Models.CustomerStatus.Lead, CreatedAt = DateTime.Now };
        customer.Interactions.Add(new Models.Interaction
        {
            Type = Models.InteractionType.Note,
            Subject = "Cascade child",
            InteractionDate = DateTime.Today,
            CreatedAt = DateTime.Now,
        });
        db.Customers.Add(customer);
        db.SaveChanges();
        var id = customer.Id;

        db.Customers.Remove(customer);
        db.SaveChanges();

        Assert.Empty(db.Interactions.Where(i => i.CustomerId == id));
        Assert.Equal(5, db.Customers.Count());   // seed data untouched
    }
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test tests\TinyCrm.Api.Tests\TinyCrm.Api.Tests.csproj`
Expected: FAIL — seeding not wired yet, or `Program` not reachable.

- [ ] **Step 7: Fix until green**

Ensure `public partial class Program { }` is at the end of `Program.cs` (Task 3 Step 5).

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests\TinyCrm.Api.Tests\TinyCrm.Api.Tests.csproj`
Expected: `Passed! - Failed: 0, Passed: 3`  (add `AssemblyInfo.cs` and `ApiCollection` first)

- [ ] **Step 9: Commit**

```powershell
git add src/TinyCrm.Api tests/TinyCrm.Api.Tests
git -c user.name="chatgptkrylor" -c user.email="chatgptkrylor@gmail.com" commit -m "Add seeder with PBKDF2 hashing and integration test harness"
```

---

### Task 5: Cookie authentication + auth endpoints

**Files:**
- Create: `src/TinyCrm.Api/Dtos/AuthDtos.cs`, `Controllers/AuthController.cs`, `tests/TinyCrm.Api.Tests/AuthTests.cs`
- Modify: `src/TinyCrm.Api/Program.cs`

**Interfaces:**
- Consumes: `TinyCrmDbContext`, `IPasswordHasher<User>`
- Produces: `POST /api/auth/login` (body `{username,password}` → 200 `{id,username,displayName}` or 401), `POST /api/auth/logout` → 204, `GET /api/auth/me` → 200 user or 401. Anonymous-401 on business endpoints is asserted in Task 6, once such an endpoint exists.

- [ ] **Step 1: Write the DTOs**

`src/TinyCrm.Api/Dtos/AuthDtos.cs`:

```csharp
namespace TinyCrm.Api.Dtos;

public record LoginRequest(string Username, string Password);

public record UserDto(int Id, string Username, string DisplayName);
```

- [ ] **Step 2: Write the failing auth tests**

`tests/TinyCrm.Api.Tests/AuthTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using TinyCrm.Api.Dtos;
using Xunit;

namespace TinyCrm.Api.Tests;

[Collection("api")]
public class AuthTests
{
    private readonly ApiFactory _factory;
    public AuthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndUser()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var user = await resp.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("admin", user!.Username);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrongpass"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnUsername()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ADMIN", "admin123"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Me_AfterLogin_ReturnsCurrentUser()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me");
        Assert.Equal("admin", me!.Username);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests\TinyCrm.Api.Tests\TinyCrm.Api.Tests.csproj --filter AuthTests`
Expected: FAIL — 404, endpoints do not exist.

- [ ] **Step 4: Wire cookie authentication**

In `Program.cs`, before `builder.Build()`:

```csharp
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "tinycrm.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;                       // D4: CSRF mitigation
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // D4: no Secure over http dev
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;
        // A SPA needs status codes, never HTML redirects.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();
```

and after `var app = builder.Build();`, before `app.MapControllers()`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

- [ ] **Step 5: Write the controller**

`src/TinyCrm.Api/Controllers/AuthController.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TinyCrmDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(TinyCrmDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return Unauthorized();

        // Case-insensitive by SQL Server default collation, matching the MVC app.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
        if (user is null) return Unauthorized();

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (result == PasswordVerificationResult.Failed) return Unauthorized();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("displayName", user.DisplayName),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Ok(new UserDto(user.Id, user.Username, user.DisplayName));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        return Ok(new UserDto(
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            User.FindFirstValue(ClaimTypes.Name)!,
            User.FindFirstValue("displayName")!));
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests\TinyCrm.Api.Tests\TinyCrm.Api.Tests.csproj --filter AuthTests`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 7: Commit**

```powershell
git add src/TinyCrm.Api tests/TinyCrm.Api.Tests
git -c user.name="chatgptkrylor" -c user.email="chatgptkrylor@gmail.com" commit -m "Add cookie authentication and auth endpoints"
```

---

### Task 6: Customers list endpoint with case-insensitive search (D1)

**Files:**
- Create: `src/TinyCrm.Api/Dtos/CustomerDtos.cs`, `Controllers/CustomersController.cs`, `tests/TinyCrm.Api.Tests/CustomersTests.cs`

**Interfaces:**
- Consumes: `TinyCrmDbContext`, cookie auth from Task 5
- Produces: `GET /api/customers?search=&status=` → 200 `CustomerListItem[]`, 401 anonymous

- [ ] **Step 1: Write the DTO**

`src/TinyCrm.Api/Dtos/CustomerDtos.cs`:

```csharp
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Dtos;

public record CustomerListItem(
    int Id, string Name, string? Company, string? Email, string? Phone,
    CustomerStatus Status, DateTime? LastInteractionDate, int InteractionCount);
```

- [ ] **Step 2: Write the failing tests**

`tests/TinyCrm.Api.Tests/CustomersTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;
using Xunit;

namespace TinyCrm.Api.Tests;

[Collection("api")]
public class CustomersTests
{
    private readonly ApiFactory _factory;
    public CustomersTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInClient()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        return client;
    }

    [Fact]
    public async Task List_ReturnsSeededCustomers()
    {
        var client = await LoggedInClient();
        var items = await client.GetFromJsonAsync<List<CustomerListItem>>("/api/customers");
        Assert.Equal(5, items!.Count);
    }

    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Search_MatchesExactCase()
    {
        var client = await LoggedInClient();
        var items = await client.GetFromJsonAsync<List<CustomerListItem>>("/api/customers?search=Acme");
        Assert.Single(items!);
        Assert.Equal("Acme Corp", items![0].Name);
    }

    // DECISION D1: search is deliberately case-INSENSITIVE now. The MVC app filtered
    // in memory with ordinal Contains, so "acme" matched nothing. This test pins the change.
    [Fact]
    public async Task Search_IsCaseInsensitive_D1()
    {
        var client = await LoggedInClient();
        var items = await client.GetFromJsonAsync<List<CustomerListItem>>("/api/customers?search=acme");
        Assert.Single(items!);
        Assert.Equal("Acme Corp", items![0].Name);
    }

    [Fact]
    public async Task StatusFilter_ReturnsOnlyLeads()
    {
        var client = await LoggedInClient();
        var items = await client.GetFromJsonAsync<List<CustomerListItem>>("/api/customers?status=Lead");
        Assert.Equal(2, items!.Count);
        Assert.All(items!, i => Assert.Equal(CustomerStatus.Lead, i.Status));
    }

    [Fact]
    public async Task StatusFilter_InvalidValue_IsIgnored_Returns200()
    {
        var client = await LoggedInClient();
        var resp = await client.GetAsync("/api/customers?status=NotAStatus");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests\TinyCrm.Api.Tests\TinyCrm.Api.Tests.csproj --filter CustomersTests`
Expected: FAIL — 404, controller does not exist.

- [ ] **Step 4: Write the controller**

`src/TinyCrm.Api/Controllers/CustomersController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly TinyCrmDbContext _db;
    public CustomersController(TinyCrmDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<CustomerListItem>>> List(
        [FromQuery] string? search, [FromQuery] string? status)
    {
        var q = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            // Translated to SQL LIKE — case-insensitive under the default collation (D1).
            q = q.Where(c => EF.Functions.Like(c.Name, $"%{s}%")
                          || (c.Email != null   && EF.Functions.Like(c.Email, $"%{s}%"))
                          || (c.Company != null && EF.Functions.Like(c.Company, $"%{s}%")));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CustomerStatus>(status, true, out var st))
            q = q.Where(c => c.Status == st);

        return await q.OrderBy(c => c.Id)
            .Select(c => new CustomerListItem(
                c.Id, c.Name, c.Company, c.Email, c.Phone,
                c.Status, c.LastInteractionDate, c.Interactions.Count()))
            .ToListAsync();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests\TinyCrm.Api.Tests\TinyCrm.Api.Tests.csproj`
Expected: all AuthTests + CustomersTests + SeedTests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/TinyCrm.Api tests/TinyCrm.Api.Tests
git -c user.name="chatgptkrylor" -c user.email="chatgptkrylor@gmail.com" commit -m "Add customers list endpoint with case-insensitive search"
```

---

### Task 7: Vue auth state, API client, login view, router guard

**Files:**
- Create: `src/tiny-crm-web/src/api/client.ts`, `src/auth.ts`, `src/router.ts`, `src/views/LoginView.vue`, `src/views/CustomersView.vue`
- Modify: `src/tiny-crm-web/src/main.ts`, `src/App.vue`

**Interfaces:**
- Consumes: `/api/auth/login`, `/api/auth/me`, `/api/customers`
- Produces: `useAuth()` → `{ user, login, logout, refresh }`; routes `/login`, `/customers`

- [ ] **Step 1: Write the API client**

`src/tiny-crm-web/src/api/client.ts`:

```ts
export class ApiError extends Error {
  constructor(public status: number, message: string, public errors?: Record<string, string[]>) {
    super(message)
  }
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await fetch(path, {
    credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) },
    ...init,
  })

  if (res.status === 401) throw new ApiError(401, 'Unauthorized')
  if (res.status === 400) {
    const body = await res.json().catch(() => ({}))
    throw new ApiError(400, 'Validation failed', body.errors)
  }
  if (!res.ok) throw new ApiError(res.status, res.statusText)
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}
```

- [ ] **Step 2: Write the auth store (plain reactive module — no Pinia)**

`src/tiny-crm-web/src/auth.ts`:

```ts
import { reactive } from 'vue'
import { api, ApiError } from './api/client'

export interface User { id: number; username: string; displayName: string }

const state = reactive<{ user: User | null; ready: boolean }>({ user: null, ready: false })

export function useAuth() {
  return {
    state,
    async refresh() {
      try {
        state.user = await api<User>('/api/auth/me')
      } catch {
        state.user = null
      } finally {
        state.ready = true
      }
    },
    async login(username: string, password: string): Promise<string | null> {
      try {
        state.user = await api<User>('/api/auth/login', {
          method: 'POST',
          body: JSON.stringify({ username, password }),
        })
        return null
      } catch (e) {
        if (e instanceof ApiError && e.status === 401) return 'Invalid username or password.'
        return 'Sign-in failed. Please try again.'
      }
    },
    async logout() {
      await api<void>('/api/auth/logout', { method: 'POST' })
      state.user = null
    },
  }
}
```

- [ ] **Step 3: Write the router with guard**

`src/tiny-crm-web/src/router.ts`:

```ts
import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from './auth'
import LoginView from './views/LoginView.vue'
import CustomersView from './views/CustomersView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/customers' },
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/customers', component: CustomersView },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuth()
  if (!auth.state.ready) await auth.refresh()
  if (!to.meta.public && !auth.state.user) {
    return { path: '/login', query: { returnUrl: to.fullPath } }
  }
  if (to.path === '/login' && auth.state.user) return { path: '/customers' }
  return true
})

export default router
```

- [ ] **Step 4: Write the login view**

`src/tiny-crm-web/src/views/LoginView.vue`:

```vue
<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuth } from '../auth'

const username = ref('')
const password = ref('')
const error = ref<string | null>(null)
const auth = useAuth()
const router = useRouter()
const route = useRoute()

async function submit() {
  error.value = await auth.login(username.value, password.value)
  if (!error.value) {
    const target = (route.query.returnUrl as string) || '/customers'
    router.push(target)
  }
}
</script>

<template>
  <main class="login-card">
    <h1>Tiny CRM</h1>
    <form @submit.prevent="submit">
      <label for="Username">Username</label>
      <input id="Username" name="Username" v-model="username" autocomplete="username" />

      <label for="Password">Password</label>
      <input id="Password" name="Password" type="password" v-model="password" autocomplete="current-password" />

      <div v-if="error" class="validation-summary">{{ error }}</div>

      <button type="submit">Sign in</button>
    </form>
  </main>
</template>
```

Field `name` attributes are `Username`/`Password` and the error container is
`.validation-summary` so the existing Playwright selectors keep working.

- [ ] **Step 5: Write the customers view**

`src/tiny-crm-web/src/views/CustomersView.vue`:

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api } from '../api/client'
import { useAuth } from '../auth'
import { useRouter } from 'vue-router'

interface CustomerListItem {
  id: number; name: string; company: string | null; email: string | null
  phone: string | null; status: string; lastInteractionDate: string | null; interactionCount: number
}

const customers = ref<CustomerListItem[]>([])
const search = ref('')
const status = ref('')
const auth = useAuth()
const router = useRouter()

async function load() {
  const params = new URLSearchParams()
  if (search.value) params.set('search', search.value)
  if (status.value) params.set('status', status.value)
  customers.value = await api<CustomerListItem[]>('/api/customers?' + params.toString())
}

async function signOut() {
  await auth.logout()
  router.push('/login')
}

onMounted(load)
</script>

<template>
  <header>
    <span>{{ auth.state.user?.displayName }}</span>
    <button type="button" @click="signOut">Sign out</button>
  </header>
  <main>
    <h1>Customers</h1>
    <form @submit.prevent="load">
      <input name="search" v-model="search" placeholder="Search" />
      <select name="status" v-model="status" @change="load">
        <option value="">All statuses</option>
        <option value="Lead">Lead</option>
        <option value="Contact">Contact</option>
        <option value="Customer">Customer</option>
      </select>
      <button type="submit">Filter</button>
    </form>

    <table class="table">
      <thead>
        <tr><th>Name</th><th>Company</th><th>Email</th><th>Status</th><th>Interactions</th></tr>
      </thead>
      <tbody>
        <tr v-for="c in customers" :key="c.id">
          <td>{{ c.name }}</td>
          <td>{{ c.company }}</td>
          <td>{{ c.email }}</td>
          <td><span :class="'badge-' + c.status.toLowerCase()">{{ c.status }}</span></td>
          <td>{{ c.interactionCount }}</td>
        </tr>
      </tbody>
    </table>
  </main>
</template>
```

- [ ] **Step 6: Wire the router into the app**

`src/tiny-crm-web/src/main.ts`:

```ts
import { createApp } from 'vue'
import App from './App.vue'
import router from './router'

createApp(App).use(router).mount('#app')
```

`src/tiny-crm-web/src/App.vue`:

```vue
<template>
  <RouterView />
</template>
```

- [ ] **Step 7: Manual smoke test**

In two terminals:

```powershell
# terminal 1
cd src\TinyCrm.Api ; dotnet run
# terminal 2
cd src\tiny-crm-web ; npm run dev
```

Open `http://localhost:5173/customers` → must redirect to `/login?returnUrl=/customers`.
Sign in `admin` / `admin123` → must land on `/customers` showing 5 rows.

**If the cookie does not stick, stop.** That is the Phase 1 gate failing and it means the
proxy/cookie design is wrong — revisit the spec rather than working around it.

- [ ] **Step 8: Commit**

```powershell
git add src/tiny-crm-web
git -c user.name="chatgptkrylor" -c user.email="chatgptkrylor@gmail.com" commit -m "Add Vue login and customers list with router guard"
```

---

### Task 8: Phase 1 gate — one Playwright check + parity change log

**Files:**
- Create: `tests/e2e/PARITY-CHANGES.md`, `tests/e2e/slice.mjs`

**Interfaces:**
- Consumes: running app (Vite `:5173` + Kestrel `:5174`)
- Produces: the Phase 1 exit criterion

- [ ] **Step 1: Start the parity change log**

`tests/e2e/PARITY-CHANGES.md`:

```markdown
# Parity change log

Every deviation of the ported e2e suite from the MVC original. Classes:
**Cosmetic** (selectors/waits) — free. **Structural** (URL shape) — listed.
**Semantic** (assertion meaning changes or check dropped) — REQUIRES USER SIGN-OFF.

| # | Original check | Change | Class | Status |
|---|---|---|---|---|
| 1 | `Unauthenticated root redirects to login` — expects `/Account/Login` | SPA route is `/login?returnUrl=…` | Structural | applied |
| 2 | `SEC: POST without CSRF token rejected` | No anti-forgery tokens in the port; replaced by SameSite + JSON content-type assertions | **Semantic** | **awaiting sign-off** |
| 3 | `SEC: POST with invalid CSRF token rejected` | Same as #2 | **Semantic** | **awaiting sign-off** |

> Tests are frozen before app code is written. Any test edited *after* seeing it fail
> must be added here with a justification.
```

- [ ] **Step 2: Write the slice check**

`tests/e2e/slice.mjs`:

```js
import { chromium } from 'playwright'

const BASE = 'http://localhost:5173'
const results = []
const log = (name, ok, detail = '') => {
  results.push({ name, ok })
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? '  - ' + detail : ''}`)
}

const browser = await chromium.launch({ headless: true })
const page = await browser.newPage()

try {
  await page.goto(BASE + '/customers', { waitUntil: 'domcontentloaded' })
  await page.waitForURL(/\/login/)
  log('Unauthenticated route redirects to login', /returnUrl/.test(page.url()), page.url())

  await page.fill('input[name="Username"]', 'admin')
  await page.fill('input[name="Password"]', 'wrongpass')
  await page.click('button[type="submit"]')
  await page.waitForSelector('.validation-summary')
  log('Wrong password shows error', true)

  await page.fill('input[name="Username"]', 'admin')
  await page.fill('input[name="Password"]', 'admin123')
  await page.click('button[type="submit"]')
  await page.waitForURL(/\/customers/)
  const rows = await page.locator('table.table tbody tr').count()
  log('Valid login lands on customers with 5 rows', rows === 5, `rows=${rows}`)

  await page.reload()
  await page.waitForSelector('table.table tbody tr')
  log('Session survives reload (cookie persisted)', !page.url().includes('/login'), page.url())
} finally {
  await browser.close()
  const failed = results.filter(r => !r.ok).length
  console.log(`\nTOTAL: ${results.length}  PASSED: ${results.length - failed}  FAILED: ${failed}`)
  process.exitCode = failed ? 1 : 0
}
```

- [ ] **Step 3: Run the slice check with both servers running**

Run: `node tests\e2e\slice.mjs`
Expected: `FAILED: 0`

- [ ] **Step 4: Verify the MVC app was not touched**

```powershell
git -C C:\Users\Administrator\Desktop\Tiny-CRM-App status --porcelain
```
Expected: **empty output**. Any output means §0 was violated — investigate before continuing.

- [ ] **Step 5: Commit**

```powershell
git add tests/e2e
git -c user.name="chatgptkrylor" -c user.email="chatgptkrylor@gmail.com" commit -m "Add Phase 1 vertical slice e2e check and parity change log"
```

---

## PHASE 1 GATE

Do not start Phase 2 until all of these hold:

- [ ] `dotnet test` — all backend tests pass
- [ ] `node tests\e2e\slice.mjs` — `FAILED: 0`
- [ ] Login persists across reload (cookie over the Vite proxy works)
- [ ] Enum `Status` serialises as a string (`"Lead"`, not `0`)
- [ ] Anonymous `/api/customers` returns **401 JSON**, not HTML
- [ ] `git -C ...\Tiny-CRM-App status --porcelain` is empty
- [ ] `TinyCrmVue` and `TinyCrmVueTests` exist; `TinyCrm` untouched

**If any fail, stop and revisit the design — do not work around it.**

---

## Phases 2–5 (outline — expanded into their own plan after the Phase 1 gate)

Deliberately not detailed yet: Phase 1 exists to prove the integration design, and detailed
steps written now would be invalidated by anything it uncovers.

- **Phase 2 — Customers CRUD.** `GET/POST/PUT/DELETE /api/customers/{id}`; `ValidationProblemDetails` → per-field `.field-error`; Create/Edit/Details/Delete views; cascade-delete test.
- **Phase 3 — Interactions.** `POST/DELETE /api/interactions`; future-date rejection; ordering `(InteractionDate DESC, Id DESC)` per D2 with a same-day tie-break test; `LastInteractionDate` recalculation.
- **Phase 4 — Dashboard, Reports, CSV.** `GET /api/dashboard`, `GET /api/reports`; CSV as a plain `<a href>` navigation per D3, asserting `content-disposition` and `text/csv`.
- **Phase 5 — Full parity.** Port all 45 functional + 48 adversarial checks; complete `PARITY-CHANGES.md`; user signs off every **Semantic** row; repoint the git remote (spec §12) and push.
