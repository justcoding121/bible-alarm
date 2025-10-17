using Bible.Alarm.Models;
using Bible.Alarm.Models.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Services
{
    public class ScheduleDbContext : DbContext
    {
        public ScheduleDbContext() { }

        public ScheduleDbContext(DbContextOptions<ScheduleDbContext> options)
            : base(options)
        {
        }

        public DbSet<AlarmSchedule> AlarmSchedules { get; set; }
        public DbSet<AlarmNotification> AlarmNotifications { get; set; }
        public DbSet<AlarmMusic> AlarmMusic { get; set; }
        public DbSet<BibleReadingSchedule> BibleReadingSchedules { get; set; }
        public DbSet<GeneralSettings> GeneralSettings { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

//#if DEBUG
//            optionsBuilder.UseSqlite("DataSource=schedule_migration.db");
//#endif
        }
    }
}
