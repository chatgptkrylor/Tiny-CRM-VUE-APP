using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;
using Xunit;

namespace TinyCrm.Api.Tests;

// Covers the customer/interaction create-edit-details-delete endpoints added to reach
// feature parity with the original MVC app's CustomersController/InteractionsController.
//
// Every test that creates data cleans up after itself (deletes what it created): the
// database is shared across the whole assembly and SeedTests/CustomersTests assert the
// exact seeded counts (5 customers, 6 interactions).
[Collection("api")]
public class CustomerCrudTests
{
    private readonly ApiFactory _factory;
    public CustomerCrudTests(ApiFactory factory) => _factory = factory;

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

    private static object ValidCustomerPayload(string name = "Test Customer") => new
    {
        name,
        company = "Test Co",
        email = "test@example.com",
        phone = "+1 555 000 0000",
        status = "Lead",
        notes = "Created by test",
    };

    private static async Task<CustomerDetail> CreateCustomer(HttpClient client, string name = "Test Customer")
    {
        var resp = await client.PostAsJsonAsync("/api/customers", ValidCustomerPayload(name));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CustomerDetail>(JsonOpts))!;
    }

    private static async Task<InteractionItem> CreateInteraction(HttpClient client, int customerId, string date, string subject = "Test interaction")
    {
        var resp = await client.PostAsJsonAsync("/api/interactions", new
        {
            customerId,
            type = "Note",
            subject,
            interactionDate = date,
            notes = (string?)null,
        });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<InteractionItem>(JsonOpts))!;
    }

    [Fact]
    public async Task Create_ValidCustomer_SucceedsAndIsRetrievable()
    {
        var client = await LoggedInClient();
        var resp = await client.PostAsJsonAsync("/api/customers", ValidCustomerPayload("Create Me"));
        Assert.True(resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
        var created = await resp.Content.ReadFromJsonAsync<CustomerDetail>(JsonOpts);

        var fetched = await client.GetFromJsonAsync<CustomerDetail>($"/api/customers/{created!.Id}", JsonOpts);
        Assert.Equal("Create Me", fetched!.Name);
        Assert.Equal("Test Co", fetched.Company);
        Assert.Empty(fetched.Interactions);

        var del = await client.DeleteAsync($"/api/customers/{created.Id}");
        Assert.True(del.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Create_InvalidCustomer_Returns400WithFieldErrors()
    {
        var client = await LoggedInClient();
        var resp = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "A",                 // too short (min 2)
            company = "",
            email = "not-an-email",     // invalid
            phone = "",
            status = "Lead",
            notes = "",
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Name", problem!.Errors.Keys);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var client = await LoggedInClient();
        var created = await CreateCustomer(client, "Update Me");

        var updateResp = await client.PutAsJsonAsync($"/api/customers/{created.Id}", new
        {
            name = "Updated Name",
            company = "New Co",
            email = "new@example.com",
            phone = "+1 555 999 9999",
            status = "Customer",
            notes = "Updated notes",
        });
        Assert.True(updateResp.IsSuccessStatusCode);

        var fetched = await client.GetFromJsonAsync<CustomerDetail>($"/api/customers/{created.Id}", JsonOpts);
        Assert.Equal("Updated Name", fetched!.Name);
        Assert.Equal("New Co", fetched.Company);
        Assert.Equal(CustomerStatus.Customer, fetched.Status);

        await client.DeleteAsync($"/api/customers/{created.Id}");
    }

    [Fact]
    public async Task Update_MissingCustomer_Returns404()
    {
        var client = await LoggedInClient();
        var resp = await client.PutAsJsonAsync("/api/customers/999999", ValidCustomerPayload());
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingCustomer_Returns404()
    {
        var client = await LoggedInClient();
        var resp = await client.DeleteAsync("/api/customers/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesCustomerAndItsInteractions()
    {
        var client = await LoggedInClient();
        var created = await CreateCustomer(client, "Delete Me");
        var interaction = await CreateInteraction(client, created.Id, DateTime.Today.ToString("yyyy-MM-dd"));

        var deleteResp = await client.DeleteAsync($"/api/customers/{created.Id}");
        Assert.True(deleteResp.IsSuccessStatusCode);

        var getResp = await client.GetAsync($"/api/customers/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);

        var getInteractionDelete = await client.DeleteAsync($"/api/interactions/{interaction.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getInteractionDelete.StatusCode);   // already gone via cascade
    }

    // D2: same-day interactions are ordered (InteractionDate DESC, Id DESC) so the order
    // is deterministic instead of depending on physical row order.
    [Fact]
    public async Task Details_OrdersInteractions_ByDateDescThenIdDesc_D2()
    {
        var client = await LoggedInClient();
        var created = await CreateCustomer(client, "Order Test");
        var sameDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

        var first = await CreateInteraction(client, created.Id, sameDate, "First");
        var second = await CreateInteraction(client, created.Id, sameDate, "Second");
        var third = await CreateInteraction(client, created.Id, sameDate, "Third");

        var detail = await client.GetFromJsonAsync<CustomerDetail>($"/api/customers/{created.Id}", JsonOpts);
        var ids = detail!.Interactions.Select(i => i.Id).ToList();
        Assert.Equal(new[] { third.Id, second.Id, first.Id }, ids);

        await client.DeleteAsync($"/api/customers/{created.Id}");
    }

    [Fact]
    public async Task AddInteraction_UpdatesCustomerLastInteractionDate()
    {
        var client = await LoggedInClient();
        var created = await CreateCustomer(client, "LID Test");
        Assert.Null(created.LastInteractionDate);

        var date = DateTime.Today.AddDays(-2);
        await CreateInteraction(client, created.Id, date.ToString("yyyy-MM-dd"));

        var fetched = await client.GetFromJsonAsync<CustomerDetail>($"/api/customers/{created.Id}", JsonOpts);
        Assert.Equal(date.Date, fetched!.LastInteractionDate!.Value.Date);

        await client.DeleteAsync($"/api/customers/{created.Id}");
    }

    [Fact]
    public async Task CreateInteraction_FutureDate_Returns400()
    {
        var client = await LoggedInClient();
        var created = await CreateCustomer(client, "Future Test");

        var resp = await client.PostAsJsonAsync("/api/interactions", new
        {
            customerId = created.Id,
            type = "Call",
            subject = "Future interaction",
            interactionDate = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"),
            notes = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        await client.DeleteAsync($"/api/customers/{created.Id}");
    }

    [Fact]
    public async Task CreateInteraction_UnknownCustomer_Returns404()
    {
        var client = await LoggedInClient();
        var resp = await client.PostAsJsonAsync("/api/interactions", new
        {
            customerId = 999999,
            type = "Call",
            subject = "Orphan interaction",
            interactionDate = DateTime.Today.ToString("yyyy-MM-dd"),
            notes = (string?)null,
        });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteInteraction_MissingInteraction_Returns404()
    {
        var client = await LoggedInClient();
        var resp = await client.DeleteAsync("/api/interactions/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteInteraction_RecalculatesLastInteractionDate()
    {
        var client = await LoggedInClient();
        var created = await CreateCustomer(client, "Recalc Test");

        var older = DateTime.Today.AddDays(-10);
        var newer = DateTime.Today.AddDays(-1);
        await CreateInteraction(client, created.Id, older.ToString("yyyy-MM-dd"), "Older");
        var newerInteraction = await CreateInteraction(client, created.Id, newer.ToString("yyyy-MM-dd"), "Newer");

        var afterBoth = await client.GetFromJsonAsync<CustomerDetail>($"/api/customers/{created.Id}", JsonOpts);
        Assert.Equal(newer.Date, afterBoth!.LastInteractionDate!.Value.Date);

        var deleteResp = await client.DeleteAsync($"/api/interactions/{newerInteraction.Id}");
        Assert.True(deleteResp.IsSuccessStatusCode);

        var afterDelete = await client.GetFromJsonAsync<CustomerDetail>($"/api/customers/{created.Id}", JsonOpts);
        Assert.Equal(older.Date, afterDelete!.LastInteractionDate!.Value.Date);

        await client.DeleteAsync($"/api/customers/{created.Id}");
    }

    [Fact]
    public async Task AnonymousAccess_Returns401_OnEveryNewEndpoint()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customers/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/customers", ValidCustomerPayload())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"/api/customers/1", ValidCustomerPayload())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/customers/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/interactions", new
        {
            customerId = 1,
            type = "Call",
            subject = "x",
            interactionDate = DateTime.Today.ToString("yyyy-MM-dd"),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/interactions/1")).StatusCode);
    }
}
