using System.Security.Claims;
using appointly.Controllers;
using appointly.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appointly.tests.UnitTests;

// Unit tests for AppointmentsController.cs covering common CRUD and query paths.
public class AppointmentsControllerTests
{
    // Index returns view with all appointments
    [Fact]
    public async Task AppointmentsControllerTest1()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Appointments.AddRange(
            BuildAppointment(1, "google:u1", "A", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)),
            BuildAppointment(2, "google:u1", "B", DateTime.UtcNow.AddHours(-4), DateTime.UtcNow.AddHours(-3)));
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Appointment>>(view.Model);
        Assert.Equal(2, model.Count());
    }

    // AppointmentsByUser returns appointments created by user or where user participates
    [Fact]
    public async Task AppointmentsControllerTest2()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        SeedUser(db, "google:u3", "User Three");

        db.Appointments.AddRange(
            BuildAppointment(1, "google:u1", "Owned", DateTime.UtcNow.AddHours(-4), DateTime.UtcNow.AddHours(-3)),
            BuildAppointment(2, "google:u2", "Participating", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)),
            BuildAppointment(3, "google:u2", "Excluded", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow));
        db.AppointmentParticipants.Add(new AppointmentParticipant { AppointmentId = 2, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipal("u1"));

        var result = await controller.AppointmentsByUser();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Appointment>>(view.Model).ToList();
        Assert.Equal(2, model.Count);
        Assert.Contains(model, a => a.Id == 1);
        Assert.Contains(model, a => a.Id == 2);
        Assert.DoesNotContain(model, a => a.Id == 3);
    }

    // Details with null id returns not found
    [Fact]
    public async Task AppointmentsControllerTest3()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Details(null);

        Assert.IsType<NotFoundResult>(result);
    }

    // Details for missing appointment returns not found
    [Fact]
    public async Task AppointmentsControllerTest4()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Details(999);

        Assert.IsType<NotFoundResult>(result);
    }

    // Details for existing appointment returns view and potential participants list
    [Fact]
    public async Task AppointmentsControllerTest5()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        SeedUser(db, "google:u3", "User Three");

        db.Appointments.Add(BuildAppointment(10, "google:u1", "Planning", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        db.AppointmentParticipants.Add(new AppointmentParticipant { AppointmentId = 10, UserId = "google:u2" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Details(10);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Appointment>(view.Model);
        Assert.Equal(10, model.Id);
        Assert.NotNull(controller.ViewBag.PotentialParticipants);
    }

    // Create GET parses start time and defaults end to one hour later
    [Fact]
    public async Task AppointmentsControllerTest6()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var start = "2026-03-18T14:00:00Z";
        var result = await controller.Create(start);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Appointment>(view.Model);
        Assert.Equal(DateTime.Parse(start).ToUniversalTime(), model.StartTimeUtc);
        Assert.Equal(DateTime.Parse(start).AddHours(1).ToUniversalTime(), model.EndTimeUtc);
    }

    // Create POST without authenticated user forbids
    [Fact]
    public async Task AppointmentsControllerTest7()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, new ClaimsPrincipal(new ClaimsIdentity()));

        var form = BuildAppointment(0, "", "No User", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var result = await controller.Create(form);

        Assert.IsType<ForbidResult>(result);
    }

    // Create POST with invalid times returns view and does not save
    [Fact]
    public async Task AppointmentsControllerTest8()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        var controller = CreateController(db, BuildPrincipal("u1"));

        var form = BuildAppointment(0, "google:u1", "Invalid", DateTime.UtcNow, DateTime.UtcNow.AddHours(-1));
        var result = await controller.Create(form);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(form, view.Model);
        Assert.Empty(db.Appointments);
    }

    // Create POST with valid model saves and redirects to index
    [Fact]
    public async Task AppointmentsControllerTest9()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        var controller = CreateController(db, BuildPrincipal("u1"));

        var now = DateTime.UtcNow;
        var form = BuildAppointment(0, "", "Created", now, now.AddHours(1));

        var result = await controller.Create(form);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        var saved = await db.Appointments.SingleAsync();
        Assert.Equal("google:u1", saved.CreatedByUserId);
        Assert.Equal("Created", saved.Title);
    }

    // Edit GET with null id returns not found
    [Fact]
    public async Task AppointmentsControllerTest10()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Edit((int?)null);

        Assert.IsType<NotFoundResult>(result);
    }

    // Edit GET with missing appointment returns not found
    [Fact]
    public async Task AppointmentsControllerTest11()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var result = await controller.Edit(404);

        Assert.IsType<NotFoundResult>(result);
    }

    // Edit GET with existing appointment returns view
    [Fact]
    public async Task AppointmentsControllerTest12()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Appointments.Add(BuildAppointment(5, "google:u1", "Edit", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Edit(5);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<Appointment>(view.Model);
        Assert.Equal(5, model.Id);
    }

    // Edit POST with route id mismatch returns not found
    [Fact]
    public async Task AppointmentsControllerTest13()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var form = BuildAppointment(2, "google:u1", "Mismatch", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        var result = await controller.Edit(1, form);

        Assert.IsType<NotFoundResult>(result);
    }

    // Edit POST with invalid model returns view and does not persist edits
    [Fact]
    public async Task AppointmentsControllerTest14()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Appointments.Add(BuildAppointment(7, "google:u1", "Before", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var incoming = BuildAppointment(7, "google:u1", "After", DateTime.UtcNow, DateTime.UtcNow.AddHours(-2));

        var result = await controller.Edit(7, incoming);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(incoming, view.Model);
        var unchanged = await db.Appointments.SingleAsync(a => a.Id == 7);
        Assert.Equal("Before", unchanged.Title);
    }

    // Edit POST valid model updates appointment and redirects
    [Fact]
    public async Task AppointmentsControllerTest15()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Appointments.Add(BuildAppointment(8, "google:u1", "Before", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var incoming = BuildAppointment(8, "google:u1", "After", DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3));
        incoming.Description = "Updated";
        incoming.Status = AppointmentStatus.Completed;

        var result = await controller.Edit(8, incoming);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        var updated = await db.Appointments.SingleAsync(a => a.Id == 8);
        Assert.Equal("After", updated.Title);
        Assert.Equal("Updated", updated.Description);
        Assert.Equal(AppointmentStatus.Completed, updated.Status);
    }

    // Delete GET null and missing id return not found
    [Fact]
    public async Task AppointmentsControllerTest16()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var nullResult = await controller.Delete(null);
        var missingResult = await controller.Delete(999);

        Assert.IsType<NotFoundResult>(nullResult);
        Assert.IsType<NotFoundResult>(missingResult);
    }

    // DeleteConfirmed removes appointment and its participants
    [Fact]
    public async Task AppointmentsControllerTest17()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        db.Appointments.Add(BuildAppointment(20, "google:u1", "Delete", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        db.AppointmentParticipants.Add(new AppointmentParticipant { AppointmentId = 20, UserId = "google:u2" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.DeleteConfirmed(20);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.False(await db.Appointments.AnyAsync(a => a.Id == 20));
        Assert.False(await db.AppointmentParticipants.AnyAsync(ap => ap.AppointmentId == 20));
    }

    // GetAppointments returns JSON payload
    [Fact]
    public async Task AppointmentsControllerTest18()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        db.Appointments.Add(BuildAppointment(30, "google:u1", "Calendar", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = controller.GetAppointments();

        var json = Assert.IsType<JsonResult>(result);
        Assert.NotNull(json.Value);
    }

    private static AppointmentsController CreateController(AppointlyContext db, ClaimsPrincipal? user = null)
    {
        var controller = new AppointmentsController(db)
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

    private static ClaimsPrincipal BuildPrincipal(string providerUserId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, providerUserId)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static Appointment BuildAppointment(int id, string createdByUserId, string title, DateTime startUtc, DateTime endUtc)
    {
        return new Appointment
        {
            Id = id,
            Title = title,
            CreatedByUserId = createdByUserId,
            StartTimeUtc = startUtc,
            EndTimeUtc = endUtc,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
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
            .UseInMemoryDatabase($"appointments-tests-{Guid.NewGuid()}")
            .Options;

        return new AppointlyContext(options);
    }
}
