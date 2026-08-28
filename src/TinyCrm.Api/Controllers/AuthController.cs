using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TinyCrm.Api.Data;
using TinyCrm.Api.Dtos;
using TinyCrm.Api.Models;

namespace TinyCrm.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly TinyCrmDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    // Computed once at class level: a real PBKDF2 hash to verify against when the
    // user does not exist, so a missing username costs the same as a wrong password
    // and cannot be distinguished by timing.
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(new User(), "dummy-password-for-timing");

    public AuthController(TinyCrmDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return Unauthorized();

        // Case-insensitive by SQL Server default collation, matching the MVC app.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);

        // Always verify a hash, even when the user is absent, so a missing username
        // costs the same as a wrong password and cannot be distinguished by timing.
        var result = _hasher.VerifyHashedPassword(user ?? new User(), user?.PasswordHash ?? DummyHash, req.Password);
        if (user is null || result == PasswordVerificationResult.Failed) return Unauthorized();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("displayName", user.DisplayName),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Ok(new UserDto(user.Id, user.Username, user.DisplayName));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        return Ok(new UserDto(
            id,
            User.FindFirstValue(ClaimTypes.Name)!,
            User.FindFirstValue("displayName")!));
    }
}
