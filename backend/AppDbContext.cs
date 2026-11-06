using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MinimalApiProject.Models;

// IDataProtectionKeyContext: the auth cookie is encrypted/signed with a key
// ring that defaults to ephemeral, per-instance storage. A container
// platform that runs more than one instance (or recycles one) needs that
// key ring shared, or a cookie issued by one instance 401s on another —
// Postgres (already available) is the simplest shared store for it.
public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Meeting> Meetings { get; set; }
    public DbSet<PriestAvailabilityInput> PriestAvailabilities { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys { get; set; }

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