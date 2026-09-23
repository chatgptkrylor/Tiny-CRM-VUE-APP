using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Nest;
using TinyCrm.Api.Data;
using TinyCrm.Api.Services;

// The model stores wall-clock local times (DatabaseSeeder uses DateTime.Now, and
// InteractionDate is a plain calendar date). Npgsql's default maps DateTime to
// "timestamp with time zone", which REJECTS any DateTime whose Kind is Local. This
// switch keeps DateTime mapped to "timestamp without time zone", which is what the
// SQL Server datetime2 columns were, so no model or seeder change is needed.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Dev only: the Vite proxy targets 5174. In the container (Production) the aspnet
// image's ASPNETCORE_HTTP_PORTS=8080 applies instead.
if (builder.Environment.IsDevelopment())
    builder.WebHost.UseUrls("http://localhost:5174");

builder.Services.AddDbContext<TinyCrmDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("TinyCrmVue")));

// --- Elasticsearch wiring ---
// Opt-in: only when ElasticSearch:Url is set (appsettings.Development.json points it at
// http://localhost:9200) is the real service registered. Otherwise the NoOp fallback is
// used and search runs on the EF ILIKE path, so the app boots without a running ES.
var esUrl = builder.Configuration["ElasticSearch:Url"];
if (!string.IsNullOrWhiteSpace(esUrl))
{
    var settings = new ConnectionSettings(new Uri(esUrl))
        .DefaultIndex("tinycrm-customers")
        .EnableApiVersioningHeader();
    var client = new ElasticClient(settings);
    builder.Services.AddSingleton<IElasticClient>(client);
    builder.Services.AddScoped<IElasticSearchService, ElasticSearchService>();
}
else
{
    builder.Services.AddScoped<IElasticSearchService, NoOpElasticSearchService>();
}

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<TinyCrm.Api.Models.User>,
                              Microsoft.AspNetCore.Identity.PasswordHasher<TinyCrm.Api.Models.User>>();

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
    db.Database.Migrate();
    DatabaseSeeder.Seed(db, scope.ServiceProvider
        .GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<TinyCrm.Api.Models.User>>());

    // Ensure the ES index exists and bulk-index all customers after seeding.
    var esService = scope.ServiceProvider.GetRequiredService<IElasticSearchService>();
    await esService.EnsureIndexExistsAsync();
    var allCustomers = await db.Customers.AsNoTracking()
        .Include(c => c.Interactions)
        .ToListAsync();
    await esService.BulkIndexAllAsync(allCustomers);

    // Wire up the DbContext's static reference so SaveChangesAsync can sync.
    TinyCrmDbContext.SetElasticSearchService(esService);
}

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

// Unmatched /api/* must be a genuine 404, never the SPA shell. MapFallback with an
// explicit "/api/{**path}" pattern is a lower-priority endpoint scoped to that prefix,
// so real controller routes above still win; only requests nothing else matched land here.
app.MapFallback("/api/{**path}", () => Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the app in tests.
public partial class Program { }
