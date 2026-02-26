namespace appointly.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

public class Appointment
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Always store times in UTC — convert in the UI when displaying
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }

    // 3rd party auth typically uses a string ID
    public string CreatedByUserId { get; set; } = string.Empty;

    // Null means it's just between users, not tied to a team
    public int? TeamId { get; set; }

    // Current state of the appointment (defaults to Scheduled)
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    // Timestamps for record keeping idk
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties 
    [ValidateNever]
    public User CreatedByUser { get; set; } = null!;
    [ValidateNever]
    public Team? Team { get; set; }

    // Users invited to this appointment
    [ValidateNever]
    public ICollection<AppointmentParticipant> Participants { get; set; } = new List<AppointmentParticipant>();
}

public enum AppointmentStatus
{
    Scheduled = 0,
    Cancelled = 1,
    Completed = 2
}