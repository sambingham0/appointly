using System.Security.Claims;
using appointly.Controllers;
using appointly.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace appointly.tests.UnitTests;

// Unit tests for the authentication controller, "AuthController.cs". These unit tests test the different endpoints used
// to authenticate login info when users log in and out of the app.

public class AuthControllerTests
{
    // Login when Google Scheme Missing returns problem
    [Fact]
    public async Task AuthControllerTest1()
    {
        // Arrange with no Google scheme configured.
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, new FakeAuthenticationSchemeProvider(googleScheme: null));

        // Act: call login endpoint.
        var result = await controller.Login();

        // Assert we return a 500 problem response.
        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Google authentication is not configured.", details.Detail);
    }

    // Login when Google Scheme Configured returns challenge with Google Scheme
    [Fact]
    public async Task AuthControllerTest2()
    {
        using var db = CreateInMemoryDb();
        // Create a fake Google auth scheme for the controller.
        var googleScheme = new AuthenticationScheme(
            GoogleDefaults.AuthenticationScheme,
            GoogleDefaults.AuthenticationScheme,
            typeof(TestAuthHandler));

        var controller = CreateController(
            db,
            new FakeAuthenticationSchemeProvider(googleScheme),
            new FakeUrlHelper(actionResult: "/auth/post-login"));

        // Act: request login with a local return URL.
        var result = await controller.Login("/calendar");

        // Assert challenge uses Google and includes redirect URI.
        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(GoogleDefaults.AuthenticationScheme, challenge.AuthenticationSchemes);
        Assert.NotNull(challenge.Properties);
        Assert.Equal("/auth/post-login", challenge.Properties!.RedirectUri);
    }

    // Post login when Missing ProviderId returns forbid
    [Fact]
    public async Task AuthControllerTest3()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, new FakeAuthenticationSchemeProvider(null));
        // Empty identity means no provider id claim is present.
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await controller.PostLogin();

        // Missing provider id should forbid access.
        Assert.IsType<ForbidResult>(result);
    }

    // Post login when new user and local return URL creates user and local redirects
    [Fact]
    public async Task AuthControllerTest4()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, new FakeAuthenticationSchemeProvider(null));
        // Simulate authenticated Google user claims.
        controller.ControllerContext.HttpContext.User = BuildPrincipal(
            providerUserId: "abc123",
            name: "Ada Lovelace",
            email: "ada@example.com");

        // Act with a safe local URL.
        var result = await controller.PostLogin("/appointments");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/appointments", redirect.Url);

        // Confirm user was created from claims.
        var savedUser = await db.Users.SingleAsync();
        Assert.Equal("google:abc123", savedUser.Id);
        Assert.Equal("Ada Lovelace", savedUser.DisplayName);
        Assert.Equal("ada@example.com", savedUser.Email);
    }

    // Post login when existing user and non-local return URL updates user and redirects home
    [Fact]
    public async Task AuthControllerTest5()
    {
        using var db = CreateInMemoryDb();
        // Seed an existing user to test update logic.
        db.Users.Add(new User
        {
            Id = "google:abc123",
            DisplayName = "Old Name",
            Email = "old@example.com"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, new FakeAuthenticationSchemeProvider(null));
        // Incoming claims should overwrite existing profile values.
        controller.ControllerContext.HttpContext.User = BuildPrincipal(
            providerUserId: "abc123",
            name: "Updated Name",
            email: "updated@example.com");

        // Non-local URL should not be used for redirect.
        var result = await controller.PostLogin("https://not-local.example.com");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);

        // User record should be updated in the DB.
        var updated = await db.Users.SingleAsync(u => u.Id == "google:abc123");
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal("updated@example.com", updated.Email);
    }

    // Logout signs out cookie scheme and redirects home
    [Fact]
    public async Task AuthControllerTest6()
    {
        using var db = CreateInMemoryDb();
        // Fake auth service captures which scheme is signed out.
        var authService = new FakeAuthenticationService();
        var controller = CreateController(db, new FakeAuthenticationSchemeProvider(null), authenticationService: authService);

        var result = await controller.Logout();

        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, authService.LastSignOutScheme);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    private static AppointlyContext CreateInMemoryDb()
    {
        // Unique in-memory DB prevents data leaking between tests.
        var options = new DbContextOptionsBuilder<AppointlyContext>()
            .UseInMemoryDatabase($"auth-tests-{Guid.NewGuid()}")
            .Options;

        return new AppointlyContext(options);
    }

    private static AuthController CreateController(
        AppointlyContext db,
        IAuthenticationSchemeProvider schemeProvider,
        IUrlHelper? urlHelper = null,
        IAuthenticationService? authenticationService = null)
    {
        var services = new ServiceCollection();
        services.AddControllers();
        services.AddSingleton(authenticationService ?? new FakeAuthenticationService());

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        var controller = new AuthController(db, schemeProvider)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            Url = urlHelper ?? new FakeUrlHelper()
        };

        return controller;
    }

    private static ClaimsPrincipal BuildPrincipal(string providerUserId, string name, string email)
    {
        // Claims expected by AuthController.PostLogin.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, providerUserId),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private sealed class FakeAuthenticationSchemeProvider(AuthenticationScheme? googleScheme) : IAuthenticationSchemeProvider
    {
        public Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync()
        {
            IEnumerable<AuthenticationScheme> schemes = googleScheme is null
                ? []
                : [googleScheme];
            return Task.FromResult(schemes);
        }

        public Task<AuthenticationScheme?> GetDefaultAuthenticateSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetDefaultChallengeSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetDefaultForbidSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetDefaultSignInSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<AuthenticationScheme?> GetDefaultSignOutSchemeAsync() => Task.FromResult<AuthenticationScheme?>(null);
        public Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync() => Task.FromResult<IEnumerable<AuthenticationScheme>>([]);

        public Task<AuthenticationScheme?> GetSchemeAsync(string name)
        {
            if (name == GoogleDefaults.AuthenticationScheme)
            {
                return Task.FromResult<AuthenticationScheme?>(googleScheme);
            }

            return Task.FromResult<AuthenticationScheme?>(null);
        }

        public void AddScheme(AuthenticationScheme scheme)
        {
        }

        public void RemoveScheme(string name)
        {
        }
    }

    private sealed class FakeUrlHelper(string actionResult = "/auth/post-login") : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext actionContext) => actionResult;
        public string Content(string? contentPath) => contentPath ?? string.Empty;
        public bool IsLocalUrl(string? url) => !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//");
        public string? Link(string? routeName, object? values) => null;
        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public string? LastSignOutScheme { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            LastSignOutScheme = scheme;
            return Task.CompletedTask;
        }
    }

    // Only needed to construct AuthenticationScheme for tests.
    private sealed class TestAuthHandler : IAuthenticationHandler
    {
        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => Task.CompletedTask;
        public Task<AuthenticateResult> AuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(AuthenticationProperties? properties) => Task.CompletedTask;
        public Task ForbidAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
