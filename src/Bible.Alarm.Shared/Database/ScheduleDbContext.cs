using System.IO;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Database;

public class ScheduleDbContext : DbContext
{
    public ScheduleDbContext()
    {
    }

    public ScheduleDbContext(DbContextOptions<ScheduleDbContext> options)
        : base(options)
    {
    }

    public DbSet<AlarmSchedule> AlarmSchedules { get; set; }
    public DbSet<AlarmNotification> AlarmNotifications { get; set; }
    public DbSet<AlarmMusic> AlarmMusic { get; set; }
    public DbSet<BiblePublicationSchedule> BiblePublicationSchedules { get; set; }
    public DbSet<GeneralSettings> GeneralSettings { get; set; }

#if DEBUG
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Design-time configuration: Only configure if no options were provided
        // This allows EF Core tools (migrations, etc.) to work without requiring the full MAUI runtime
        // The design-time factory (ScheduleDbContextFactory) is preferred, but this provides a fallback
        if (!optionsBuilder.IsConfigured)
        {
            // For design-time, use a temporary database path in the project directory
            var dbPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                AppConstants.Database.ScheduleDatabaseFileName);

            var connectionString = string.Format(
                AppConstants.Database.ScheduleDatabaseConnectionStringFormat,
                dbPath);

            optionsBuilder.UseSqlite(connectionString);
        }
    }
#endif

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // This is the one-to-one relationship where CategoryId is null (Bible schedules)
        modelBuilder.Entity<BiblePublicationSchedule>()
            .HasOne(bps => bps.AlarmSchedule)
            .WithOne(aschedule => aschedule.BiblePublicationSchedule)
            .HasForeignKey<BiblePublicationSchedule>(bps => bps.AlarmScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure AlarmMusic relationship (obsolete, but kept for backward compatibility)
        modelBuilder.Entity<AlarmMusic>()
            .HasOne(am => am.AlarmSchedule)
            .WithOne(aschedule => aschedule.Music)
            .HasForeignKey<AlarmMusic>(am => am.AlarmScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // MusicBiblePublicationSchedule is marked as [NotMapped] and will be queried manually
        // using CategoryId filter, so no relationship configuration needed here
    }
}
