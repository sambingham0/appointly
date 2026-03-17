// join table for users and teams

namespace appointly.Models;

public class TeamMember
{
    public int TeamId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public TeamRole Role { get; set; } = TeamRole.Member;

    // Navigation properties
    public Team Team { get; set; } = null!;
    public User User { get; set; } = null!;
}

public enum TeamRole
{
    Member = 0,
    Admin = 1
}