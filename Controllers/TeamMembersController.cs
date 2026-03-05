using appointly.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appointly.Controllers;

[Authorize]
public class TeamMembersController : Controller
{
    private readonly AppointlyContext _db;

    public TeamMembersController(AppointlyContext db)
    {
        _db = db;
    }

    // POST: TeamMembers/Add?teamId=5&userId=abc
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int teamId, string userId)
    {
        var team = await _db.Teams.FindAsync(teamId);
        var user = await _db.Users.FindAsync(userId);

        if (team == null || user == null)
        {
            return NotFound();
        }

        var exists = await _db.TeamMembers
            .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId);

        if (!exists)
        {
            var member = new TeamMember
            {
                TeamId = teamId,
                UserId = userId
            };
            _db.TeamMembers.Add(member);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Details", "Teams", new { id = teamId });
    }

    // POST: TeamMembers/Remove?teamId=5&userId=abc
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int teamId, string userId)
    {
        var member = await _db.TeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId);

        if (member != null)
        {
            _db.TeamMembers.Remove(member);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction("Details", "Teams", new { id = teamId });
    }
}