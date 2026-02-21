// assuming google oidc for auth

namespace appointly.Models;

public class User
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // for display - not sending notifications. Optional just in case we can't get it from the auth service
    public string? Email { get; set; }

    // Navigation properties
    public ICollection<Appointment> CreatedAppointments { get; set; } = new List<Appointment>();
    public ICollection<AppointmentParticipant> AppointmentParticipations { get; set; } = new List<AppointmentParticipant>();
    public ICollection<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();
}