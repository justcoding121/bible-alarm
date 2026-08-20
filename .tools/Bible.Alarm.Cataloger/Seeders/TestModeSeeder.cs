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
        logger.Debug("=== Seeding test languages ({Languages}) for all discovered publications ===",
            string.Join(", ", testLanguages));

        var publicationCodes = dataStore.PublicationLanguages.Keys.ToList();

        // Note: "iam" (Kingdom Melodies) is not included here because it doesn't support ad-hoc fetching
        // for other languages - it's only seeded once with null language for English

        if (publicationCodes.Count == 0)
        {
            logger.Warning("No publications discovered, skipping test language seeding");
            return;
        }

        logger.Debug("Found {Count} publication(s) to seed test languages for", publicationCodes.Count);

        foreach (var languageCode in testLanguages)
        {
            logger.Debug("=== Seeding {LanguageCode} for all discovered publications ===", languageCode);

            foreach (var publicationCode in publicationCodes.OrderBy(pc => pc))
            {
                logger.Debug("Seeding {LanguageCode} for publication: {PublicationCode}", languageCode, publicationCode);

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

            logger.Debug("=== {LanguageCode} seeding completed ===", languageCode);
        }

        logger.Debug("=== Test language seeding completed ===");
    }

    /// <summary>
    /// Tests on-demand fetching of non-English languages (MY and A) for all English publications.
    /// Uses LanguageContentService from Shared project with data-driven approach.
    /// Only runs in test mode to validate the on-demand fetching logic.
    /// </summary>
    public Task TestOnDemandFetching() => RunTestOnDemandFetchingAsync();

    private async Task RunTestOnDemandFetchingAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var httpClient = scope.ServiceProvider.GetRequiredService<System.Net.Http.HttpClient>();
        var languageContentService = new LanguageContentService(scopeFactory, logger, httpClient);

        var testLanguages = new[] { "MY", "A" };

        logger.Debug("=== TEST MODE: Testing on-demand fetching for languages {Languages} ===",
            string.Join(", ", testLanguages));

        var publicationCodes = await db.PublicationLanguages
            .Include(pl => pl.Language)
            .Where(pl => pl.Language != null && pl.Language.LanguageCode != AppConstants.Media.DefaultLanguageCode)
            .Select(pl => pl.PublicationCode)
            .Distinct()
            .ToListAsync();

        if (publicationCodes.Count == 0)
        {
            logger.Warning("No publications found in PublicationLanguages for testing");
            return;
        }

        logger.Debug("Found {Count} publication(s) to test on-demand fetching", publicationCodes.Count);

        var totalStartTime = DateTime.UtcNow;
        var publicationStats = new List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)>();

        foreach (var publicationCode in publicationCodes.OrderBy(pc => pc))
        {
            var languageTimes = await BuildOnDemandLanguageTimesForPublicationAsync(
                db, languageContentService, publicationCode, testLanguages);
            publicationStats.Add((publicationCode, languageTimes));
        }

        var totalElapsed = DateTime.UtcNow - totalStartTime;
        LogOnDemandFetchingSummary(testLanguages, publicationStats, totalElapsed);
    }

    private async Task<Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)>> BuildOnDemandLanguageTimesForPublicationAsync(
        MediaDbContext db,
        LanguageContentService languageContentService,
        string publicationCode,
        string[] testLanguages)
    {
        var languageTimes = new Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)>(StringComparer.OrdinalIgnoreCase);
        var normalizedPublicationCode = publicationCode.ToLowerInvariant();

        var isDrama = PublicationTypeHelper.IsDrama(normalizedPublicationCode);
        var publicationCodeForDb = ResolvePublicationCodeForDb(normalizedPublicationCode, isDrama);

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
            return languageTimes;
        }

        logger.Debug("Testing on-demand fetching for publication: {PublicationCode} (Category: {Category}, IsVideo: {IsVideo})",
            publicationCode, englishPublication.PrimaryCategory?.CategoryCode ?? "Unknown", englishPublication.IsVideo);

        foreach (var testLanguageCode in testLanguages)
        {
            var normalizedTestLanguageCode = testLanguageCode.ToUpperInvariant();

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

            var existing = await db.BiblePublications
                .Include(bp => bp.Language)
                .AnyAsync(bp => bp.PublicationCode == normalizedPublicationCode &&
                              bp.Language != null &&
                              bp.Language.LanguageCode == normalizedTestLanguageCode);

            if (existing)
            {
                logger.Debug("Publication {PublicationCode} for language {LanguageCode} already exists, skipping fetch",
                    publicationCode, testLanguageCode);
                continue;
            }

            await TryRunOnDemandFetchForTestLanguageAsync(new OnDemandFetchForTestAttemptContext
            {
                Db = db,
                LanguageContentService = languageContentService,
                PublicationCode = publicationCode,
                NormalizedPublicationCode = normalizedPublicationCode,
                NormalizedTestLanguageCode = normalizedTestLanguageCode,
                TestLanguageCode = testLanguageCode,
                LanguageTimes = languageTimes
            });
        }

        return languageTimes;
    }

    private static string ResolvePublicationCodeForDb(string normalizedPublicationCode, bool isDrama)
    {
        if (!isDrama)
        {
            return normalizedPublicationCode;
        }

        return normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
            ? AppConstants.Media.BiblePublicationCategoryDramas
            : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
    }

    private void LogOnDemandFetchingSummary(
        string[] testLanguages,
        List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)> publicationStats,
        TimeSpan totalElapsed)
    {
        logger.Debug(
            "=== TEST MODE: On-Demand Fetching Summary | Total time: {TotalSeconds:F2}s | Publications tested: {Count} ===",
            totalElapsed.TotalSeconds, publicationStats.Count);

        LogOnDemandSummaryPerLanguageAggregates(testLanguages, publicationStats);
        LogOnDemandSummarySectionsTiming(publicationStats);
        LogOnDemandSummarySectionTracksTiming(publicationStats);
        LogOnDemandSummaryPublicationTracksTiming(publicationStats);
        LogOnDemandSummaryPerPublicationDetail(publicationStats);
    }

    private void LogOnDemandSummaryPerLanguageAggregates(
        string[] testLanguages,
        List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)> publicationStats)
    {
        foreach (var testLanguageCode in testLanguages)
        {
            var times = publicationStats
                .SelectMany(ps => ps.LanguageTimes.Where(lt => lt.Key == testLanguageCode).Select(lt => lt.Value.Total))
                .ToList();

            if (times.Count == 0)
            {
                continue;
            }

            var avgTime = TimeSpan.FromMilliseconds(times.Average(t => t.TotalMilliseconds));
            var minTime = times.Min();
            var maxTime = times.Max();
            logger.Debug("Language {LanguageCode}: {Count} fetched, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
                testLanguageCode, times.Count, avgTime.TotalMilliseconds, minTime.TotalMilliseconds, maxTime.TotalMilliseconds);
        }
    }

    private void LogOnDemandSummarySectionsTiming(
        List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)> publicationStats)
    {
        var sectionsTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.Sections.HasValue).Select(v => v.Sections!.Value))
            .ToList();
        if (sectionsTimes.Count == 0)
        {
            return;
        }

        var avgSections = TimeSpan.FromMilliseconds(sectionsTimes.Average(t => t.TotalMilliseconds));
        logger.Debug("=== Fetching Sections Statistics ===\n  Count: {Count}, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
            sectionsTimes.Count, avgSections.TotalMilliseconds, sectionsTimes.Min().TotalMilliseconds, sectionsTimes.Max().TotalMilliseconds);
    }

    private void LogOnDemandSummarySectionTracksTiming(
        List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)> publicationStats)
    {
        var sectionTracksTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.SectionTracks.HasValue && v.SectionCount.HasValue)
                .Select(v => (Time: v.SectionTracks!.Value, Count: v.SectionCount!.Value)))
            .ToList();
        if (sectionTracksTimes.Count == 0)
        {
            return;
        }

        var avgSectionTracks = TimeSpan.FromMilliseconds(sectionTracksTimes.Average(t => t.Time.TotalMilliseconds));
        var totalSections = sectionTracksTimes.Sum(t => t.Count);
        var avgPerSection = TimeSpan.FromMilliseconds(sectionTracksTimes.Average(t => t.Time.TotalMilliseconds / Math.Max(1, t.Count)));
        logger.Debug(
            "=== Fetching Section Tracks Statistics ===\n  Publications: {Count}, Total Sections: {TotalSections}, Avg per publication: {AvgMs:F0}ms, Avg per section: {AvgPerSectionMs:F0}ms\n  Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
            sectionTracksTimes.Count,
            totalSections,
            avgSectionTracks.TotalMilliseconds,
            avgPerSection.TotalMilliseconds,
            sectionTracksTimes.Min(t => t.Time).TotalMilliseconds,
            sectionTracksTimes.Max(t => t.Time).TotalMilliseconds);
    }

    private void LogOnDemandSummaryPublicationTracksTiming(
        List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)> publicationStats)
    {
        var publicationTracksTimes = publicationStats
            .SelectMany(ps => ps.LanguageTimes.Values.Where(v => v.PublicationTracks.HasValue).Select(v => v.PublicationTracks!.Value))
            .ToList();
        if (publicationTracksTimes.Count == 0)
        {
            return;
        }

        var avgPubTracks = TimeSpan.FromMilliseconds(publicationTracksTimes.Average(t => t.TotalMilliseconds));
        logger.Debug(
            "=== Fetching Publication Tracks Statistics (non-sectioned) ===\n  Count: {Count}, Avg: {AvgMs:F0}ms, Min: {MinMs:F0}ms, Max: {MaxMs:F0}ms",
            publicationTracksTimes.Count, avgPubTracks.TotalMilliseconds, publicationTracksTimes.Min().TotalMilliseconds, publicationTracksTimes.Max().TotalMilliseconds);
    }

    private void LogOnDemandSummaryPerPublicationDetail(
        List<(string PublicationCode, Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes)> publicationStats)
    {
        logger.Debug("=== Per-Publication Statistics ===");
        foreach (var (pubCode, langTimes) in publicationStats.OrderBy(ps => ps.PublicationCode))
        {
            if (langTimes.Count == 0)
            {
                continue;
            }

            var timesStr = string.Join(", ", langTimes.Select(lt => FormatOnDemandLanguageTimesLogLine(lt.Key, lt.Value)));
            logger.Debug("  {PublicationCode}: {Times}", pubCode, timesStr);
        }
    }

    private static string FormatOnDemandLanguageTimesLogLine(
        string languageKey,
        (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks) value)
    {
        var (total, sections, sectionTracks, sectionCount, pubTracks) = value;
        if (sections.HasValue && sectionTracks.HasValue && sectionCount.HasValue)
        {
            var perSection = sectionTracks.Value.TotalMilliseconds / Math.Max(1, sectionCount.Value);
            return $"{languageKey}: {total.TotalMilliseconds:F0}ms (sections: {sections.Value.TotalMilliseconds:F0}ms, tracks: {sectionTracks.Value.TotalMilliseconds:F0}ms for {sectionCount.Value} sections, ~{perSection:F0}ms/section)";
        }

        if (pubTracks.HasValue)
        {
            return $"{languageKey}: {total.TotalMilliseconds:F0}ms (tracks: {pubTracks.Value.TotalMilliseconds:F0}ms)";
        }

        return $"{languageKey}: {total.TotalMilliseconds:F0}ms";
    }

    private sealed class FetchSectionedPublicationForTestRequest
    {
        public required string PublicationCode { get; init; }
        public required string TestLanguageCode { get; init; }
        public required string NormalizedPublicationCode { get; init; }
        public required string NormalizedTestLanguageCode { get; init; }
        public required Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes { get; init; }
        public required DateTime LanguageStartTime { get; init; }
    }

    private async Task FetchSectionedPublicationForTestAsync(
        MediaDbContext db,
        LanguageContentService languageContentService,
        FetchSectionedPublicationForTestRequest request)
    {
        var publicationCode = request.PublicationCode;
        var testLanguageCode = request.TestLanguageCode;
        var normalizedPublicationCode = request.NormalizedPublicationCode;
        var normalizedTestLanguageCode = request.NormalizedTestLanguageCode;
        var languageTimes = request.LanguageTimes;
        var languageStartTime = request.LanguageStartTime;
        logger.Debug("Fetching sections for publication {PublicationCode} in language {LanguageCode}...",
            publicationCode, testLanguageCode);

        var sectionsStartTime = DateTime.UtcNow;
        var success = await languageContentService.FetchPublicationSectionsAsync(
            normalizedPublicationCode, normalizedTestLanguageCode);
        var sectionsElapsed = DateTime.UtcNow - sectionsStartTime;

        if (!success)
        {
            languageTimes[testLanguageCode] = (DateTime.UtcNow - languageStartTime, sectionsElapsed, null, null, null);
            return;
        }

        logger.Debug("✓ Fetched sections for {PublicationCode} in {LanguageCode} in {ElapsedMs}ms",
            publicationCode, testLanguageCode, sectionsElapsed.TotalMilliseconds);

        var sectionCodes = await db.SectionLanguages
            .Include(sl => sl.Language)
            .Where(sl => sl.PublicationCode == normalizedPublicationCode &&
                       sl.Language != null &&
                       sl.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode)
            .Select(sl => sl.SectionCode)
            .Distinct()
            .OrderBy(sc => sc)
            .ToListAsync();

        logger.Debug("Fetching tracks for {Count} section(s) in publication {PublicationCode} for language {LanguageCode}...",
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

        logger.Debug("✓ Fetched tracks for {Fetched}/{Total} section(s) in {ElapsedMs}ms",
            sectionsFetched, sectionCodes.Count, tracksElapsed.TotalMilliseconds);

        languageTimes[testLanguageCode] = (DateTime.UtcNow - languageStartTime, sectionsElapsed, tracksElapsed, sectionCodes.Count, null);
    }

    private sealed class OnDemandFetchForTestAttemptContext
    {
        public required MediaDbContext Db { get; init; }
        public required LanguageContentService LanguageContentService { get; init; }
        public required string PublicationCode { get; init; }
        public required string NormalizedPublicationCode { get; init; }
        public required string NormalizedTestLanguageCode { get; init; }
        public required string TestLanguageCode { get; init; }
        public required Dictionary<string, (TimeSpan Total, TimeSpan? Sections, TimeSpan? SectionTracks, int? SectionCount, TimeSpan? PublicationTracks)> LanguageTimes { get; init; }
    }

    private async Task TryRunOnDemandFetchForTestLanguageAsync(OnDemandFetchForTestAttemptContext ctx)
    {
        var db = ctx.Db;
        var languageContentService = ctx.LanguageContentService;
        var publicationCode = ctx.PublicationCode;
        var normalizedPublicationCode = ctx.NormalizedPublicationCode;
        var normalizedTestLanguageCode = ctx.NormalizedTestLanguageCode;
        var testLanguageCode = ctx.TestLanguageCode;
        var languageTimes = ctx.LanguageTimes;

        var languageStartTime = DateTime.UtcNow;
        try
        {
            var hasSections = await db.SectionLanguages
                .Include(sl => sl.Language)
                .AnyAsync(sl => sl.PublicationCode == normalizedPublicationCode &&
                              sl.Language != null &&
                              sl.Language.LanguageCode == AppConstants.Media.DefaultLanguageCode);

            if (hasSections)
            {
                await FetchSectionedPublicationForTestAsync(db, languageContentService,
                    new FetchSectionedPublicationForTestRequest
                    {
                        PublicationCode = publicationCode,
                        TestLanguageCode = testLanguageCode,
                        NormalizedPublicationCode = normalizedPublicationCode,
                        NormalizedTestLanguageCode = normalizedTestLanguageCode,
                        LanguageTimes = languageTimes,
                        LanguageStartTime = languageStartTime
                    });
                return;
            }

            logger.Debug("Fetching tracks for publication {PublicationCode} in language {LanguageCode}...",
                publicationCode, testLanguageCode);

            var pubTracksStartTime = DateTime.UtcNow;
            var flatSuccess = await languageContentService.FetchPublicationTracksAsync(
                normalizedPublicationCode, normalizedTestLanguageCode);
            var pubTracksElapsed = DateTime.UtcNow - pubTracksStartTime;

            var elapsed = DateTime.UtcNow - languageStartTime;
            languageTimes[testLanguageCode] = (elapsed, null, null, null, pubTracksElapsed);

            if (flatSuccess)
            {
                logger.Debug("✓ Successfully fetched {PublicationCode} for {LanguageCode} in {ElapsedMs}ms",
                    publicationCode, testLanguageCode, elapsed.TotalMilliseconds);
            }
            else
            {
                logger.Warning("✗ Failed to fetch {PublicationCode} for {LanguageCode} (took {ElapsedMs}ms)",
                    publicationCode, testLanguageCode, elapsed.TotalMilliseconds);
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
}
