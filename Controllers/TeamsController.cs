using appointly.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appointly.Controllers;

[Authorize]
public class TeamsController : Controller
{
    private readonly AppointlyContext _db;

    public TeamsController(AppointlyContext db)
    {
        _db = db;
    }

    // GET: Teams
    public async Task<IActionResult> Index()
    {
        return View(await _db.Teams.ToListAsync());
    }

    // GET: Teams/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var team = await _db.Teams
            .Include(t => t.Members)
            .ThenInclude(tm => tm.User)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (team == null) return NotFound();

        // Pass a list of users not in the team you could add
        var existingMemberIds = team.Members.Select(m => m.UserId).ToList();
        var potentialMembers = await _db.Users
            .Where(u => !existingMemberIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .ToListAsync();
            
        ViewBag.PotentialMembers = potentialMembers;

        return View(team);
    }

    // GET: Teams/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Teams/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name")] Team team)
    {
        if (ModelState.IsValid)
        {
            _db.Add(team);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(team);
    }

    // GET: Teams/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var team = await _db.Teams.FindAsync(id);
        if (team == null) return NotFound();
        return View(team);
    }

    // POST: Teams/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Team team)
    {
        if (id != team.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _db.Update(team);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TeamExists(team.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(team);
    }

    // GET: Teams/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var team = await _db.Teams
            .FirstOrDefaultAsync(m => m.Id == id);
        if (team == null) return NotFound();

        return View(team);
    }

    // POST: Teams/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var team = await _db.Teams.FindAsync(id);
        if (team != null)
        {
            var members = _db.TeamMembers.Where(tm => tm.TeamId == id);
            _db.TeamMembers.RemoveRange(members);
            
            _db.Teams.Remove(team);
            await _db.SaveChangesAsync();
        }
        
        return RedirectToAction(nameof(Index));
    }

    private bool TeamExists(int id)
    {
        return _db.Teams.Any(e => e.Id == id);
    }
}