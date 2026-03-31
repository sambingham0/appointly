using System.Security.Claims;
using appointly.Controllers;
using appointly.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appointly.tests.UnitTests;

// Unit tests for TeamsController.cs covering common CRUD paths.
public class TeamsControllerTests
{
    // Basic list/read scenarios.
    [Fact]
    public async Task TeamsControllerTest1()
    {
        using var db = CreateInMemoryDb();
        db.Teams.AddRange(
            new Team { Id = 1, Name = "Alpha Team" },
            new Team { Id = 2, Name = "Beta Team" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Team>>(view.Model);
        Assert.Equal(2, model.Count());
    }

    [Fact]
    public async Task TeamsControllerTest2()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Teams.AddRange(
            new Team { Id = 1, Name = "Alpha Team" },
            new Team { Id = 2, Name = "Beta Team" },
            new Team { Id = 3, Name = "Gamma Team" });
        db.TeamMembers.AddRange(
            new TeamMember { TeamId = 1, UserId = "google:u1" },
            new TeamMember { TeamId = 2, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Team>>(view.Model);
        Assert.Equal(3, model.Count());
    }

    [Fact]
    public async Task TeamsControllerTest3()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Details(null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TeamsControllerTest4()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Details(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TeamsControllerTest5()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        SeedUser(db, "google:u3", "User Three");

        db.Teams.Add(new Team { Id = 10, Name = "Project Team" });
        db.TeamMembers.AddRange(
            new TeamMember { TeamId = 10, UserId = "google:u1" },
            new TeamMember { TeamId = 10, UserId = "google:u2" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.Details(10);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Team>(view.Model);
        Assert.Equal(10, model.Id);

        // The controller exposes users that are not already members.
        var potentialMembersObj = (object)controller.ViewBag.PotentialMembers;
        var potentialMembers = Assert.IsAssignableFrom<IEnumerable<User>>(potentialMembersObj);
        var potentialMemberList = potentialMembers.ToList();
        Assert.Single(potentialMemberList);
        Assert.Equal("google:u3", potentialMemberList[0].Id);
    }

    // Create action scenarios.
    [Fact]
    public void TeamsControllerTest6()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = controller.Create();

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task TeamsControllerTest7()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.Create(new Team { Name = "No User Team" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
    }

    [Fact]
    public async Task TeamsControllerTest8()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, BuildPrincipal("u1"));
        controller.ModelState.AddModelError("Name", "Required");
        var team = new Team { Name = string.Empty };

        var result = await controller.Create(team);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(team, view.Model);
        Assert.Empty(db.Teams);
    }

    [Fact]
    public async Task TeamsControllerTest9()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        var controller = CreateController(db, BuildPrincipal("u1"));

        var result = await controller.Create(new Team { Name = "Created Team" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        // Current controller behavior: creates a team record only.
        var savedTeam = await db.Teams.SingleAsync();
        Assert.Equal("Created Team", savedTeam.Name);
        Assert.Empty(db.TeamMembers);
    }

    // Edit action scenarios.
    [Fact]
    public async Task TeamsControllerTest10()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Edit((int?)null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TeamsControllerTest11()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Teams.Add(new Team { Id = 5, Name = "Edit Team" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.Edit(5);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Team>(view.Model);
        Assert.Equal(5, model.Id);
    }

    [Fact]
    public async Task TeamsControllerTest12()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Teams.Add(new Team { Id = 6, Name = "Edit Team" });
        db.TeamMembers.Add(new TeamMember { TeamId = 6, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.Edit(6);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Team>(view.Model);
        Assert.Equal(6, model.Id);
    }

    [Fact]
    public async Task TeamsControllerTest13()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, BuildPrincipal("u1"));

        var result = await controller.Edit(1, new Team { Id = 2, Name = "Mismatch" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TeamsControllerTest14()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        var existing = new Team { Id = 7, Name = "Before" };
        db.Teams.Add(existing);
        await db.SaveChangesAsync();
        // Detach the seeded entity so posting a new instance with the same key does not conflict.
        db.Entry(existing).State = EntityState.Detached;

        var controller = CreateController(db, BuildPrincipal("u1"));
        var incoming = new Team { Id = 7, Name = "After" };

        var result = await controller.Edit(7, incoming);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var updated = await db.Teams.SingleAsync(t => t.Id == 7);
        Assert.Equal("After", updated.Name);
    }

    [Fact]
    public async Task TeamsControllerTest15()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Teams.Add(new Team { Id = 8, Name = "Before" });
        db.TeamMembers.Add(new TeamMember { TeamId = 8, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        controller.ModelState.AddModelError("Name", "Required");
        var incoming = new Team { Id = 8, Name = string.Empty };

        var result = await controller.Edit(8, incoming);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(incoming, view.Model);
    }

    [Fact]
    public async Task TeamsControllerTest16()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        var existing = new Team { Id = 9, Name = "Before" };
        db.Teams.Add(existing);
        db.TeamMembers.Add(new TeamMember { TeamId = 9, UserId = "google:u1" });
        await db.SaveChangesAsync();
        // Detach for the same reason as Test14.
        db.Entry(existing).State = EntityState.Detached;

        var controller = CreateController(db, BuildPrincipal("u1"));
        var incoming = new Team { Id = 9, Name = "After" };

        var result = await controller.Edit(9, incoming);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var updated = await db.Teams.SingleAsync(t => t.Id == 9);
        Assert.Equal("After", updated.Name);
    }

    [Fact]
    public async Task TeamsControllerTest17()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Delete(null);

        Assert.IsType<NotFoundResult>(result);
    }

    // Delete action scenarios.
    [Fact]
    public async Task TeamsControllerTest18()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Teams.Add(new Team { Id = 11, Name = "Delete Team" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.Delete(11);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Team>(view.Model);
        Assert.Equal(11, model.Id);
    }

    [Fact]
    public async Task TeamsControllerTest19()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Teams.Add(new Team { Id = 12, Name = "Delete Team" });
        db.TeamMembers.Add(new TeamMember { TeamId = 12, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.Delete(12);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Team>(view.Model);
        Assert.Equal(12, model.Id);
    }

    [Fact]
    public async Task TeamsControllerTest20()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Teams.Add(new Team { Id = 13, Name = "Delete Team" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.DeleteConfirmed(13);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.False(await db.Teams.AnyAsync(t => t.Id == 13));
    }

    [Fact]
    public async Task TeamsControllerTest21()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        db.Teams.Add(new Team { Id = 14, Name = "Delete Team" });
        db.TeamMembers.AddRange(
            new TeamMember { TeamId = 14, UserId = "google:u1" },
            new TeamMember { TeamId = 14, UserId = "google:u2" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.DeleteConfirmed(14);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.False(await db.Teams.AnyAsync(t => t.Id == 14));
        Assert.False(await db.TeamMembers.AnyAsync(tm => tm.TeamId == 14));
    }

    [Fact]
    public async Task TeamsControllerTest22()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.TeamMembers.Add(new TeamMember { TeamId = 999, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));
        var result = await controller.DeleteConfirmed(999);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    // Creates a controller with a fake HttpContext/User so auth-dependent code can run in tests.
    private static TeamsController CreateController(AppointlyContext db, ClaimsPrincipal? user = null)
    {
        var controller = new TeamsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        return controller;
    }

    // Builds a principal with the same NameIdentifier claim shape used by the app.
    private static ClaimsPrincipal BuildPrincipal(string providerUserId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, providerUserId)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    // Adds test users when needed for relationship or view-model setup.
    private static void SeedUser(AppointlyContext db, string id, string name)
    {
        if (!db.Users.Any(u => u.Id == id))
        {
            db.Users.Add(new User { Id = id, DisplayName = name, Email = $"{name.Replace(" ", "").ToLowerInvariant()}@example.com" });
        }
    }

    // Uses a unique in-memory database per test to avoid cross-test data leakage.
    private static AppointlyContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppointlyContext>()
            .UseInMemoryDatabase($"teams-tests-{Guid.NewGuid()}")
            .Options;

        return new AppointlyContext(options);
    }
}
