using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Database
{
    public class MediaDbContext : DbContext
    {
        public MediaDbContext() { }

        public MediaDbContext(DbContextOptions<MediaDbContext> options)
            : base(options)
        {
        }

        public DbSet<Language> Languages { get; set; }

        public DbSet<BibleTranslation> BibleTranslations { get; set; }
        public DbSet<BibleBook> BibleBook { get; set; }
        public DbSet<BibleChapter> BibleChapter { get; set; }
        public DbSet<MelodyMusic> MelodyMusic { get; set; }
        public DbSet<VocalMusic> VocalMusic { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            // Suppress pending model changes warning - migrations are only needed for users upgrading from older app versions
            // The app is packaged with the latest database schema, so pending changes are expected during migration
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

            //#if DEBUG
            //            optionsBuilder.UseSqlite("DataSource=media_migration.db");
            //#endif
        }

    }
}
