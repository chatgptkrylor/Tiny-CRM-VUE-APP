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

    // Paging is opt-in. Without pageSize the response must stay the whole list with no
    // paging header, because every pre-existing caller reads it as a plain array.
    [Fact]
    public async Task NoPageSize_ReturnsFullListAndNoCountHeader()
    {
        var client = await LoggedInClient();
        var resp = await client.GetAsync("/api/customers");
        var items = await resp.Content.ReadFromJsonAsync<List<CustomerListItem>>(JsonOpts);

        Assert.Equal(5, items!.Count);
        Assert.False(resp.Headers.Contains("X-Total-Count"));
    }

    [Fact]
    public async Task Paging_ReturnsRequestedSliceAndTotalCount()
    {
        var client = await LoggedInClient();

        var resp = await client.GetAsync("/api/customers?page=1&pageSize=2");
        var first = await resp.Content.ReadFromJsonAsync<List<CustomerListItem>>(JsonOpts);

        Assert.Equal(2, first!.Count);
        Assert.Equal("5", Assert.Single(resp.Headers.GetValues("X-Total-Count")));

        // Last page of 5 rows at 2 per page holds the single remainder row.
        var lastResp = await client.GetAsync("/api/customers?page=3&pageSize=2");
        var last = await lastResp.Content.ReadFromJsonAsync<List<CustomerListItem>>(JsonOpts);

        Assert.Single(last!);
        Assert.DoesNotContain(last!, x => first!.Any(f => f.Id == x.Id));
    }

    // The count must describe the FILTERED set, not the table, or the client renders
    // page links for rows the filter already excluded.
    [Fact]
    public async Task Paging_TotalCountReflectsFilter()
    {
        var client = await LoggedInClient();
        var resp = await client.GetAsync("/api/customers?status=Lead&page=1&pageSize=1");
        var items = await resp.Content.ReadFromJsonAsync<List<CustomerListItem>>(JsonOpts);

        Assert.Single(items!);
        Assert.Equal("2", Assert.Single(resp.Headers.GetValues("X-Total-Count")));
    }

    // (page - 1) * pageSize overflows int at the top of the range; that must not 500.
    [Fact]
    public async Task Paging_PageBeyondEnd_ReturnsEmpty_Not500()
    {
        var client = await LoggedInClient();
        var resp = await client.GetAsync("/api/customers?page=2147483647&pageSize=20");
        var items = await resp.Content.ReadFromJsonAsync<List<CustomerListItem>>(JsonOpts);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(items!);
    }

    [Fact]
    public async Task Paging_HostilePageSize_IsCappedNotHonoured()
    {
        var client = await LoggedInClient();
        var resp = await client.GetAsync("/api/customers?page=1&pageSize=99999999");
        var items = await resp.Content.ReadFromJsonAsync<List<CustomerListItem>>(JsonOpts);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(5, items!.Count);
    }
}
