using appointly.Controllers;
using appointly.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appointly.tests.UnitTests;

// Unit tests for UsersController.cs covering common CRUD paths.
public class UsersControllerTests
{
    // Index returns view with all users
    [Fact]
    public async Task UsersControllerTest1()
    {
        using var db = CreateInMemoryDb();
        db.Users.AddRange(
            new User { Id = "u1", DisplayName = "User One", Email = "u1@example.com" },
            new User { Id = "u2", DisplayName = "User Two", Email = "u2@example.com" });
        await db.SaveChangesAsync();

        var controller = new UsersController(db);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<User>>(view.Model);
        Assert.Equal(2, model.Count());
    }

    // Details with null id returns not found
    [Fact]
    public async Task UsersControllerTest2()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);

        var result = await controller.Details(null);

        Assert.IsType<NotFoundResult>(result);
    }

    // Details for missing user returns not found
    [Fact]
    public async Task UsersControllerTest3()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);

        var result = await controller.Details("missing-id");

        Assert.IsType<NotFoundResult>(result);
    }

    // Details for existing user returns view with user model
    [Fact]
    public async Task UsersControllerTest4()
    {
        using var db = CreateInMemoryDb();
        db.Users.Add(new User
        {
            Id = "u1",
            DisplayName = "Ada",
            Email = "ada@example.com"
        });
        await db.SaveChangesAsync();

        var controller = new UsersController(db);

        var result = await controller.Details("u1");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<User>(view.Model);
        Assert.Equal("u1", model.Id);
    }

    // Create GET returns view
    [Fact]
    public void UsersControllerTest5()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);

        var result = controller.Create();

        Assert.IsType<ViewResult>(result);
    }

    // Create POST with valid model saves user and redirects to index
    [Fact]
    public async Task UsersControllerTest6()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);
        var user = new User
        {
            Id = "u1",
            DisplayName = "New User",
            Email = "new@example.com"
        };

        var result = await controller.Create(user);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.True(await db.Users.AnyAsync(u => u.Id == "u1"));
    }

    // Create POST with invalid model state returns same view/model
    [Fact]
    public async Task UsersControllerTest7()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);
        controller.ModelState.AddModelError("DisplayName", "Required");

        var user = new User { Id = "u1", DisplayName = string.Empty };
        var result = await controller.Create(user);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(user, view.Model);
        Assert.Empty(db.Users);
    }

    // Edit GET with null id returns not found
    [Fact]
    public async Task UsersControllerTest8()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);

        var result = await controller.Edit((string?)null);

        Assert.IsType<NotFoundResult>(result);
    }

    // Edit GET for missing user returns not found
    [Fact]
    public async Task UsersControllerTest9()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);

        var result = await controller.Edit("missing-id");

        Assert.IsType<NotFoundResult>(result);
    }

    // Edit GET for existing user returns view with user
    [Fact]
    public async Task UsersControllerTest10()
    {
        using var db = CreateInMemoryDb();
        db.Users.Add(new User { Id = "u1", DisplayName = "Edit Me" });
        await db.SaveChangesAsync();

        var controller = new UsersController(db);

        var result = await controller.Edit("u1");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<User>(view.Model);
        Assert.Equal("u1", model.Id);
    }

    // Edit POST with route id mismatch returns not found
    [Fact]
    public async Task UsersControllerTest11()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);

        var result = await controller.Edit("u1", new User { Id = "u2", DisplayName = "Mismatch" });

        Assert.IsType<NotFoundResult>(result);
    }

    // Edit POST with invalid model returns same view/model
    [Fact]
    public async Task UsersControllerTest12()
    {
        using var db = CreateInMemoryDb();
        db.Users.Add(new User { Id = "u1", DisplayName = "Original" });
        await db.SaveChangesAsync();

        var controller = new UsersController(db);
        controller.ModelState.AddModelError("DisplayName", "Required");
        var incoming = new User { Id = "u1", DisplayName = string.Empty };

        var result = await controller.Edit("u1", incoming);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(incoming, view.Model);
    }

    // Edit POST with valid model updates user and redirects to index
    [Fact]
    public async Task UsersControllerTest13()
    {
        using var db = CreateInMemoryDb();
        var existing = new User
        {
            Id = "u1",
            DisplayName = "Before",
            Email = "before@example.com"
        };
        db.Users.Add(existing);
        await db.SaveChangesAsync();
        db.Entry(existing).State = EntityState.Detached;

        var controller = new UsersController(db);
        var updatedUser = new User
        {
            Id = "u1",
            DisplayName = "After",
            Email = "after@example.com"
        };

        var result = await controller.Edit("u1", updatedUser);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var userFromDb = await db.Users.SingleAsync(u => u.Id == "u1");
        Assert.Equal("After", userFromDb.DisplayName);
        Assert.Equal("after@example.com", userFromDb.Email);
    }

    // Delete GET with null id returns not found
    [Fact]
    public async Task UsersControllerTest14()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);

        var result = await controller.Delete(null);

        Assert.IsType<NotFoundResult>(result);
    }

    // Delete GET for missing user returns not found
    [Fact]
    public async Task UsersControllerTest15()
    {
        using var db = CreateInMemoryDb();
        var controller = new UsersController(db);

        var result = await controller.Delete("missing-id");

        Assert.IsType<NotFoundResult>(result);
    }

    // Delete GET for existing user returns view with user
    [Fact]
    public async Task UsersControllerTest16()
    {
        using var db = CreateInMemoryDb();
        db.Users.Add(new User { Id = "u1", DisplayName = "Delete Me" });
        await db.SaveChangesAsync();

        var controller = new UsersController(db);

        var result = await controller.Delete("u1");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<User>(view.Model);
        Assert.Equal("u1", model.Id);
    }

    // DeleteConfirmed removes related rows, created appointments, and the user
    [Fact]
    public async Task UsersControllerTest17()
    {
        using var db = CreateInMemoryDb();

        var user = new User { Id = "u1", DisplayName = "Owner" };
        var team = new Team { Id = 10, Name = "Team A" };
        var appointment = new Appointment
        {
            Id = 100,
            Title = "Planning",
            CreatedByUserId = "u1",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.Teams.Add(team);
        db.Appointments.Add(appointment);
        db.TeamMembers.Add(new TeamMember { TeamId = 10, UserId = "u1" });
        db.AppointmentParticipants.Add(new AppointmentParticipant { AppointmentId = 100, UserId = "u1" });
        await db.SaveChangesAsync();

        var controller = new UsersController(db);

        var result = await controller.DeleteConfirmed("u1");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        Assert.False(await db.Users.AnyAsync(u => u.Id == "u1"));
        Assert.False(await db.TeamMembers.AnyAsync(tm => tm.UserId == "u1"));
        Assert.False(await db.AppointmentParticipants.AnyAsync(ap => ap.UserId == "u1"));
        Assert.False(await db.Appointments.AnyAsync(a => a.CreatedByUserId == "u1"));
    }

    private static AppointlyContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppointlyContext>()
            .UseInMemoryDatabase($"users-tests-{Guid.NewGuid()}")
            .Options;

        return new AppointlyContext(options);
    }
}
