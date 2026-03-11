using System.Security.Claims;
using appointly.Controllers;
using appointly.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace appointly.tests.UnitTests;

// Unit tests for the appointments controller, "AppointmentsController.cs". These unit tests test the different endpoints used
// to create, edit, and delete appointments while using the app.
public class AppointmentsControllerTests
{
    // Index returns appointments sorted by descending start time.
    [Fact]
    public async Task AppointmentsControllerTest1()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);

        db.Appointments.AddRange(
            new Appointment
            {
                Title = "Later",
                StartTimeUtc = DateTime.UtcNow.AddHours(2),
                EndTimeUtc = DateTime.UtcNow.AddHours(3),
                CreatedByUserId = "google:u1",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new Appointment
            {
                Title = "Sooner",
                StartTimeUtc = DateTime.UtcNow.AddHours(1),
                EndTimeUtc = DateTime.UtcNow.AddHours(2),
                CreatedByUserId = "google:u1",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act: load appointments list.
        var result = await controller.Index();

        // Assert list is returned and sorted newest-start first.
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<Appointment>>(view.Model);
        Assert.Equal(2, model.Count);
        Assert.True(model[0].StartTimeUtc >= model[1].StartTimeUtc);
    }

    // Details returns NotFound when id is missing.
    [Fact]
    public async Task AppointmentsControllerTest2()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Details(null);

        Assert.IsType<NotFoundResult>(result);
    }

    // Details returns appointment and only users not already participating.
    [Fact]
    public async Task AppointmentsControllerTest3()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);

        var appt = new Appointment
        {
            Title = "Project Sync",
            StartTimeUtc = DateTime.UtcNow.AddHours(1),
            EndTimeUtc = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = "google:u1",
            TeamId = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        db.AppointmentParticipants.Add(new AppointmentParticipant
        {
            AppointmentId = appt.Id,
            UserId = "google:u2"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // Act: request details for existing appointment.
        var result = await controller.Details(appt.Id);

        // Assert model and participant picker contents.
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Appointment>(view.Model);
        Assert.Equal(appt.Id, model.Id);

        var potential = Assert.IsType<SelectList>((object)controller.ViewBag.PotentialParticipants);
        var ids = potential.Select(i => i.Value).ToList();
        Assert.Contains("google:u1", ids);
        Assert.DoesNotContain("google:u2", ids);
    }

    // Create (GET) returns form view with user/team dropdown data.
    [Fact]
    public async Task AppointmentsControllerTest4()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);
        var controller = CreateController(db);

        var result = await controller.Create();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.Model);
        Assert.NotNull(controller.ViewBag.Users);
        Assert.NotNull(controller.ViewBag.Teams);
    }

    // Create (POST) forbids when caller has no provider user id claim.
    [Fact]
    public async Task AppointmentsControllerTest5()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);
        var controller = CreateController(db, user: new ClaimsPrincipal(new ClaimsIdentity()));

        var form = new Appointment
        {
            Title = "Create Attempt",
            StartTimeUtc = DateTime.UtcNow.AddHours(1),
            EndTimeUtc = DateTime.UtcNow.AddHours(2),
            TeamId = 1
        };

        var result = await controller.Create(form);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, await db.Appointments.CountAsync());
    }

    // Create (POST) returns view when end time is before start time.
    [Fact]
    public async Task AppointmentsControllerTest6()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);
        var controller = CreateController(db, BuildPrincipal("u1"));

        var form = new Appointment
        {
            Title = "Invalid Window",
            StartTimeUtc = DateTime.UtcNow.AddHours(2),
            EndTimeUtc = DateTime.UtcNow.AddHours(1),
            TeamId = 1
        };

        var result = await controller.Create(form);

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<Appointment>(view.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(0, await db.Appointments.CountAsync());
    }

    // Create (POST) saves valid appointment and redirects to details.
    [Fact]
    public async Task AppointmentsControllerTest7()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);
        var controller = CreateController(db, BuildPrincipal("u1"));

        var form = new Appointment
        {
            Title = "Good Appointment",
            Description = "Planning",
            StartTimeUtc = DateTime.UtcNow.AddHours(1),
            EndTimeUtc = DateTime.UtcNow.AddHours(2),
            TeamId = 1,
            Status = AppointmentStatus.Scheduled
        };

        var result = await controller.Create(form);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var saved = await db.Appointments.SingleAsync();
        Assert.Equal("google:u1", saved.CreatedByUserId);
        Assert.Equal("Good Appointment", saved.Title);
        Assert.NotEqual(default, saved.CreatedAtUtc);
        Assert.NotEqual(default, saved.UpdatedAtUtc);
    }

    // Edit (GET) returns NotFound when id is missing.
    [Fact]
    public async Task AppointmentsControllerTest8()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Edit(null);

        Assert.IsType<NotFoundResult>(result);
    }

    // Edit (GET) returns existing appointment and dropdown data.
    [Fact]
    public async Task AppointmentsControllerTest9()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);

        var appt = new Appointment
        {
            Title = "Editable",
            StartTimeUtc = DateTime.UtcNow.AddHours(1),
            EndTimeUtc = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = "google:u1",
            TeamId = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Edit(appt.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Appointment>(view.Model);
        Assert.Equal(appt.Id, model.Id);
        Assert.NotNull(controller.ViewBag.Users);
        Assert.NotNull(controller.ViewBag.Teams);
    }

    // Edit (POST) returns NotFound when route id and model id do not match.
    [Fact]
    public async Task AppointmentsControllerTest10()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var form = new Appointment
        {
            Id = 99,
            Title = "Mismatch",
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow.AddHours(1)
        };

        var result = await controller.Edit(1, form);

        Assert.IsType<NotFoundResult>(result);
    }

    // Edit (POST) updates fields and bumps UpdatedAt timestamp.
    [Fact]
    public async Task AppointmentsControllerTest11()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);

        var appt = new Appointment
        {
            Title = "To Update",
            Description = "Before",
            StartTimeUtc = DateTime.UtcNow.AddHours(1),
            EndTimeUtc = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = "google:u1",
            TeamId = 1,
            Status = AppointmentStatus.Scheduled,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var originalUpdatedAt = appt.UpdatedAtUtc;

        var form = new Appointment
        {
            Id = appt.Id,
            Title = "Updated",
            Description = "After",
            StartTimeUtc = appt.StartTimeUtc.AddHours(1),
            EndTimeUtc = appt.EndTimeUtc.AddHours(1),
            TeamId = null,
            Status = AppointmentStatus.Completed
        };

        var result = await controller.Edit(appt.Id, form);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);

        var updated = await db.Appointments.SingleAsync(a => a.Id == appt.Id);
        Assert.Equal("Updated", updated.Title);
        Assert.Equal("After", updated.Description);
        Assert.Equal(AppointmentStatus.Completed, updated.Status);
        Assert.Null(updated.TeamId);
        Assert.True(updated.UpdatedAtUtc >= originalUpdatedAt);
    }

    // Delete (GET) returns NotFound when id is missing.
    [Fact]
    public async Task AppointmentsControllerTest12()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Delete(null);

        Assert.IsType<NotFoundResult>(result);
    }

    // DeleteConfirmed removes appointment and related participants.
    [Fact]
    public async Task AppointmentsControllerTest13()
    {
        using var db = CreateInMemoryDb();
        SeedBasicUsersAndTeams(db);

        var appt = new Appointment
        {
            Title = "To Delete",
            StartTimeUtc = DateTime.UtcNow.AddHours(1),
            EndTimeUtc = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = "google:u1",
            TeamId = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        db.AppointmentParticipants.Add(new AppointmentParticipant
        {
            AppointmentId = appt.Id,
            UserId = "google:u2"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.DeleteConfirmed(appt.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(0, await db.Appointments.CountAsync());
        Assert.Equal(0, await db.AppointmentParticipants.CountAsync());
    }

    private static AppointlyContext CreateInMemoryDb()
    {
        // Use a unique in-memory database per test to avoid cross-test contamination.
        var options = new DbContextOptionsBuilder<AppointlyContext>()
            .UseInMemoryDatabase($"appointments-tests-{Guid.NewGuid()}")
            .Options;

        return new AppointlyContext(options);
    }

    private static AppointmentsController CreateController(AppointlyContext db, ClaimsPrincipal? user = null)
    {
        var controller = new AppointmentsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? BuildPrincipal("u1")
                }
            }
        };

        return controller;
    }

    private static ClaimsPrincipal BuildPrincipal(string providerUserId)
    {
        // Controller reads NameIdentifier as the external provider user id.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, providerUserId)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static void SeedBasicUsersAndTeams(AppointlyContext db)
    {
        // Minimal related entities used by create/edit/detail actions.
        db.Users.AddRange(
            new User { Id = "google:u1", DisplayName = "Ada" },
            new User { Id = "google:u2", DisplayName = "Ben" });

        db.Teams.Add(new Team { Id = 1, Name = "Core Team" });
        db.SaveChanges();
    }
}
