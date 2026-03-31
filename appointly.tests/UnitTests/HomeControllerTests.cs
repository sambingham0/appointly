using System.Security.Claims;
using appointly.Controllers;
using appointly.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace appointly.tests.UnitTests;

// Unit tests for HomeController.cs covering dashboard, privacy, and error endpoints.
public class HomeControllerTests
{
    // Index returns today's appointments for current user (owned or participant) ordered by start time.
    [Fact]
    public async Task HomeControllerTest1()
    {
        using var db = CreateInMemoryDb();

        SeedUser(db, "google:u1", "User One");
        SeedUser(db, "google:u2", "User Two");

        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayUtc = nowLocal.Date.ToUniversalTime();
        var tomorrowUtc = todayUtc.AddDays(1);

        db.Appointments.AddRange(
            BuildAppointment(1, "google:u1", "Owned Today", todayUtc.AddHours(2), todayUtc.AddHours(3)),
            BuildAppointment(2, "google:u2", "Participant Today", todayUtc.AddHours(4), todayUtc.AddHours(5)),
            BuildAppointment(3, "google:u2", "Other User", todayUtc.AddHours(6), todayUtc.AddHours(7)),
            BuildAppointment(4, "google:u1", "Owned Tomorrow", tomorrowUtc.AddHours(2), tomorrowUtc.AddHours(3)));
        db.AppointmentParticipants.Add(new AppointmentParticipant { AppointmentId = 2, UserId = "google:u1" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipalWithNameIdentifier("u1"));

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        var todayAppointmentsObj = (object)controller.ViewBag.TodayAppointments;
        var todayAppointments = Assert.IsAssignableFrom<IEnumerable<Appointment>>(todayAppointmentsObj).ToList();

        Assert.Equal(2, todayAppointments.Count);
        Assert.Equal(1, todayAppointments[0].Id);
        Assert.Equal(2, todayAppointments[1].Id);
        Assert.DoesNotContain(todayAppointments, a => a.Id == 3);
        Assert.DoesNotContain(todayAppointments, a => a.Id == 4);
    }

    // Index with no authenticated user returns empty today appointments list.
    [Fact]
    public async Task HomeControllerTest2()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:u1", "User One");

        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayUtc = nowLocal.Date.ToUniversalTime();

        db.Appointments.Add(BuildAppointment(1, "google:u1", "Hidden", todayUtc.AddHours(1), todayUtc.AddHours(2)));
        await db.SaveChangesAsync();

        var controller = CreateController(db, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        var todayAppointmentsObj = (object)controller.ViewBag.TodayAppointments;
        var todayAppointments = Assert.IsAssignableFrom<IEnumerable<Appointment>>(todayAppointmentsObj);
        Assert.Empty(todayAppointments);
    }

    // Index falls back to sub claim when NameIdentifier is not present.
    [Fact]
    public async Task HomeControllerTest3()
    {
        using var db = CreateInMemoryDb();
        SeedUser(db, "google:sub-user", "Sub User");
        SeedUser(db, "google:u2", "User Two");

        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayUtc = nowLocal.Date.ToUniversalTime();

        db.Appointments.Add(BuildAppointment(1, "google:u2", "Participant Via Sub", todayUtc.AddHours(2), todayUtc.AddHours(3)));
        db.AppointmentParticipants.Add(new AppointmentParticipant { AppointmentId = 1, UserId = "google:sub-user" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, BuildPrincipalWithSub("sub-user"));

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        var todayAppointmentsObj = (object)controller.ViewBag.TodayAppointments;
        var todayAppointments = Assert.IsAssignableFrom<IEnumerable<Appointment>>(todayAppointmentsObj).ToList();

        Assert.Single(todayAppointments);
        Assert.Equal(1, todayAppointments[0].Id);
    }

    // Privacy returns a view.
    [Fact]
    public void HomeControllerTest4()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    // Error returns view with ErrorViewModel populated from HttpContext.TraceIdentifier.
    [Fact]
    public void HomeControllerTest5()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db, new ClaimsPrincipal(new ClaimsIdentity()));
        controller.ControllerContext.HttpContext.TraceIdentifier = "trace-123";

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal("trace-123", model.RequestId);
        Assert.True(model.ShowRequestId);
    }

    private static AppointlyContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppointlyContext>()
            .UseInMemoryDatabase($"home-tests-{Guid.NewGuid()}")
            .Options;

        return new AppointlyContext(options);
    }

    private static HomeController CreateController(AppointlyContext db, ClaimsPrincipal user)
    {
        var controller = new HomeController(NullLogger<HomeController>.Instance, db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            }
        };

        return controller;
    }

    private static void SeedUser(AppointlyContext db, string id, string name)
    {
        if (!db.Users.Any(u => u.Id == id))
        {
            db.Users.Add(new User
            {
                Id = id,
                DisplayName = name,
                Email = $"{name.Replace(" ", "").ToLowerInvariant()}@example.com"
            });
        }
    }

    private static ClaimsPrincipal BuildPrincipalWithNameIdentifier(string providerUserId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, providerUserId)
        ],
        authenticationType: "Test"));
    }

    private static ClaimsPrincipal BuildPrincipalWithSub(string providerUserId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", providerUserId)
        ],
        authenticationType: "Test"));
    }

    private static Appointment BuildAppointment(int id, string createdByUserId, string title, DateTime startUtc, DateTime endUtc)
    {
        return new Appointment
        {
            Id = id,
            Title = title,
            CreatedByUserId = createdByUserId,
            StartTimeUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
            EndTimeUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
