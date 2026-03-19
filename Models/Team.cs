namespace appointly.Models;
using System.ComponentModel.DataAnnotations;

public class Team
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Team name is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Team name must be between 3 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}