//standar crud for Appointments
//Index() - list appointments (usually include Team and CreatedByUser)
//Details(id) - show appointment info + created by user. team?, list of participants?, UI for adding/removing participants
//Create() - create new appointment
//Edit(id) - edit existing appointment
//Delete(id) - delete appointment

using appointly.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace appointly.Controllers;

[Authorize]
public class AppointmentsController : Controller
{
    private readonly AppointlyContext _db;

    public AppointmentsController(AppointlyContext db)
    {
        _db = db;
    }

    //might need to update this 
    private string? CurrentUserId()
    {
        var providerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(providerUserId)) return null;
        return $"google:{providerUserId}";
    }

    // =========================
    // INDEX
    // =========================
    public async Task<IActionResult> Index()
    {
        var appointments = await _db.Appointments
            .Include(a => a.CreatedByUser)
            .Include(a => a.Team)
            .OrderByDescending(a => a.StartTimeUtc)
            .ToListAsync();

        return View(appointments);
    }

    // =========================
    // INDEX BY USER (personal view)
    // =========================
    public async Task<IActionResult> AppointmentsByUser()
    {
        var userId = CurrentUserId();

        var appointments = await _db.Appointments
            .Include(a => a.CreatedByUser)
            .Include(a => a.Team)
            .Where(a => a.CreatedByUserId == userId ||
                        a.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(a => a.StartTimeUtc)
            .ToListAsync();

        return View(appointments);
    }

    // =========================
    // DETAILS (includes participants management)
    // =========================
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var appointment = await _db.Appointments
            .Include(a => a.CreatedByUser)
            .Include(a => a.Team)
            .Include(a => a.Participants)
                .ThenInclude(ap => ap.User)
            .FirstOrDefaultAsync(a => a.Id == id.Value);

        if (appointment == null) return NotFound();

        var currentParticipantIds = appointment.Participants.Select(p => p.UserId).ToHashSet();

        var potentialParticipants = await _db.Users
            .Where(u => !currentParticipantIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        ViewBag.PotentialParticipants = new SelectList(potentialParticipants, "Id", "DisplayName");

        return View(appointment);
    }

    // =========================
    // CREATE (GET)
    // =========================
    public async Task<IActionResult> Create(string? startTime)
    {
        var model = new Appointment();

        if (!string.IsNullOrEmpty(startTime) && DateTime.TryParse(startTime, out var parsed))
        {
            model.StartTimeUtc = parsed.ToUniversalTime();
            model.EndTimeUtc = parsed.AddHours(1).ToUniversalTime();
        }

        return View(model);
    }

    // =========================
    // CREATE (POST)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Appointment appointment)
    {
        var currentUserId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(currentUserId)) return Forbid();

        if (appointment.EndTimeUtc <= appointment.StartTimeUtc)
        {
            ModelState.AddModelError("", "End time must be after start time.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(appointment.CreatedByUserId, appointment.TeamId);
            return View(appointment);
        }

        appointment.CreatedByUserId = currentUserId;
        appointment.CreatedAtUtc = DateTime.UtcNow;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        // return RedirectToAction(nameof(Details), new { id = appointment.Id });

        return RedirectToAction(nameof(Index));
    }

    // =========================
    // EDIT (GET)
    // =========================
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var appointment = await _db.Appointments.FindAsync(id.Value);
        if (appointment == null) return NotFound();

        await PopulateDropdownsAsync(appointment.CreatedByUserId, appointment.TeamId);
        return View(appointment);
    }

    // =========================
    // EDIT (POST)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Appointment formModel)
    {
        if (id != formModel.Id) return NotFound();

        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        if (formModel.EndTimeUtc <= formModel.StartTimeUtc)
        {
            ModelState.AddModelError("", "End time must be after start time.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(formModel.CreatedByUserId, formModel.TeamId);
            return View(formModel);
        }


        appointment.Title = formModel.Title;
        appointment.Description = formModel.Description;
        appointment.StartTimeUtc = formModel.StartTimeUtc;
        appointment.EndTimeUtc = formModel.EndTimeUtc;
        appointment.Status = formModel.Status;
        appointment.TeamId = formModel.TeamId;

        appointment.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // =========================
    // DELETE (GET)
    // =========================
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var appointment = await _db.Appointments
            .Include(a => a.CreatedByUser)
            .Include(a => a.Team)
            .FirstOrDefaultAsync(a => a.Id == id.Value);

        if (appointment == null) return NotFound();

        return View(appointment);
    }

    // =========================
    // DELETE (POST)
    // =========================
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        // Remove participants first (to avoid foreign key issues)
        _db.AppointmentParticipants
            .RemoveRange(_db.AppointmentParticipants.Where(ap => ap.AppointmentId == id));

        _db.Appointments.Remove(appointment);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // =========================
    // Dropdown population
    // =========================
    private async Task PopulateDropdownsAsync(string? selectedUserId = null, int? selectedTeamId = null)
    {
        ViewBag.Users = new SelectList(
            await _db.Users.OrderBy(u => u.DisplayName).ToListAsync(),
            "Id",
            "DisplayName",
            selectedUserId
        );

        ViewBag.Teams = new SelectList(
            await _db.Teams.OrderBy(t => t.Name).ToListAsync(),
            "Id",
            "Name",
            selectedTeamId
        );
    }

    // =========================
    // JSON endpoint for calendar.io 
    // =========================
    public JsonResult GetAppointments()
    {
        var appointments = _db.Appointments.Select(a => new
        {
            id = a.Id,
            title = a.Title,
            start = a.StartTimeUtc.ToString("o"),
            end = a.EndTimeUtc.ToString("o")
        });

        return Json(appointments);
    }
}