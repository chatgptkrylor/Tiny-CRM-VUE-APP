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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
    db.Database.Migrate();
    DatabaseSeeder.Seed(db, scope.ServiceProvider
        .GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<TinyCrm.Api.Models.User>>());
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the app in tests.
public partial class Program { }
