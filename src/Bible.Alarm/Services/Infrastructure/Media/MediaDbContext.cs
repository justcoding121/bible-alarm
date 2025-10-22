using Bible.Alarm.Models.Media.Music;
using Bible.Alarm.Shared.Models;
using Bible.Alarm.Shared.Models.Bible;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Services.Infrastructure.Media;

public class MediaDbContext : DbContext
{
    public MediaDbContext()
    {
    }

    public MediaDbContext(DbContextOptions<MediaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Language> Languages { get; set; }
    public DbSet<AudioSource> AudioSources { get; set; }

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