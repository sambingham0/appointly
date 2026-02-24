using Microsoft.EntityFrameworkCore;
using appointly.Models;

namespace appointly;

public class AppointlyContext : DbContext
{
    public AppointlyContext(
        DbContextOptions<AppointlyContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentParticipant> AppointmentParticipants => Set<AppointmentParticipant>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Users and Appointments have a many to many relationship
    modelBuilder.Entity<AppointmentParticipant>()
        .HasKey(ap => new { ap.AppointmentId, ap.UserId });

    // Each AppointmentParticipant connects one user to one appointment
    modelBuilder.Entity<AppointmentParticipant>()
        .HasOne(ap => ap.Appointment)
        .WithMany(a => a.Participants)
        .HasForeignKey(ap => ap.AppointmentId);

    modelBuilder.Entity<AppointmentParticipant>()
        .HasOne(ap => ap.User)
        .WithMany(u => u.AppointmentParticipations)
        .HasForeignKey(ap => ap.UserId);


    // Users and Teams have a many to many relationship
    modelBuilder.Entity<TeamMember>()
        .HasKey(tm => new { tm.TeamId, tm.UserId });

    // Each TeamMember connects one user to one team
    modelBuilder.Entity<TeamMember>()
        .HasOne(tm => tm.Team)
        .WithMany(t => t.Members)
        .HasForeignKey(tm => tm.TeamId);

    modelBuilder.Entity<TeamMember>()
        .HasOne(tm => tm.User)
        .WithMany(u => u.TeamMemberships)
        .HasForeignKey(tm => tm.UserId);


    // One user can create many appointments
    modelBuilder.Entity<Appointment>()
        .HasOne(a => a.CreatedByUser)
        .WithMany(u => u.CreatedAppointments)
        .HasForeignKey(a => a.CreatedByUserId)
        .OnDelete(DeleteBehavior.Restrict);

    // A team can have many appointments
    // This is optional because some appointments are just between users
    modelBuilder.Entity<Appointment>()
        .HasOne(a => a.Team)
        .WithMany(t => t.Appointments)
        .HasForeignKey(a => a.TeamId)
        .OnDelete(DeleteBehavior.SetNull);
}
}