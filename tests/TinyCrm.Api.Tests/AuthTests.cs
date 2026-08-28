using System.Net;
using System.Net.Http.Json;
using TinyCrm.Api.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace TinyCrm.Api.Tests;

[Collection("api")]
public class AuthTests
{
    private readonly ApiFactory _factory;
    private readonly ITestOutputHelper _output;
    public AuthTests(ApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

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
    public async Task Login_UnknownUser_And_WrongPassword_ReturnIdenticalResponses()
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

    [Fact]
    public async Task Login_UnknownUser_PaysPasswordHashingCost()
    {
        var client = _factory.CreateClient();

        // Warm up (JIT, first-request overhead, and - post-F7 - the lazy DummyHash
        // computation) so the timed calls below reflect steady-state PBKDF2 cost,
        // not one-time startup noise.
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("no_such_user_xyz", "whatever"));
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrongpass"));

        var unknownElapsed = await TimeLogin(client, "no_such_user_xyz", "whatever");
        var wrongPwElapsed = await TimeLogin(client, "admin", "wrongpass");
        _output.WriteLine($"unknown-user login: {unknownElapsed.TotalMilliseconds:F1}ms, wrong-password login: {wrongPwElapsed.TotalMilliseconds:F1}ms, ratio: {unknownElapsed.TotalMilliseconds / wrongPwElapsed.TotalMilliseconds:F2}");

        // Coarse band, not an exact match: two PBKDF2 verifications will never take
        // identical microseconds, and CI boxes are noisy. The point is to catch a
        // REGRESSION to a short-circuit "no such user" path, which returns in a
        // fraction of a millisecond versus PBKDF2's tens of milliseconds - a 50%
        // floor sits well clear of normal PBKDF2-to-PBKDF2 jitter but fails hard
        // the instant the hashing step is skipped again.
        Assert.True(
            unknownElapsed.TotalMilliseconds >= wrongPwElapsed.TotalMilliseconds * 0.5,
            $"unknown-user login ({unknownElapsed.TotalMilliseconds:F1}ms) should cost roughly as much as " +
            $"wrong-password login ({wrongPwElapsed.TotalMilliseconds:F1}ms), not be short-circuited");
    }

    private static async Task<TimeSpan> TimeLogin(HttpClient client, string username, string password)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        sw.Stop();
        return sw.Elapsed;
    }

    private static string StripTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"\"");
}
