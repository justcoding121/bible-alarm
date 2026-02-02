using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bible.Alarm.Shared.Database;

public class MediaDbContext : DbContext
{
    public MediaDbContext() { }

    public MediaDbContext(DbContextOptions<MediaDbContext> options)
        : base(options)
    {
    }

    public DbSet<Language> Languages { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<BaseUrl> BaseUrls { get; set; }

    public DbSet<BiblePublication> BiblePublications { get; set; }
    public DbSet<BiblePublicationSection> BiblePublicationSections { get; set; }
    public DbSet<BiblePublicationTrack> BiblePublicationTracks { get; set; }
    public DbSet<PublicationLanguage> PublicationLanguages { get; set; }
    public DbSet<SectionLanguage> SectionLanguages { get; set; }
    public DbSet<UrlParam> UrlParams { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Suppress pending model changes warning - migrations are only needed for users upgrading from older app versions
        // The app is packaged with the latest database schema, so pending changes are expected during migration
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

        //#if DEBUG
        //            optionsBuilder.UseSqlite("DataSource=media_migration.db");
        //#endif
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UrlParams are only stored for tracks (complete params needed to fetch that track)
        // Sections and publications build URLs in code using harvest-type logic
        modelBuilder.Entity<UrlParam>()
            .HasOne(up => up.BiblePublicationTrack)
            .WithMany(bt => bt.UrlParams)
            .HasForeignKey(up => up.BiblePublicationTrackId)
            .IsRequired(false);

        modelBuilder.Entity<UrlParam>()
            .HasOne(up => up.BaseUrl)
            .WithMany(bu => bu.UrlParams)
            .HasForeignKey(up => up.BaseUrlId)
            .IsRequired(false);

        // Language relationship is already optional (LanguageId is nullable)

        // Ensure Language.LanguageCode has a unique index (also defined via [Index] attribute on model)
        modelBuilder.Entity<Language>()
            .HasIndex(l => l.LanguageCode)
            .IsUnique();

        // Configure PublicationLanguage relationships
        // Language relationship is optional (LanguageId can be null for publications without language, e.g., instrumental music)
        modelBuilder.Entity<PublicationLanguage>()
            .HasOne(pl => pl.Language)
            .WithMany()
            .HasForeignKey(pl => pl.LanguageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure SectionLanguage relationships
        // Language relationship is optional (LanguageId can be null for sections of publications without language)
        modelBuilder.Entity<SectionLanguage>()
            .HasOne(sl => sl.Language)
            .WithMany()
            .HasForeignKey(sl => sl.LanguageId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // SectionLanguage has a required relationship to PublicationLanguage
        // If a section language exists, the publication language must exist
        // Each publication can have many sections
        modelBuilder.Entity<SectionLanguage>()
            .HasOne(sl => sl.PublicationLanguage)
            .WithMany(pl => pl.SectionLanguages)
            .HasForeignKey(sl => sl.PublicationLanguageId)
            .OnDelete(DeleteBehavior.Cascade);
    }

}
