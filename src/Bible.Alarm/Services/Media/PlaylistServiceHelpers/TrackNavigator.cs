#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers.TrackNavigatorHelpers;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles Bible track and section navigation.
/// </summary>
public sealed class TrackNavigator
{
    private readonly IMediaService mediaService;
    private readonly IBiblePublicationService biblePublicationService;
    private readonly ILanguageContentService? languageContentService;
    private readonly ILogger logger;

    public TrackNavigator(
        IMediaService mediaService,
        IBiblePublicationService biblePublicationService,
        ILogger logger,
        ILanguageContentService? languageContentService = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
        this.logger = logger;
        NonSectionedHelper = new TrackNavigatorNonSectionedHelper(
            biblePublicationService,
            logger,
            GetFirstTrackOfPublicationAsync,
            GetLastTrackOfPublicationAsync);
        SectionCataloger = new TrackNavigatorSectionCataloger(
            mediaService,
            languageContentService,
            scopeFactory,
            logger,
            GetSectionsCachedAsync);
        CrossPublicationHelper = new TrackNavigatorCrossPublicationHelper(
            biblePublicationService,
            logger,
            GetFirstTrackOfPublicationAsync,
            GetLastTrackOfPublicationAsync);
    }

    private TrackNavigatorNonSectionedHelper NonSectionedHelper { get; }
    private TrackNavigatorSectionCataloger SectionCataloger { get; }
    private TrackNavigatorCrossPublicationHelper CrossPublicationHelper { get; }

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly record struct SectionsCacheKey(string LanguageCode, string PublicationCode);
    private readonly record struct TracksCacheKey(string LanguageCode, string PublicationCode, string SectionCode);

    private sealed class CacheEntry<T>(DateTimeOffset createdAt, T value)
    {
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public T Value { get; } = value;
    }

    private readonly Dictionary<SectionsCacheKey, CacheEntry<SortedDictionary<string, BiblePublicationSection>>> sectionsCache = new();
    private readonly Dictionary<TracksCacheKey, CacheEntry<SortedDictionary<string, BiblePublicationTrack>>> tracksCache = new();
    private readonly object cacheLock = new();

    private async Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsCachedAsync(string languageCode, string publicationCode)
    {
        var key = new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode);
        var now = DateTimeOffset.UtcNow;

        lock (cacheLock)
        {
            if (sectionsCache.TryGetValue(key, out var entry) && now - entry.CreatedAt <= CacheTtl)
            {
                return entry.Value;
            }
        }

        var sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
        lock (cacheLock)
        {
            sectionsCache[key] = new CacheEntry<SortedDictionary<string, BiblePublicationSection>>(now, sections);
        }

