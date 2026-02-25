//standar crud for Users. 
//views: index() -> lists all users
//       details(id) -> details for one user. nice to have: teams the belong to?, appointments participated in?, appointments user crated?
//       create() -> create new user
//       edit(id) -> edit existing user
//       delete(id) -> delete user
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using appointly.Models;
using Microsoft.VisualBasic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.Marshalling;
namespace appointly.Controllers;

public class UsersController : Controller
{
    private readonly AppointlyContext _db;

    public UsersController(AppointlyContext db)
    {
        _db = db;
    }

    //GET: /Users
    public async Task<IActionResult> Index()
    {
        var users = await _db.Users.ToListAsync();
        return View(users);
    }

    //GET:/Users/Details/{id}
    public async Task<IActionResult> Details(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var user = await _db.Users
            .Include(u => u.TeamMemberships).ThenInclude(tm => tm.Team)
            .Include(u => u.AppointmentParticipations).ThenInclude(ap => ap.Appointment)
            .Include(u => u.CreatedAppointments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();
        return View(user);
    }

    //GET: /Users/Create
    public IActionResult Create()
    {
        return View();
    }

    //POST: /Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(User user)
    {
        if (ModelState.IsValid)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(user);
    }

    //GET: /Users/Edit/{id}
    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        return View(user);
    }


    //POST: /Users/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, User user)
    {
        if (id != user.Id) return NotFound();
        if (!ModelState.IsValid) return View(user);

        _db.Update(user);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    //GET: /Users/Delete/{id}
    public async Task<IActionResult> Delete(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        return View(user);
    }

    //POST: /Users/Delete/{id}
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        // Remove join rows first to avoid foreign key issues
        var teamMemberships = _db.TeamMembers.Where(tm => tm.UserId == id);
        _db.TeamMembers.RemoveRange(teamMemberships);

        var appointmentLinks = _db.AppointmentParticipants.Where(ap => ap.UserId == id);
        _db.AppointmentParticipants.RemoveRange(appointmentLinks);

        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            _db.Users.Remove(user);
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }




}