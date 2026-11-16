using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SkillPilot.Api.Core;
using SkillPilot.Api.Data;
using SkillPilot.Api.Domain;
using SkillPilot.Api.Security;

namespace SkillPilot.Api.Controllers;

public record RegisterRequest([EmailAddress, MaxLength(200)] string Email, [MinLength(8), MaxLength(100)] string Password, [MaxLength(80)] string DisplayName);
public record LoginRequest(string Email, string Password);

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthController(AppDbContext db, JwtService jwt, IConfiguration config) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var open = config.GetValue("Registration:Open", false);
        var allowed = config.GetSection("Registration:AllowedEmails").Get<string[]>() ?? Array.Empty<string>();
        if (!open && !allowed.Select(a => a.Trim().ToLowerInvariant()).Contains(email))
            return StatusCode(403, new { error = "Registration is invite only." });
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = "An account with this email already exists." });

        var user = new User { Email = email, DisplayName = req.DisplayName.Trim(), PasswordHash = PasswordHasher.Hash(req.Password) };
        user.Profile = new UserProfile { PriorityJson = JsonSerializer.Serialize(Curriculum.DefaultPriority) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Ok(new { token = jwt.Create(user), user = new { user.Id, user.Email, user.DisplayName } });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        // Same answer for unknown email and wrong password.
        if (user == null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Email or password is not right." });
        return Ok(new { token = jwt.Create(user), user = new { user.Id, user.Email, user.DisplayName } });
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await db.Users.FindAsync(User.Id());
        return user == null ? Unauthorized() : Ok(new { user.Id, user.Email, user.DisplayName });
    }
}
