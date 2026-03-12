using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using appointly.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace appointly.Controllers;


public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppointlyContext _db;

    public HomeController(ILogger<HomeController> logger, AppointlyContext db)
    {
        _logger = logger;
        _db = db;
    }
    private string? CurrentUserId()
    {
        var providerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(providerUserId)) return null;
        return $"google:{providerUserId}";
    }


    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId();
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayUtc = nowLocal.Date.ToUniversalTime();
        var tomorrowUtc = todayUtc.AddDays(1);
        var todayAppointments = await _db.Appointments
            .Include(a => a.Team)
            .Where(a =>
                (a.CreatedByUserId == userId || a.Participants.Any(p => p.UserId == userId)) &&
                a.StartTimeUtc >= todayUtc &&
                a.StartTimeUtc < tomorrowUtc)
            .OrderBy(a => a.StartTimeUtc)
            .ToListAsync();

        ViewBag.TodayAppointments = todayAppointments;
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
