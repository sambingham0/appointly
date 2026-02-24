// join table for Appointments and Users

namespace appointly.Models;

public class AppointmentParticipant
{
  public int AppointmentId { get; set; }
  public string UserId { get; set; } = string.Empty;

  // Navigation properties
  public Appointment Appointment { get; set; } = null!;
  public User User { get; set; } = null!;
}