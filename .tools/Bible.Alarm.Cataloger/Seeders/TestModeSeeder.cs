#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Services.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Cataloger.Seeders;

/// <summary>
/// Helper class for test mode seeding operations.
/// </summary>
internal sealed class TestModeSeeder
{
    private readonly ILogger logger;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly InMemoryDataStore dataStore;

    public TestModeSeeder(ILogger logger, IServiceScopeFactory scopeFactory, InMemoryDataStore dataStore)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    /// <summary>
    /// Seeds test languages (MY and A) for all discovered publications in test mode.
    /// Uses EnsurePublicationExistsAsync to seed each publication for each test language.
    /// </summary>
    public async Task SeedTestLanguages()
    {
        using var scope = scopeFactory.CreateScope();
        var httpClient = scope.ServiceProvider.GetRequiredService<System.Net.Http.HttpClient>();
        var languageContentService = new LanguageContentService(scopeFactory, logger, httpClient);

        var testLanguages = new[] { "MY", "A" };
        logger.Information("=== Seeding test languages ({Languages}) for all discovered publications ===",
            string.Join(", ", testLanguages));

        // Get all discovered publication codes from PublicationLanguages
        var publicationCodes = dataStore.PublicationLanguages.Keys.ToList();

        // Note: "iam" (Kingdom Melodies) is not included here because it doesn't support ad-hoc fetching
        // for other languages - it's only seeded once with null language for English

        if (publicationCodes.Count == 0)
        {
            logger.Warning("No publications discovered, skipping test language seeding");
            return;
        }

        logger.Information("Found {Count} publication(s) to seed test languages for", publicationCodes.Count);

        foreach (var languageCode in testLanguages)
        {
            logger.Information("=== Seeding {LanguageCode} for all discovered publications ===", languageCode);

            foreach (var publicationCode in publicationCodes.OrderBy(pc => pc))
            {
                logger.Information("Seeding {LanguageCode} for publication: {PublicationCode}", languageCode, publicationCode);

                // Use EnsurePublicationExistsAsync to seed the publication for this language
                // This will fetch the publication if it doesn't exist
                var success = await languageContentService.EnsurePublicationExistsAsync(publicationCode, languageCode);

                if (success)
                {
                    logger.Information("✓ Successfully seeded {LanguageCode} for publication {PublicationCode}", languageCode, publicationCode);
                }
                else
                {
                    logger.Warning("✗ Failed to seed {LanguageCode} for publication {PublicationCode}", languageCode, publicationCode);
                }
            }

            logger.Information("=== {LanguageCode} seeding completed ===", languageCode);
        }

        logger.Information("=== Test language seeding completed ===");
    }

    /// <summary>
    /// Tests on-demand fetching of non-English languages (MY and A) for all English publications.
    /// Uses LanguageContentService from Shared project with data-driven approach.
    /// Only runs in test mode to validate the on-demand fetching logic.
    /// </summary>
    public async Task TestOnDemandFetching()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var httpClient = scope.ServiceProvider.GetRequiredService<System.Net.Http.HttpClient>();
        var languageContentService = new LanguageContentService(scopeFactory, logger, httpClient);

        // Test languages: Malayalam (MY) and Arabic (A)
        var testLanguages = new[] { "MY", "A" };

        logger.Information("=== TEST MODE: Testing on-demand fetching for languages {Languages} ===",
            string.Join(", ", testLanguages));

        // Get all publication codes from PublicationLanguages (these are the ones available for non-English)
        var publicationCodes = await db.PublicationLanguages
            .Include(pl => pl.Language)
            .Where(pl => pl.Language != null && pl.Language.LanguageCode != AppConstants.Media.DefaultLanguageCode) // Exclude English
            .Select(pl => pl.PublicationCode)
            .Distinct()
            .ToListAsync();

        if (publicationCodes.Count == 0)
        {
            logger.Warning("No publications found in PublicationLanguages for testing");
            return;
        }

        logger.Information("Found {Count} publication(s) to test on-demand fetching", publicationCodes.Count);

