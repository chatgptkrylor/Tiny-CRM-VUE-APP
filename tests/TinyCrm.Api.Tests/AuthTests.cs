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

    [Fact]
    public async Task Login_UnknownUser_And_WrongPassword_AreIndistinguishable()
    {
        var client = _factory.CreateClient();
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("no_such_user_xyz", "whatever"));
        var wrongPw = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrongpass"));

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(wrongPw.StatusCode, unknown.StatusCode);

        // [ApiController]'s default ProblemDetails body embeds a fresh "traceId" per
        // request (an ambient correlation nonce, unrelated to whether the user exists),
        // so a raw string compare must ignore it. Everything else in the body must match.
        Assert.Equal(StripTraceId(await wrongPw.Content.ReadAsStringAsync()), StripTraceId(await unknown.Content.ReadAsStringAsync()));
    }

    private static string StripTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"\"");
}
