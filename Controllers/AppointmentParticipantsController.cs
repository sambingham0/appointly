using appointly.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appointly.Controllers;

[Authorize]
public class AppointmentParticipantsController : Controller
{
    private readonly AppointlyContext _db;

    public AppointmentParticipantsController(AppointlyContext db)
    {
        _db = db;
    }

    // POST: AppointmentParticipants/Add?appointmentId=5&userId=abc
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int appointmentId, string userId)
    {
        var appointment = await _db.Appointments.FindAsync(appointmentId);
        var user = await _db.Users.FindAsync(userId);

        if (appointment == null || user == null)
        {
            return NotFound();
        }

        var exists = await _db.AppointmentParticipants
            .AnyAsync(ap => ap.AppointmentId == appointmentId && ap.UserId == userId);

        if (!exists)
        {
            var participant = new AppointmentParticipant
            {
                AppointmentId = appointmentId,
                UserId = userId
            };
            _db.AppointmentParticipants.Add(participant);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Details", "Appointments", new { id = appointmentId });
    }

    // POST: AppointmentParticipants/Remove?appointmentId=5&userId=abc
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int appointmentId, string userId)
    {
        var participant = await _db.AppointmentParticipants
            .FirstOrDefaultAsync(ap => ap.AppointmentId == appointmentId && ap.UserId == userId);

        if (participant != null)
        {
            _db.AppointmentParticipants.Remove(participant);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Details", "Appointments", new { id = appointmentId });
    }
}