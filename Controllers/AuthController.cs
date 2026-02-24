using appointly;
using appointly.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace appointly.Controllers;

public class AuthController : Controller
{
    private readonly AppointlyContext _db;
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;

    public AuthController(AppointlyContext db, IAuthenticationSchemeProvider authenticationSchemeProvider)
    {
        _db = db;
        _authenticationSchemeProvider = authenticationSchemeProvider;
    }

    [HttpGet("/auth/login")]
    public async Task<IActionResult> Login(string? returnUrl = "/")
    {
        var googleScheme = await _authenticationSchemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme);
        if (googleScheme is null)
        {
            return Problem("Google authentication is not configured.");
        }

        var redirectUrl = Url.Action(nameof(PostLogin), values: new { returnUrl }) ?? "/";
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("/auth/post-login")]
    public async Task<IActionResult> PostLogin(string? returnUrl = "/")
    {
        var providerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            return Forbid();
        }

        var userId = $"google:{providerUserId}";
        var displayName = User.FindFirstValue(ClaimTypes.Name) ?? "User";
        var email = User.FindFirstValue(ClaimTypes.Email);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            user = new User
            {
                Id = userId,
                DisplayName = displayName,
                Email = email
            };
            _db.Users.Add(user);
        }
        else
        {
            user.DisplayName = displayName;
            user.Email = email;
        }

        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost("/auth/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}
