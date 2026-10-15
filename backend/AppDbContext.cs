using Microsoft.EntityFrameworkCore;
using MinimalApiProject.Models;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Meeting> Meetings { get; set; }
    public DbSet<PriestAvailabilityInput> PriestAvailabilities { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<Appointment> Appointments { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // These DateTimes are church wall-clock times with no real timezone
        // meaning, and are built with DateTimeKind.Unspecified/Local (e.g.
        // DateTime.Today). Npgsql's default "timestamp with time zone" only
        // accepts Utc, so map them to the timezone-less Postgres type instead.
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
    }
}