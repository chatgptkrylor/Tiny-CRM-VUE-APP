using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using TinyCrm.Api.Data;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;
using Xunit;

namespace TinyCrm.Api.Tests;

[Collection("api")]
public class DashboardReportsTests
{
    private readonly ApiFactory _factory;
    public DashboardReportsTests(ApiFactory factory) => _factory = factory;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private async Task<HttpClient> LoggedInClient()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        return client;
    }

    [Fact]
    public async Task Dashboard_TotalsMatchSeed()
    {
        var client = await LoggedInClient();
        var dashboard = await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard", JsonOpts);
        Assert.Equal(5, dashboard!.TotalCustomers);
        Assert.Equal(6, dashboard.TotalInteractions);
    }

    [Fact]
    public async Task Dashboard_CustomersByStatus_MatchesSeedDistribution()
    {
        var client = await LoggedInClient();
        var dashboard = await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard", JsonOpts);
        Assert.Equal(2, dashboard!.CustomersByStatus["Lead"]);
        Assert.Equal(1, dashboard.CustomersByStatus["Contact"]);
        Assert.Equal(2, dashboard.CustomersByStatus["Customer"]);
    }

    [Fact]
    public async Task Dashboard_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Reports_CustomersList_IsOrderedByName()
    {
        var client = await LoggedInClient();
        var reports = await client.GetFromJsonAsync<ReportsResponse>("/api/reports", JsonOpts);
        var names = reports!.Customers.Select(c => c.Name).ToList();
        var sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public async Task Reports_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/reports");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ReportsCsv_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/reports/customers.csv");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ReportsCsv_ReturnsExpectedHeaderAndFiveDataRows()
    {
        var client = await LoggedInClient();
        var resp = await client.GetAsync("/api/reports/customers.csv");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/csv", resp.Content.Headers.ContentType?.MediaType);

        // HttpClient parses Content-Disposition into a structured header and reformats it
        // (adds a space after the semicolon) when read back, so assert on the parsed fields
        // rather than the raw wire string.
        var disposition = resp.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("attachment", disposition!.DispositionType);
        Assert.Equal("customers.csv", disposition.FileName);

        var body = await resp.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        Assert.Equal("Id,Name,Company,Email,Phone,Status,InteractionCount,LastInteraction", lines[0]);
        Assert.Equal(6, lines.Count); // header + 5 seeded customers
    }

    [Fact]
    public async Task ReportsCsv_EscapesNameContainingComma()
    {
        var client = await LoggedInClient();

        int newId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
            var customer = new Customer { Name = "Doe, Jane", Status = CustomerStatus.Lead, CreatedAt = DateTime.Now };
            db.Customers.Add(customer);
            db.SaveChanges();
            newId = customer.Id;
        }

        try
        {
            var resp = await client.GetAsync("/api/reports/customers.csv");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("\"Doe, Jane\"", body);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TinyCrmDbContext>();
            var customer = db.Customers.Single(c => c.Id == newId);
            db.Customers.Remove(customer);
            db.SaveChanges();
        }
    }
}
