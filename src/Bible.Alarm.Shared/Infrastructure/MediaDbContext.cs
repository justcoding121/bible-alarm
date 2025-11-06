using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Infrastructure
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

//#if DEBUG
//            optionsBuilder.UseSqlite("DataSource=media_migration.db");
//#endif
        }

    }
}
