namespace appointly.Models;

public class TeamMember
{
    public int TeamId { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Navigation properties
    public Team Team { get; set; } = null!;
    public User User { get; set; } = null!;
}