        var totalStartTime = DateTime.UtcNow;
        var publicationStats = new List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)>();

        foreach (var publicationCode in publicationCodes.OrderBy(pc => pc))
        {
            var languageTimes = new Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)>(StringComparer.OrdinalIgnoreCase);
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? AppConstants.Media.BiblePublicationCategoryDramas
                    : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // Get English publication to determine category/cataloger type
            var englishPublication = await db.BiblePublications
                .Include(bp => bp.Language)
                .Include(bp => bp.BiblePublicationCategories)
                .ThenInclude(bpc => bpc.Category)
                .FirstOrDefaultAsync(bp => bp.PublicationCode == publicationCodeForDb &&
                                          bp.Language != null &&
                                          bp.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

            if (englishPublication == null)
            {
                logger.Warning("English publication {PublicationCode} not found, skipping", publicationCode);
                continue;
            }

            logger.Information("Testing on-demand fetching for publication: {PublicationCode} (Category: {Category}, IsVideo: {IsVideo})",
                publicationCode, englishPublication.PrimaryCategory?.CategoryCode ?? "Unknown", englishPublication.IsVideo);

            foreach (var testLanguageCode in testLanguages)
            {
                var normalizedTestLanguageCode = testLanguageCode.ToUpperInvariant();
                
                // Check if language is available for this publication
                var isAvailable = await db.PublicationLanguages
                    .Include(pl => pl.Language)
                    .AnyAsync(pl => pl.PublicationCode == normalizedPublicationCode &&
                                   pl.Language != null &&
                                   pl.Language.LanguageCode == normalizedTestLanguageCode);

                if (!isAvailable)
                {
                    logger.Warning("Language {LanguageCode} is not available for publication {PublicationCode}, skipping",
                        testLanguageCode, publicationCode);
                    continue;
                }

                // Check if publication already exists for this language
                var existing = await db.BiblePublications
                    .Include(bp => bp.Language)
                    .AnyAsync(bp => bp.PublicationCode == normalizedPublicationCode &&
                                  bp.Language != null &&
                                  bp.Language.LanguageCode == normalizedTestLanguageCode);

                if (existing)
                {
                    logger.Information("Publication {PublicationCode} for language {LanguageCode} already exists, skipping fetch",
                        publicationCode, testLanguageCode);
                    continue;
                }

                var languageStartTime = DateTime.UtcNow;
                try
                {
                    // Check if publication has sections by checking SectionLanguages table for English
                    // (SectionLanguages is only populated for English during initial catalog)
                    var hasSections = await db.SectionLanguages
                        .Include(sl => sl.Language)
                        .AnyAsync(sl => sl.PublicationCode == normalizedPublicationCode &&
                                      sl.Language != null &&
                                      sl.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

                    bool success;
                    if (hasSections)
                    {
                        // Publication has sections: Fetch all sections first, then tracks for each section
                        logger.Information("Fetching sections for publication {PublicationCode} in language {LanguageCode}...",
                            publicationCode, testLanguageCode);

                        var sectionsStartTime = DateTime.UtcNow;
                        success = await languageContentService.FetchPublicationSectionsAsync(
                            normalizedPublicationCode, normalizedTestLanguageCode);
                        var sectionsElapsed = DateTime.UtcNow - sectionsStartTime;

                        if (success)
                        {
                            logger.Information("✓ Fetched sections for {PublicationCode} in {LanguageCode} in {ElapsedMs}ms",
                                publicationCode, testLanguageCode, sectionsElapsed.TotalMilliseconds);

                            // Get all section codes for this publication from English (E)
                            // (SectionLanguages is only populated for English during initial catalog)
                            var sectionCodes = await db.SectionLanguages
                                .Include(sl => sl.Language)
                                .Where(sl => sl.PublicationCode == normalizedPublicationCode &&
                                           sl.Language != null &&
                                           sl.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode)
                                .Select(sl => sl.SectionCode)
                                .Distinct()
                                .OrderBy(sc => sc)
                                .ToListAsync();

                            logger.Information("Fetching tracks for {Count} section(s) in publication {PublicationCode} for language {LanguageCode}...",
                                sectionCodes.Count, publicationCode, testLanguageCode);

                            var tracksStartTime = DateTime.UtcNow;
                            var sectionsFetched = 0;
                            foreach (var sectionCode in sectionCodes)
                            {
                                var sectionSuccess = await languageContentService.FetchSectionTracksAsync(
                                    normalizedPublicationCode, sectionCode, normalizedTestLanguageCode);
                                if (sectionSuccess)
                                {
                                    sectionsFetched++;
                                }
                            }
                            var tracksElapsed = DateTime.UtcNow - tracksStartTime;

                            logger.Information("✓ Fetched tracks for {Fetched}/{Total} section(s) in {ElapsedMs}ms",
                                sectionsFetched, sectionCodes.Count, tracksElapsed.TotalMilliseconds);

                            // Total time includes both sections and tracks
                            var totalTimeForLanguage = DateTime.UtcNow - languageStartTime;
                            languageTimes[testLanguageCode] = (totalTimeForLanguage, sectionsElapsed, tracksElapsed, sectionCodes.Count, null);
                        }
                        else
                        {
                            var totalTimeForLanguage = DateTime.UtcNow - languageStartTime;
                            languageTimes[testLanguageCode] = (totalTimeForLanguage, sectionsElapsed, null, null, null);
                        }
                    }
                    else
                    {
                        // Publication has no sections: Fetch all tracks directly
                        logger.Information("Fetching tracks for publication {PublicationCode} in language {LanguageCode}...",
                            publicationCode, testLanguageCode);

                        var pubTracksStartTime = DateTime.UtcNow;
                        success = await languageContentService.FetchPublicationTracksAsync(
                            normalizedPublicationCode, normalizedTestLanguageCode);
                        var pubTracksElapsed = DateTime.UtcNow - pubTracksStartTime;

                        var elapsed = DateTime.UtcNow - languageStartTime;
                        languageTimes[testLanguageCode] = (elapsed, null, null, null, pubTracksElapsed);

                        if (success)
                        {
                            logger.Information("✓ Successfully fetched {PublicationCode} for {LanguageCode} in {ElapsedMs}ms",
                                publicationCode, testLanguageCode, elapsed.TotalMilliseconds);
                        }
                        else
                        {
                            logger.Warning("✗ Failed to fetch {PublicationCode} for {LanguageCode} (took {ElapsedMs}ms)",
                                publicationCode, testLanguageCode, elapsed.TotalMilliseconds);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var elapsed = DateTime.UtcNow - languageStartTime;
                    languageTimes[testLanguageCode] = (elapsed, null, null, null, null);
                    logger.Error(ex, "✗ Error fetching {PublicationCode} for {LanguageCode} (took {ElapsedMs}ms)",
                        publicationCode, testLanguageCode, elapsed.TotalMilliseconds);
                }
            }

            publicationStats.Add((publicationCode, languageTimes));
        }

        var totalElapsed = DateTime.UtcNow - totalStartTime;

        // Log summary statistics
        logger.Information("=== TEST MODE: On-Demand Fetching Summary ===");
        logger.Information("Total time: {TotalSeconds:F2}s", totalElapsed.TotalSeconds);
        logger.Information("Publications tested: {Count}", publicationStats.Count);

        // Calculate averages per language
        foreach (var testLanguageCode in testLanguages)
        {
            var times = publicationStats
                .SelectMany(ps => ps.LanguageTimes.Where(lt => lt.Key == testLanguageCode).Select(lt => lt.Value.Total))
                .ToList();

            if (times.Count > 0)
            {
                var avgTime = TimeSpan.FromMilliseconds(times.Average(t => t.TotalMilliseconds));
                var minTime = times.Min();
                var maxTime = times.Max();
                logger.Information("Language {LanguageCode}: {Count} fetched, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                    testLanguageCode, times.Count, avgTime.TotalMilliseconds, minTime.TotalMilliseconds, maxTime.TotalMilliseconds);
            }
        }

        // Calculate separate averages for different operation types
        var sectionsTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.Sections.HasValue).Select(v => v.Sections!.Value))
            .ToList();
        if (sectionsTimes.Count > 0)
        {
            var avgSections = TimeSpan.FromMilliseconds(sectionsTimes.Average(t => t.TotalMilliseconds));
            logger.Information("=== Fetching Sections Statistics ===");
            logger.Information("  Count: {Count}, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                sectionsTimes.Count, avgSections.TotalMilliseconds, sectionsTimes.Min().TotalMilliseconds, sectionsTimes.Max().TotalMilliseconds);
        }

        var sectionTracksTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.SectionTracks.HasValue && v.SectionCount.HasValue)
                .Select(v => (Time: v.SectionTracks!.Value, Count: v.SectionCount!.Value)))
            .ToList();
        if (sectionTracksTimes.Count > 0)
        {
            var avgSectionTracks = TimeSpan.FromMilliseconds(sectionTracksTimes.Average(t => t.Time.TotalMilliseconds));
            var totalSections = sectionTracksTimes.Sum(t => t.Count);
            var avgPerSection = TimeSpan.FromMilliseconds(sectionTracksTimes.Average(t => t.Time.TotalMilliseconds / Math.Max(1, t.Count)));
            logger.Information("=== Fetching Section Tracks Statistics ===");
            logger.Information("  Publications: {Count}, Total Sections: {TotalSections}, Avg per publication: {AvgMs:F0}ms, Avg per section: {AvgPerSectionMs:F0}ms",
                sectionTracksTimes.Count, totalSections, avgSectionTracks.TotalMilliseconds, avgPerSection.TotalMilliseconds);
            logger.Information("  Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                sectionTracksTimes.Min(t => t.Time).TotalMilliseconds, sectionTracksTimes.Max(t => t.Time).TotalMilliseconds);
        }

        var publicationTracksTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.PublicationTracks.HasValue).Select(v => v.PublicationTracks!.Value))
            .ToList();
        if (publicationTracksTimes.Count > 0)
        {
            var avgPubTracks = TimeSpan.FromMilliseconds(publicationTracksTimes.Average(t => t.TotalMilliseconds));
            logger.Information("=== Fetching Publication Tracks Statistics (non-sectioned) ===");
            logger.Information("  Count: {Count}, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                publicationTracksTimes.Count, avgPubTracks.TotalMilliseconds, publicationTracksTimes.Min().TotalMilliseconds, publicationTracksTimes.Max().TotalMilliseconds);
        }

        // Log per-publication statistics with breakdown
        logger.Information("=== Per-Publication Statistics ===");
        foreach (var (pubCode, langTimes) in publicationStats.OrderBy(ps => ps.PublicationCode))
        {
            if (langTimes.Count > 0)
            {
                var timesStr = string.Join(", ", langTimes.Select(lt =>
                {
                    var (total, sections, sectionTracks, sectionCount, pubTracks) = lt.Value;
                    if (sections.HasValue && sectionTracks.HasValue && sectionCount.HasValue)
                    {
                        var perSection = sectionTracks.Value.TotalMilliseconds / Math.Max(1, sectionCount.Value);
                        return $"{lt.Key}: {total.TotalMilliseconds:F0}ms (sections: {sections.Value.TotalMilliseconds:F0}ms, tracks: {sectionTracks.Value.TotalMilliseconds:F0}ms for {sectionCount.Value} sections, ~{perSection:F0}ms/section)";
                    }
                    else if (pubTracks.HasValue)
                    {
                        return $"{lt.Key}: {total.TotalMilliseconds:F0}ms (tracks: {pubTracks.Value.TotalMilliseconds:F0}ms)";
                    }
                    else
                    {
                        return $"{lt.Key}: {total.TotalMilliseconds:F0}ms";
                    }
                }));
                logger.Information("  {PublicationCode}: {Times}", pubCode, timesStr);
            }
        }
    }
}
