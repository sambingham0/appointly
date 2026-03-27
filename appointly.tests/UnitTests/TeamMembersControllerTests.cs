using appointly.Controllers;
using appointly.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appointly.tests.UnitTests;

// Unit tests for TeamMembersController.cs covering add/remove member paths.
public class TeamMembersControllerTests
{
    // Add returns not found when team does not exist.
    [Fact]
    public async Task TeamMembersControllerTest1()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        await db.SaveChangesAsync();

        var controller = new TeamMembersController(db);

        var result = await controller.Add(teamId: 999, userId: "google:u1");

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(db.TeamMembers);
    }

    // Add returns not found when user does not exist.
    [Fact]
    public async Task TeamMembersControllerTest2()
    {
        using var db = CreateInMemoryDb();
        db.Teams.Add(new Team { Id = 1, Name = "Alpha Team" });
        await db.SaveChangesAsync();

        var controller = new TeamMembersController(db);

        var result = await controller.Add(teamId: 1, userId: "google:missing");

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(db.TeamMembers);
    }

    // Add creates member when relation does not already exist and redirects to team details.
    [Fact]
    public async Task TeamMembersControllerTest3()
    {
        using var db = CreateInMemoryDb();
        db.Teams.Add(new Team { Id = 5, Name = "Project Team" });
        SeedUser(db, "google:u1", "User One");
        await db.SaveChangesAsync();

        var controller = new TeamMembersController(db);

        var result = await controller.Add(teamId: 5, userId: "google:u1");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Teams", redirect.ControllerName);
        Assert.Equal(5, redirect.RouteValues!["id"]);

        var member = await db.TeamMembers.SingleAsync();
        Assert.Equal(5, member.TeamId);
        Assert.Equal("google:u1", member.UserId);
    }

    // Add does not create duplicate member rows when relation already exists.
    [Fact]
    public async Task TeamMembersControllerTest4()
    {
        using var db = CreateInMemoryDb();
        db.Teams.Add(new Team { Id = 6, Name = "Project Team" });
        SeedUser(db, "google:u1", "User One");
        db.TeamMembers.Add(new TeamMember { TeamId = 6, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = new TeamMembersController(db);

        var result = await controller.Add(teamId: 6, userId: "google:u1");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Teams", redirect.ControllerName);
        Assert.Equal(6, redirect.RouteValues!["id"]);
        Assert.Single(db.TeamMembers);
    }

    // Remove deletes existing team member relation and redirects to team details.
    [Fact]
    public async Task TeamMembersControllerTest5()
    {
        using var db = CreateInMemoryDb();
        db.Teams.Add(new Team { Id = 7, Name = "Project Team" });
        SeedUser(db, "google:u1", "User One");
        db.TeamMembers.Add(new TeamMember { TeamId = 7, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = new TeamMembersController(db);

        var result = await controller.Remove(teamId: 7, userId: "google:u1");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Teams", redirect.ControllerName);
        Assert.Equal(7, redirect.RouteValues!["id"]);
        Assert.Empty(db.TeamMembers);
    }

    // Remove with missing relation still redirects and leaves data unchanged.
    [Fact]
    public async Task TeamMembersControllerTest6()
    {
        using var db = CreateInMemoryDb();
        db.Teams.Add(new Team { Id = 8, Name = "Project Team" });
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        db.TeamMembers.Add(new TeamMember { TeamId = 8, UserId = "google:u2" });
        await db.SaveChangesAsync();

        var controller = new TeamMembersController(db);

        var result = await controller.Remove(teamId: 8, userId: "google:u1");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Teams", redirect.ControllerName);
        Assert.Equal(8, redirect.RouteValues!["id"]);
        var remaining = await db.TeamMembers.SingleAsync();
        Assert.Equal("google:u2", remaining.UserId);
    }

    private static void SeedUser(AppointlyContext db, string id, string name)
    {
        if (!db.Users.Any(u => u.Id == id))
        {
            db.Users.Add(new User { Id = id, DisplayName = name, Email = $"{name.Replace(" ", "").ToLowerInvariant()}@example.com" });
        }
    }

    private static AppointlyContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppointlyContext>()
            .UseInMemoryDatabase($"team-members-tests-{Guid.NewGuid()}")
            .Options;

        return new AppointlyContext(options);
    }
}
