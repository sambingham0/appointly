namespace appointly.Models;

public class Team
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}