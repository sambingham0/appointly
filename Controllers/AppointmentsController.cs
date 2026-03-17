//standar crud for Appointments
//Index() - list appointments (usually include Team and CreatedByUser)
//Details(id) - show appointment info + created by user. team?, list of participants?, UI for adding/removing participants
//Create() - create new appointment
//Edit(id) - edit existing appointment
//Delete(id) - delete appointment

using appointly.Extensions;
using appointly.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace appointly.Controllers;

[Authorize]
public class AppointmentsController : Controller
{
    private readonly AppointlyContext _db;

    public AppointmentsController(AppointlyContext db)
    {
        _db = db;
    }

    // =========================
    // INDEX
    // =========================
    public async Task<IActionResult> Index()
    {
        var currentUserId = User.GetAppointlyUserId();
        var appointments = await _db.Appointments
            .Include(a => a.CreatedByUser)
            .Include(a => a.Team)
            .OrderByDescending(a => a.StartTimeUtc)
            .ToListAsync();

        var adminTeamIds = string.IsNullOrWhiteSpace(currentUserId)
            ? new HashSet<int>()
            : await _db.TeamMembers
                .Where(tm => tm.UserId == currentUserId && tm.Role == TeamRole.Admin)
                .Select(tm => tm.TeamId)
                .ToHashSetAsync();

        ViewBag.ManageableAppointmentIds = appointments
            .Where(a => a.CreatedByUserId == currentUserId || (a.TeamId.HasValue && adminTeamIds.Contains(a.TeamId.Value)))
            .Select(a => a.Id)
            .ToHashSet();

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

        var currentUserId = User.GetAppointlyUserId();
        ViewBag.CanManageAppointment = !string.IsNullOrWhiteSpace(currentUserId)
            && await _db.CanManageAppointmentAsync(appointment.Id, currentUserId);

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
    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync(User.GetAppointlyUserId());
        return View();
    }

    // =========================
    // CREATE (POST)
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Appointment appointment)
    {
        var currentUserId = User.GetAppointlyUserId();
        if (string.IsNullOrWhiteSpace(currentUserId)) return Forbid();

        if (appointment.EndTimeUtc <= appointment.StartTimeUtc)
        {
            ModelState.AddModelError("", "End time must be after start time.");
        }

        if (appointment.TeamId.HasValue && !await _db.IsTeamAdminAsync(appointment.TeamId.Value, currentUserId))
        {
            ModelState.AddModelError(nameof(appointment.TeamId), "You must be a team admin to create an appointment for that team.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(currentUserId, appointment.TeamId);
            return View(appointment);
        }

        appointment.CreatedByUserId = currentUserId;
        appointment.CreatedAtUtc = DateTime.UtcNow;
        appointment.UpdatedAtUtc = DateTime.UtcNow;

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = appointment.Id });
    }

    // =========================
    // EDIT (GET)
    // =========================
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var currentUserId = User.GetAppointlyUserId();
        if (string.IsNullOrWhiteSpace(currentUserId) || !await _db.CanManageAppointmentAsync(id.Value, currentUserId))
        {
            return Forbid();
        }

        var appointment = await _db.Appointments.FindAsync(id.Value);
        if (appointment == null) return NotFound();

        await PopulateDropdownsAsync(currentUserId, appointment.TeamId, appointment.TeamId);
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

        var currentUserId = User.GetAppointlyUserId();
        if (string.IsNullOrWhiteSpace(currentUserId) || !await _db.CanManageAppointmentAsync(id, currentUserId))
        {
            return Forbid();
        }

        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        if (formModel.EndTimeUtc <= formModel.StartTimeUtc)
        {
            ModelState.AddModelError("", "End time must be after start time.");
        }

        if (formModel.TeamId != appointment.TeamId &&
            formModel.TeamId.HasValue &&
            !await _db.IsTeamAdminAsync(formModel.TeamId.Value, currentUserId))
        {
            ModelState.AddModelError(nameof(formModel.TeamId), "You must be a team admin to assign this appointment to that team.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync(currentUserId, formModel.TeamId, appointment.TeamId);
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

        return RedirectToAction(nameof(Details), new { id });
    }

    // =========================
    // DELETE (GET)
    // =========================
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var currentUserId = User.GetAppointlyUserId();
        if (string.IsNullOrWhiteSpace(currentUserId) || !await _db.CanManageAppointmentAsync(id.Value, currentUserId))
        {
            return Forbid();
        }

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
        var currentUserId = User.GetAppointlyUserId();
        if (string.IsNullOrWhiteSpace(currentUserId) || !await _db.CanManageAppointmentAsync(id, currentUserId))
        {
            return Forbid();
        }

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
    private async Task PopulateDropdownsAsync(string? currentUserId, int? selectedTeamId = null, int? includeTeamId = null)
    {
        List<Team> teams = [];

        if (!string.IsNullOrWhiteSpace(currentUserId))
        {
            teams = await _db.Teams
                .Where(t => t.Members.Any(tm => tm.UserId == currentUserId && tm.Role == TeamRole.Admin))
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        if (includeTeamId.HasValue && teams.All(t => t.Id != includeTeamId.Value))
        {
            var existingTeam = await _db.Teams.FindAsync(includeTeamId.Value);
            if (existingTeam != null)
            {
                teams.Add(existingTeam);
                teams = teams.OrderBy(t => t.Name).ToList();
            }
        }

        ViewBag.Teams = new SelectList(
            teams,
            "Id",
            "Name",
            selectedTeamId
        );
    }
}