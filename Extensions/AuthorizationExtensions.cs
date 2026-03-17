using System.Security.Claims;
using appointly.Models;
using Microsoft.EntityFrameworkCore;

namespace appointly.Extensions;

public static class AuthorizationExtensions
{
    public static string? GetAppointlyUserId(this ClaimsPrincipal user)
    {
        var providerUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            return null;
        }

        return $"google:{providerUserId}";
    }

    public static Task<bool> IsTeamAdminAsync(this AppointlyContext db, int teamId, string userId)
    {
        return db.TeamMembers.AnyAsync(tm =>
            tm.TeamId == teamId &&
            tm.UserId == userId &&
            tm.Role == TeamRole.Admin);
    }

    public static async Task<bool> CanManageAppointmentAsync(this AppointlyContext db, int appointmentId, string userId)
    {
        var appointment = await db.Appointments
            .AsNoTracking()
            .Select(a => new { a.Id, a.CreatedByUserId, a.TeamId })
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment is null)
        {
            return false;
        }

        if (appointment.CreatedByUserId == userId)
        {
            return true;
        }

        if (!appointment.TeamId.HasValue)
        {
            return false;
        }

        return await db.IsTeamAdminAsync(appointment.TeamId.Value, userId);
    }
}