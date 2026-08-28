using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5174");

builder.Services.AddDbContext<TinyCrmDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("TinyCrmVue")));

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
}

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the app in tests.
public partial class Program { }
