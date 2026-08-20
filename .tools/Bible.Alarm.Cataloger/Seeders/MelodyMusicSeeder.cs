#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using BiblePublicationCategory = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationCategory;
using SharedBiblePublicationSection = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection;
using SharedBiblePublicationTrack = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack;

namespace Bible.Alarm.Cataloger.Seeders;

internal sealed class MelodyMusicSeeder
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly InMemoryDataStore dataStore;

    public MelodyMusicSeeder(ILogger logger, IServiceScopeFactory scopeFactory, InMemoryDataStore dataStore)
    {
        this.logger = logger;
        this.scopeFactory = scopeFactory;
        this.dataStore = dataStore;
    }

    /// <summary>
    /// Seeds MelodyMusic publications (instrumental music without language) to the database.
    /// Each disc becomes a section, and tracks within each disc become tracks under that section.
    /// </summary>
    public async Task SeedMelodyMusic()
    {
        if (dataStore.MelodyMusic.IsEmpty)
        {
            logger.Information("No MelodyMusic publications to seed");
            return;
        }

        logger.Information("=== Seeding MelodyMusic publications ===");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var musicCategory = await db.Categories
            .FirstOrDefaultAsync(c => c.CategoryCode == "Music");

        if (musicCategory == null)
        {
            logger.Error("Music category not found in database");
            return;
        }

        foreach (var kvp in dataStore.MelodyMusic)
        {
            await TrySeedMelodyMusicPublicationAsync(db, musicCategory, kvp.Key, kvp.Value);
        }

        logger.Information("=== MelodyMusic seeding completed ===");
    }

    private async Task TrySeedMelodyMusicPublicationAsync(
        MediaDbContext db,
        Bible.Alarm.Shared.Models.Media.Category musicCategory,
        string publicationCode,
        (Dictionary<string, List<MusicTrack>> DiscTracksMap, Dictionary<string, string> DiscNamesMap) melodyData)
    {
        var (discTracksMap, discNamesMap) = melodyData;

        if (discTracksMap.Count == 0)
        {
            logger.Warning("No discs found for MelodyMusic publication {PublicationCode}", publicationCode);
            return;
        }

        var existingPublication = await db.BiblePublications
            .Include(bp => bp.Sections)
            .FirstOrDefaultAsync(bp => bp.PublicationCode == publicationCode && bp.LanguageId == null);

        if (existingPublication != null)
        {
            logger.Information("MelodyMusic publication {PublicationCode} already exists, skipping", publicationCode);
            return;
        }

        var publicationName = ResolveMelodyPublicationDisplayName(publicationCode, discNamesMap);

        var biblePublication = new BiblePublication
        {
            PublicationCode = publicationCode.ToLowerInvariant(),
            Name = publicationName,
            LanguageId = null,
            BiblePublicationCategories = new List<BiblePublicationCategory> { new BiblePublicationCategory { CategoryId = musicCategory.Id, Category = musicCategory } },
            IsVideo = false,
            IsMusic = true,
            Sections = new List<SharedBiblePublicationSection>(),
            Tracks = new List<SharedBiblePublicationTrack>()
        };

        foreach (var discEntry in discTracksMap.OrderBy(d => d.Key))
        {
            AppendMelodyDiscSection(publicationCode, biblePublication, discEntry, discNamesMap);
        }

        if (biblePublication.Sections.Count == 0)
        {
            logger.Warning("No sections created for MelodyMusic publication {PublicationCode}", publicationCode);
            return;
        }

        db.BiblePublications.Add(biblePublication);
        await db.SaveChangesAsync();

        logger.Information("✓ Successfully seeded MelodyMusic publication {PublicationCode} with {SectionCount} sections and {TrackCount} total tracks",
            publicationCode, biblePublication.Sections.Count, biblePublication.Sections.Sum(s => s.Tracks.Count));
    }

    private static string ResolveMelodyPublicationDisplayName(string publicationCode, Dictionary<string, string> discNamesMap)
    {
        var publicationName = publicationCode.ToUpperInvariant();
        if (discNamesMap.Count == 0)
        {
            return publicationName;
        }

        var firstDiscName = discNamesMap.Values.First();
        if (string.IsNullOrEmpty(firstDiscName))
        {
            return publicationName;
        }

        if (firstDiscName.Contains(AppConstants.Media.PublicationDisplayNameKingdomMelodies, StringComparison.OrdinalIgnoreCase))
        {
            return AppConstants.Media.PublicationDisplayNameKingdomMelodies;
        }

        return firstDiscName;
    }

    private static void AppendMelodyDiscSection(
        string publicationCode,
        BiblePublication biblePublication,
        KeyValuePair<string, List<MusicTrack>> discEntry,
        Dictionary<string, string> discNamesMap)
    {
        var discCode = discEntry.Key;
        var discTracks = discEntry.Value;

        if (discTracks.Count == 0)
        {
            return;
        }

        var sectionName = discNamesMap.TryGetValue(discCode, out var name) && !string.IsNullOrEmpty(name)
            ? name
            : discCode;

        var section = new SharedBiblePublicationSection
        {
            Name = sectionName,
            SectionCode = discCode.ToLowerInvariant(),
            BiblePublication = biblePublication,
            BiblePublicationId = 0,
            Tracks = new List<SharedBiblePublicationTrack>()
        };

        foreach (var musicTrack in discTracks.OrderBy(t => t.Number))
        {
            var trackCode = (musicTrack.OriginalTrackCode ?? musicTrack.Number).ToString(System.Globalization.CultureInfo.InvariantCulture);

            var trackTitle = musicTrack.Title;
            if (publicationCode.Equals(AppConstants.Media.MelodyMusicPublicationCodeIam, StringComparison.OrdinalIgnoreCase))
            {
                trackTitle = $"Melody Number(s) {trackTitle}";
            }

            var track = new SharedBiblePublicationTrack
            {
                TrackCode = trackCode,
                Title = trackTitle,
                Section = section,
                BiblePublicationSectionId = 0,
                Publication = biblePublication,
                BiblePublicationId = 0,
                TrackUrl = !string.IsNullOrEmpty(musicTrack.Url) ? new TrackUrl { Url = musicTrack.Url } : null
            };

            section.Tracks.Add(track);
        }

        biblePublication.Sections.Add(section);
    }
}

