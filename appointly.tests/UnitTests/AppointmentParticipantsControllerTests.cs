using appointly.Controllers;
using appointly.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appointly.tests.UnitTests;

// Unit tests for AppointmentParticipantsController.cs covering add/remove participant behavior.
public class AppointmentParticipantsControllerTests
{
    // Add returns not found when appointment does not exist.
    [Fact]
    public async Task AppointmentParticipantsControllerTest1()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Add(999, "google:u1");

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(db.AppointmentParticipants);
    }

    // Add returns not found when user does not exist.
    [Fact]
    public async Task AppointmentParticipantsControllerTest2()
    {
        using var db = CreateInMemoryDb();
        db.Appointments.Add(BuildAppointment(1, "google:u1", "Planning", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Add(1, "google:missing");

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(db.AppointmentParticipants);
    }

    // Add creates participant when one does not already exist.
    [Fact]
    public async Task AppointmentParticipantsControllerTest3()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        db.Appointments.Add(BuildAppointment(10, "google:u1", "Retro", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Add(10, "google:u2");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Appointments", redirect.ControllerName);
        Assert.Equal(10, redirect.RouteValues?["id"]);

        var participant = await db.AppointmentParticipants.SingleAsync();
        Assert.Equal(10, participant.AppointmentId);
        Assert.Equal("google:u2", participant.UserId);
    }

    // Add does not create duplicate participant rows.
    [Fact]
    public async Task AppointmentParticipantsControllerTest4()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        db.Appointments.Add(BuildAppointment(11, "google:u1", "Sprint", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        db.AppointmentParticipants.Add(new AppointmentParticipant { AppointmentId = 11, UserId = "google:u2" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Add(11, "google:u2");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Appointments", redirect.ControllerName);
        Assert.Equal(11, redirect.RouteValues?["id"]);
        Assert.Equal(1, await db.AppointmentParticipants.CountAsync(ap => ap.AppointmentId == 11 && ap.UserId == "google:u2"));
    }

    // Remove deletes existing participant and redirects.
    [Fact]
    public async Task AppointmentParticipantsControllerTest5()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        db.Appointments.Add(BuildAppointment(20, "google:u1", "Demo", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        db.AppointmentParticipants.Add(new AppointmentParticipant { AppointmentId = 20, UserId = "google:u2" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Remove(20, "google:u2");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Appointments", redirect.ControllerName);
        Assert.Equal(20, redirect.RouteValues?["id"]);
        Assert.False(await db.AppointmentParticipants.AnyAsync(ap => ap.AppointmentId == 20 && ap.UserId == "google:u2"));
    }

    // Remove is a no-op when participant does not exist.
    [Fact]
    public async Task AppointmentParticipantsControllerTest6()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");
        db.Appointments.Add(BuildAppointment(21, "google:u1", "Standup", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Remove(21, "google:u2");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("Appointments", redirect.ControllerName);
        Assert.Equal(21, redirect.RouteValues?["id"]);
        Assert.Empty(db.AppointmentParticipants);
    }

    private static AppointmentParticipantsController CreateController(AppointlyContext db)
    {
        return new AppointmentParticipantsController(db);
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
            .UseInMemoryDatabase($"appointment-participants-tests-{Guid.NewGuid()}")
            .Options;

        return new AppointlyContext(options);
    }
}
