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

        // Warm up (JIT, first-request overhead, thread-pool ramp-up, and - post-F7 -
        // the lazy DummyHash computation) so the timed rounds below reflect steady-state
        // PBKDF2 cost, not one-time startup noise. A few rounds, not one: the thread
        // pool grows its worker count gradually under burst load.
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("no_such_user_xyz", "whatever"));
            await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "wrongpass"));
        }

        // FLAKE FIX: a single timed round was intermittently failing under load - one GC
        // pause, thread-pool stall, or DB-connection hiccup landing on either call is
        // enough to swing a one-shot ratio past the floor either way. Timing noise from
        // contention is one-directional (it can only ADD latency, never subtract it), so
        // the MINIMUM across several interleaved rounds - not the mean or a single
        // sample - is the standard way to recover the true steady-state cost: as long as
        // one round per side lands relatively uncontended, the minimum finds it. A real
        // short-circuit regression, by contrast, returns in a fraction of a millisecond
        // on EVERY round, so its minimum stays near zero regardless of how many rounds
        // run, and the assertion still fails hard.
        const int rounds = 7;
        var unknownTimes = new List<double>();
        var wrongPwTimes = new List<double>();
        for (var i = 0; i < rounds; i++)
        {
            unknownTimes.Add((await TimeLogin(client, "no_such_user_xyz", "whatever")).TotalMilliseconds);
            wrongPwTimes.Add((await TimeLogin(client, "admin", "wrongpass")).TotalMilliseconds);
        }

        var unknownMin = unknownTimes.Min();
        var wrongPwMin = wrongPwTimes.Min();
        _output.WriteLine($"unknown-user login min: {unknownMin:F1}ms, wrong-password login min: {wrongPwMin:F1}ms, ratio: {unknownMin / wrongPwMin:F2}");

        // Coarse band, not an exact match: two PBKDF2 verifications will never take
        // identical microseconds, and this box also runs the API and Vite dev servers
        // alongside the test suite. The point is to catch a REGRESSION to a short-circuit
        // "no such user" path, which returns in a fraction of a millisecond versus
        // PBKDF2's tens of milliseconds - a 40% floor sits well clear of that near-zero
        // ratio but fails hard the instant the hashing step is skipped again.
        Assert.True(
            unknownMin >= wrongPwMin * 0.4,
            $"unknown-user login min ({unknownMin:F1}ms) should cost roughly as much as " +
            $"wrong-password login min ({wrongPwMin:F1}ms), not be short-circuited");
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
