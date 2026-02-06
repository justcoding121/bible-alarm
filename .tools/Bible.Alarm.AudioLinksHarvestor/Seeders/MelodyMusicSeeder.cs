#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SharedBiblePublicationSection = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationSection;
using SharedBiblePublicationTrack = Bible.Alarm.Shared.Models.Media.BiblePublications.BiblePublicationTrack;
using SharedUrlParam = Bible.Alarm.Shared.Models.Media.BiblePublications.UrlParam;

namespace Bible.Alarm.AudioLinksHarvestor.Seeders;

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
        if (dataStore.MelodyMusic.Count == 0)
        {
            logger.Information("No MelodyMusic publications to seed");
            return;
        }

        logger.Information("=== Seeding MelodyMusic publications ===");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Get Music category
        var musicCategory = await db.Categories
            .FirstOrDefaultAsync(c => c.CategoryName == "Music");

        if (musicCategory == null)
        {
            logger.Error("Music category not found in database");
            return;
        }

        // Get BaseUrl for creating UrlParams
        var baseUrl = await db.BaseUrls
            .FirstOrDefaultAsync(bu => bu.PathPrefix == "apis/pub-media/GETPUBMEDIALINKS");

        if (baseUrl == null)
        {
            logger.Error("BaseUrl not found for GETPUBMEDIALINKS");
            return;
        }

        foreach (var kvp in dataStore.MelodyMusic)
        {
            var publicationCode = kvp.Key;
            var (discTracksMap, discNamesMap) = kvp.Value;

            if (discTracksMap.Count == 0)
            {
                logger.Warning("No discs found for MelodyMusic publication {PublicationCode}", publicationCode);
                continue;
            }

            // Check if publication already exists
            var existingPublication = await db.BiblePublications
                .Include(bp => bp.Sections)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == publicationCode && bp.LanguageId == null);

            if (existingPublication != null)
            {
                logger.Information("MelodyMusic publication {PublicationCode} already exists, skipping", publicationCode);
                continue;
            }

            // Get publication name - try to get it from the first disc name, or use publication code
            var publicationName = publicationCode.ToUpperInvariant();
            if (discNamesMap.Count > 0)
            {
                var firstDiscName = discNamesMap.Values.First();
                if (!string.IsNullOrEmpty(firstDiscName))
                {
                    // For iam, the disc name might be something like "Kingdom Melodies 1"
                    // Try to extract a better publication name
                    if (firstDiscName.Contains("Kingdom Melodies", StringComparison.OrdinalIgnoreCase))
                    {
                        publicationName = "Kingdom Melodies";
                    }
                    else
                    {
                        publicationName = firstDiscName;
                    }
                }
            }

            // Create BiblePublication using shared model
            var biblePublication = new BiblePublication
            {
                PublicationCode = publicationCode.ToLowerInvariant(),
                Name = publicationName,
                LanguageId = null, // MelodyMusic has no language
                CategoryId = musicCategory.Id,
                Category = musicCategory,
                IsVideo = false,
                Sections = new List<SharedBiblePublicationSection>(),
                Tracks = new List<SharedBiblePublicationTrack>()
            };

            // Create sections from discs
            foreach (var discEntry in discTracksMap.OrderBy(d => d.Key))
            {
                var discCode = discEntry.Key;
                var discTracks = discEntry.Value;

                if (discTracks.Count == 0)
                {
                    continue;
                }

                // Get section name from discNamesMap, or use disc code
                var sectionName = discNamesMap.TryGetValue(discCode, out var name) && !string.IsNullOrEmpty(name)
                    ? name
                    : discCode;

                // Create section using shared model
                var section = new SharedBiblePublicationSection
                {
                    Name = sectionName,
                    SectionCode = discCode.ToLowerInvariant(),
                    BiblePublication = biblePublication,
                    BiblePublicationId = 0, // Will be set after publication is saved
                    Tracks = new List<SharedBiblePublicationTrack>()
                };

                // Create tracks for this section
                foreach (var musicTrack in discTracks.OrderBy(t => t.Number))
                {
                    // Create UrlParams for the track (similar to ParseIamTracks)
                    var trackUrlParams = new List<SharedUrlParam>
                    {
                        new SharedUrlParam
                        {
                            Key = "pub",
                            Value = discCode.ToLowerInvariant(), // Use disc code (e.g., "iam-1") as pub parameter
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new SharedUrlParam
                        {
                            Key = "fileformat",
                            Value = "mp3",
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new SharedUrlParam
                        {
                            Key = "langwritten",
                            Value = "E",
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        },
                        new SharedUrlParam
                        {
                            Key = "track",
                            Value = (musicTrack.OriginalTrackCode ?? musicTrack.Number).ToString(),
                            IsQueryParam = true,
                            BaseUrl = baseUrl,
                            BaseUrlId = baseUrl.Id
                        }
                    };

                    // For Kingdom Melodies (iam), prefix track title with "Melody Number(s) "
                    var trackTitle = musicTrack.Title;
                    if (publicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase))
                    {
                        trackTitle = $"Melody Number(s) {trackTitle}";
                    }

                    // TrackCode is the originalTrackCode from UrlParams (track param), or Number as string fallback
                    var trackCode = trackUrlParams.FirstOrDefault(p => p.Key == "track")?.Value ?? musicTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    
                    var track = new SharedBiblePublicationTrack
                    {
                        TrackCode = trackCode,
                        Title = trackTitle,
                        Section = section,
                        BiblePublicationSectionId = 0, // Will be set after section is saved
                        Publication = biblePublication,
                        BiblePublicationId = 0, // Will be set after publication is saved
                        UrlParams = trackUrlParams
                    };

                    section.Tracks.Add(track);
                }

                biblePublication.Sections.Add(section);
            }

            if (biblePublication.Sections.Count == 0)
            {
                logger.Warning("No sections created for MelodyMusic publication {PublicationCode}", publicationCode);
                continue;
            }

            // Save to database
            db.BiblePublications.Add(biblePublication);
            await db.SaveChangesAsync();

            logger.Information("✓ Successfully seeded MelodyMusic publication {PublicationCode} with {SectionCount} sections and {TrackCount} total tracks",
                publicationCode, biblePublication.Sections.Count, biblePublication.Sections.Sum(s => s.Tracks.Count));
        }

        logger.Information("=== MelodyMusic seeding completed ===");
    }
}