        return sections;
    }

    private async Task<SortedDictionary<string, BiblePublicationTrack>> GetTracksCachedAsync(string languageCode, string publicationCode, string? sectionCode)
    {
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode) ?? string.Empty;
        var key = new TracksCacheKey(languageCode.ToUpperInvariant(), publicationCode, normalizedSectionCode.ToUpperInvariant());
        var now = DateTimeOffset.UtcNow;

        lock (cacheLock)
        {
            if (tracksCache.TryGetValue(key, out var entry) && now - entry.CreatedAt <= CacheTtl)
            {
                return entry.Value;
            }
        }

        var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, SectionCodeHelper.Normalize(sectionCode));
        lock (cacheLock)
        {
            tracksCache[key] = new CacheEntry<SortedDictionary<string, BiblePublicationTrack>>(now, tracks);
        }

        return tracks;
    }

    private void InvalidateSectionsCache(string languageCode, string publicationCode)
    {
        lock (cacheLock)
        {
            sectionsCache.Remove(new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode));
        }
    }

    private void InvalidateTracksCache(string languageCode, string publicationCode, string sectionCode)
    {
        lock (cacheLock)
        {
            tracksCache.Remove(new TracksCacheKey(languageCode.ToUpperInvariant(), publicationCode, sectionCode.ToUpperInvariant()));
        }
    }

    /// <summary>
    /// Gets the next Bible track.
    /// For non-sectioned publications, navigates through tracks with wrap at end.
    /// For sectioned publications: exhausts all tracks in a section, then moves to the next section.
    /// At the last section boundary, non-Bible/non-Music categories advance to the next publication;
    /// Bible and Music categories wrap to the first section of the same publication.
    /// </summary>
    public async Task<TrackNavigationResult> GetNextBiblePublicationTrack(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        string trackCode,
        IFetchProgress? sectionFetchProgress = null)
    {
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return await NonSectionedHelper.GetNextAsync(languageCode, publicationCode, trackCode, sectionFetchProgress);
        }

        // Sectioned publication: circle within the whole publication (all sections), not within a section.
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        if (string.IsNullOrEmpty(normalizedSectionCode))
        {
            return await NonSectionedHelper.GetNextAsync(languageCode, publicationCode, trackCode, sectionFetchProgress);
        }

        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        if (!sections.TryGetValue(normalizedSectionCode, out var currentSection) || currentSection == null)
        {
            throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        var tracks = await GetTracksCachedAsync(languageCode, publicationCode, normalizedSectionCode);
        var currentKey = ResolveTrackCodeToKey(tracks, trackCode);
        // Use TrackCodeComparer to find next track (tracks dictionary is already sorted correctly)
        var nextTrack = tracks.SkipWhile(kvp => TrackCodeComparer.Comparer.Compare(kvp.Key, currentKey) <= 0).FirstOrDefault();

        if (!nextTrack.Equals(default(KeyValuePair<string, BiblePublicationTrack>)))
        {
            return new TrackNavigationResult(publicationCode, currentSection, nextTrack.Value);
        }

        // No next track in this section. If this is the last section and the category
        // supports cross-publication navigation (non-Bible, non-Music), advance to the next publication.
        var discoveredForNext = await SectionCataloger.GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
        var orderedForNext = discoveredForNext.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();
        var nextIdx = orderedForNext.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (nextIdx >= 0 && nextIdx == orderedForNext.Count - 1)
        {
            var crossPub = await CrossPublicationHelper.TryGetNextAsync(languageCode, publicationCode, sectionFetchProgress);
            if (crossPub != null)
            {
                return crossPub;
            }
        }

        var nextSection = await GetNextBiblePublicationSection(languageCode, publicationCode, normalizedSectionCode, sectionFetchProgress);
        if (nextSection.Value == null)
        {
            throw new InvalidOperationException($"Next bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        tracks = await GetTracksCachedAsync(languageCode, publicationCode, nextSection.Key);
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks in next section: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={nextSection.Key}");
        }

        return new TrackNavigationResult(publicationCode, nextSection.Value, FirstSortedDictionaryTrack(tracks));
    }

    private static string ResolveTrackCodeToKey(SortedDictionary<string, BiblePublicationTrack> tracks, string trackCode)
    {
        if (tracks.ContainsKey(trackCode))
        {
            return trackCode;
        }
        return tracks.First(kvp => TrackCodeHelper.GetFromTrack(kvp.Value) == trackCode).Key;
    }

    /// <summary>
    /// Gets the previous Bible track.
    /// For non-sectioned publications, navigates through tracks with wrap at start.
    /// For sectioned publications: moves to the previous track, then the previous section.
    /// At the first section boundary, non-Bible/non-Music categories go to the previous publication's last track;
    /// Bible and Music categories wrap to the last section of the same publication.
    /// </summary>
    public async Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        string trackCode,
        IFetchProgress? sectionFetchProgress = null)
    {
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return await NonSectionedHelper.GetPreviousAsync(languageCode, publicationCode, trackCode, sectionFetchProgress);
        }

        // Sectioned publication: circle within the whole publication (all sections), not within a section.
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        if (string.IsNullOrEmpty(normalizedSectionCode))
        {
            return await NonSectionedHelper.GetPreviousAsync(languageCode, publicationCode, trackCode, sectionFetchProgress);
        }

        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        if (!sections.TryGetValue(normalizedSectionCode, out var currentSection) || currentSection == null)
        {
            throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        var tracks = await GetTracksCachedAsync(languageCode, publicationCode, normalizedSectionCode);
        var currentKey = ResolveTrackCodeToKey(tracks, trackCode);

        // Use TrackCodeComparer to find previous track (tracks dictionary is already sorted correctly)
        var previousTrack = tracks.Reverse().SkipWhile(kvp => TrackCodeComparer.Comparer.Compare(kvp.Key, currentKey) >= 0).FirstOrDefault();

        if (!previousTrack.Equals(default(KeyValuePair<string, BiblePublicationTrack>)))
        {
            return new TrackNavigationResult(publicationCode, currentSection, previousTrack.Value);
        }

        // No previous track in this section. If this is the first section and the category
        // supports cross-publication navigation (non-Bible, non-Music), go to the previous publication.
        var discoveredForPrev = await SectionCataloger.GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
        var orderedForPrev = discoveredForPrev.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();
        var prevIdx = orderedForPrev.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (prevIdx == 0)
        {
            var crossPub = await CrossPublicationHelper.TryGetPreviousAsync(languageCode, publicationCode, sectionFetchProgress);
            if (crossPub != null)
            {
                return crossPub;
            }
        }

        var previousSection = await GetPreviousBiblePublicationSection(languageCode, publicationCode, normalizedSectionCode, sectionFetchProgress);
        if (previousSection.Value == null)
        {
            throw new InvalidOperationException($"Previous bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        tracks = await GetTracksCachedAsync(languageCode, publicationCode, previousSection.Key);
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks in previous section: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={previousSection.Key}");
        }

        return new TrackNavigationResult(publicationCode, previousSection.Value, LastSortedDictionaryTrack(tracks));
    }

    /// <summary>
    /// Gets the previous Bible section in publication order.
    /// Circular: previous of first section is the last section.
    /// Checks discovered sections and catalogs missing ones if needed.
    /// </summary>
    public Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(
        string languageCode,
        string publicationCode,
        string sectionCode,
        IFetchProgress? sectionFetchProgress = null)
        => ResolveAdjacentSectionWithTracksAsync(false, languageCode, publicationCode, sectionCode, sectionFetchProgress);

    /// <summary>
    /// Gets the next Bible section in publication order.
    /// Circular: next of last section is the first section.
    /// Checks discovered sections and catalogs missing ones if needed.
    /// </summary>
    public Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(
        string languageCode,
        string publicationCode,
        string sectionCode,
        IFetchProgress? sectionFetchProgress = null)
        => ResolveAdjacentSectionWithTracksAsync(true, languageCode, publicationCode, sectionCode, sectionFetchProgress);

    private async Task<KeyValuePair<string, BiblePublicationSection>> ResolveAdjacentSectionWithTracksAsync(
        bool forward,
        string languageCode,
        string publicationCode,
        string sectionCode,
        IFetchProgress? sectionFetchProgress)
    {
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        if (string.IsNullOrEmpty(normalizedSectionCode))
        {
            throw new InvalidOperationException($"Invalid section code: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var discoveredSectionCodes = await SectionCataloger.GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
        if (discoveredSectionCodes.Count == 0)
        {
            throw new InvalidOperationException($"No discovered sections found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var orderedDiscoveredKeys = discoveredSectionCodes.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();
        var discoveredCount = orderedDiscoveredKeys.Count;
        var currentIndex = orderedDiscoveredKeys.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Bible section not found in discovered sections: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        var index = StepDiscoveredSectionIndex(forward, currentIndex, discoveredCount);
        var attempts = 0;

        while (attempts < discoveredCount)
        {
            var candidateSectionCode = orderedDiscoveredKeys[index];

            var (resolved, updatedSections) = await AttemptResolveAdjacentCandidateAsync(
                sections,
                languageCode,
                publicationCode,
                candidateSectionCode,
                sectionFetchProgress);

            sections = updatedSections;

            if (resolved != null)
            {
                return resolved.Value;
            }

            index = StepDiscoveredSectionIndex(forward, index, discoveredCount);
            attempts++;
        }

        throw new InvalidOperationException(forward
            ? $"No valid next section found after attempting {discoveredCount} sections: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}"
            : $"No valid previous section found after attempting {discoveredCount} sections: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
    }

    private static int StepDiscoveredSectionIndex(bool forward, int index, int discoveredCount)
    {
        return forward
            ? (index + 1) % discoveredCount
            : (index - 1 + discoveredCount) % discoveredCount;
    }

    private async Task<(KeyValuePair<string, BiblePublicationSection>? Result, SortedDictionary<string, BiblePublicationSection> Sections)>
        AttemptResolveAdjacentCandidateAsync(
            SortedDictionary<string, BiblePublicationSection> sections,
            string languageCode,
            string publicationCode,
            string candidateSectionCode,
            IFetchProgress? sectionFetchProgress)
    {
        if (sections.TryGetValue(candidateSectionCode, out var matchedSection))
        {
            var fromCache =
                await TryBuildSectionPairWhenHasTracksAsync(languageCode, publicationCode, candidateSectionCode,
                    matchedSection);
            if (fromCache != null)
            {
                return (fromCache, sections);
            }
        }

        var cataloged = await SectionCataloger.EnsureSectionCatalogedAsync(
            languageCode,
            publicationCode,
            candidateSectionCode,
            sectionFetchProgress,
            () => InvalidateSectionsCache(languageCode, publicationCode),
            () => InvalidateTracksCache(languageCode, publicationCode, candidateSectionCode));

        if (!cataloged)
        {
            return (null, sections);
        }

        var refreshedSections = await GetSectionsCachedAsync(languageCode, publicationCode);

        if (!refreshedSections.TryGetValue(candidateSectionCode, out matchedSection))
        {
            return (null, refreshedSections);
        }

        var afterCatalog =
            await TryBuildSectionPairWhenHasTracksAsync(languageCode, publicationCode, candidateSectionCode,
                matchedSection);
        return (afterCatalog, refreshedSections);
    }

    private async Task<KeyValuePair<string, BiblePublicationSection>?> TryBuildSectionPairWhenHasTracksAsync(
        string languageCode,
        string publicationCode,
        string candidateSectionCode,
        BiblePublicationSection section)
    {
        var tracks = await GetTracksCachedAsync(languageCode, publicationCode, candidateSectionCode);
        return tracks.Count > 0
            ? new KeyValuePair<string, BiblePublicationSection>(candidateSectionCode, section)
            : null;
    }

    private async Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?> GetFirstTrackOfPublicationAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? sectionFetchProgress = null)
        => await ResolvePublicationBoundaryTrackAsync(languageCode, publicationCode, sectionFetchProgress, pickLast: false);

    private async Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?> GetLastTrackOfPublicationAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? sectionFetchProgress = null)
        => await ResolvePublicationBoundaryTrackAsync(languageCode, publicationCode, sectionFetchProgress, pickLast: true);

    private async Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?> ResolvePublicationBoundaryTrackAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? sectionFetchProgress,
        bool pickLast)
    {
        var orderedFlat = await TryLoadOrderedFlatPublicationTracksAsync(languageCode, publicationCode, sectionFetchProgress);
        if (orderedFlat != null)
        {
            var track = pickLast ? orderedFlat[^1] : orderedFlat[0];
            return (null, track);
        }

        return await TryResolveBoundaryTrackViaOrderedSectionsAsync(languageCode, publicationCode, sectionFetchProgress, pickLast);
    }

    private async Task<List<BiblePublicationTrack>?> TryLoadOrderedFlatPublicationTracksAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? sectionFetchProgress)
    {
        var pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
        var ordered = TryOrderPublicationTracks(pubWithTracks);
        if (ordered != null)
        {
            return ordered;
        }

        if (languageContentService != null && (pubWithTracks == null || pubWithTracks.Tracks == null || pubWithTracks.Tracks.Count == 0))
        {
            var ensured = await languageContentService.EnsurePublicationExistsAsync(
                publicationCode,
                languageCode,
                sectionFetchProgress,
                CancellationToken.None);
            if (ensured)
            {
                biblePublicationService.InvalidatePublicationCaches(languageCode, publicationCode);
                pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
                return TryOrderPublicationTracks(pubWithTracks);
            }
        }

        return null;
    }

    private static List<BiblePublicationTrack>? TryOrderPublicationTracks(BiblePublication? publication)
    {
        if (publication?.Tracks == null || publication.Tracks.Count == 0)
        {
            return null;
        }

        return publication.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList();
    }

    private async Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?> TryResolveBoundaryTrackViaOrderedSectionsAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? sectionFetchProgress,
        bool pickLast)
    {
        var discoveredSectionCodes = await SectionCataloger.GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
        if (discoveredSectionCodes.Count == 0)
        {
            return null;
        }

        var orderedSectionCodes = discoveredSectionCodes.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();

        if (pickLast)
        {
            for (var i = orderedSectionCodes.Count - 1; i >= 0; i--)
            {
                var resolved = await TryResolveBoundaryTrackForSectionAsync(
                    languageCode,
                    publicationCode,
                    orderedSectionCodes[i],
                    sectionFetchProgress,
                    useLastTrack: true);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }

        foreach (var candidateSectionCode in orderedSectionCodes)
        {
            var resolved = await TryResolveBoundaryTrackForSectionAsync(
                languageCode,
                publicationCode,
                candidateSectionCode,
                sectionFetchProgress,
                useLastTrack: false);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }

    private async Task<(BiblePublicationSection Section, BiblePublicationTrack Track)?> TryResolveBoundaryTrackForSectionAsync(
        string languageCode,
        string publicationCode,
        string candidateSectionCode,
        IFetchProgress? sectionFetchProgress,
        bool useLastTrack)
    {
        var cataloged = await SectionCataloger.EnsureSectionCatalogedAsync(
            languageCode,
            publicationCode,
            candidateSectionCode,
            sectionFetchProgress,
            () => InvalidateSectionsCache(languageCode, publicationCode),
            () => InvalidateTracksCache(languageCode, publicationCode, candidateSectionCode));
        if (!cataloged)
        {
            return null;
        }

        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        var tracks = await GetTracksCachedAsync(languageCode, publicationCode, candidateSectionCode);
        if (!sections.TryGetValue(candidateSectionCode, out var section) || tracks.Count == 0)
        {
            logger.Information(
                useLastTrack
                    ? "GetLastTrackOfPublicationAsync: Section {SectionCode} has no tracks after catalog, trying previous section"
                    : "GetFirstTrackOfPublicationAsync: Section {SectionCode} has no tracks after catalog, trying next section",
                candidateSectionCode);
            return null;
        }

        var track = useLastTrack ? LastSortedDictionaryTrack(tracks) : FirstSortedDictionaryTrack(tracks);
        return (section, track);
    }

    private static BiblePublicationTrack FirstSortedDictionaryTrack(SortedDictionary<string, BiblePublicationTrack> tracks)
    {
        using var e = tracks.GetEnumerator();
        _ = e.MoveNext();
        return e.Current.Value;
    }

    private static BiblePublicationTrack LastSortedDictionaryTrack(SortedDictionary<string, BiblePublicationTrack> tracks)
    {
        KeyValuePair<string, BiblePublicationTrack> last = default;
        foreach (var kvp in tracks)
        {
            last = kvp;
        }

        return last.Value;
    }

}
