using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;
using Xunit;

namespace TinyCrm.Api.Tests;

[Collection("api")]
public class CustomersTests
{
    private readonly ApiFactory _factory;
    public CustomersTests(ApiFactory factory) => _factory = factory;

    // GetFromJsonAsync with no explicit options uses JsonSerializerDefaults.Web, which
    // does not register JsonStringEnumConverter. The API serialises Status as a string
    // (Program.cs AddJsonOptions), so the client needs the matching converter to read it back.
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
    public async Task List_ReturnsSeededCustomers()
    {
        var client = await LoggedInClient();
        var items = await client.GetFromJsonAsync<List<CustomerListItem>>("/api/customers", JsonOpts);
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
        var items = await client.GetFromJsonAsync<List<CustomerListItem>>("/api/customers?search=Acme", JsonOpts);
        Assert.Single(items!);
        Assert.Equal("Acme Corp", items![0].Name);
    }

    // DECISION D1: search is deliberately case-INSENSITIVE now. The MVC app filtered
    // in memory with ordinal Contains, so "acme" matched nothing. This test pins the change.
    [Fact]
    public async Task Search_IsCaseInsensitive_D1()
    {
        var client = await LoggedInClient();
        var items = await client.GetFromJsonAsync<List<CustomerListItem>>("/api/customers?search=acme", JsonOpts);
        Assert.Single(items!);
        Assert.Equal("Acme Corp", items![0].Name);
    }

    [Fact]
    public async Task StatusFilter_ReturnsOnlyLeads()
    {
        var client = await LoggedInClient();
        var items = await client.GetFromJsonAsync<List<CustomerListItem>>("/api/customers?status=Lead", JsonOpts);
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
