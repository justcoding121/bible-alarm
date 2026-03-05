#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers.TrackNavigatorHelpers;
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
    private readonly ILogger? logger;

    public TrackNavigator(
        IMediaService mediaService,
        IBiblePublicationService biblePublicationService,
        ILanguageContentService? languageContentService = null,
        IServiceScopeFactory? scopeFactory = null,
        ILogger? logger = null)
    {
        this.mediaService = mediaService;
        this.biblePublicationService = biblePublicationService;
        this.languageContentService = languageContentService;
        this.logger = logger;
        NonSectionedHelper = new TrackNavigatorNonSectionedHelper(
            biblePublicationService,
            GetFirstTrackOfPublicationAsync,
            GetLastTrackOfPublicationAsync,
            logger);
        SectionCataloger = new TrackNavigatorSectionCataloger(
            mediaService,
            languageContentService,
            scopeFactory,
            logger,
            GetSectionsCachedAsync);
    }

    private TrackNavigatorNonSectionedHelper NonSectionedHelper { get; }
    private TrackNavigatorSectionCataloger SectionCataloger { get; }

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

    /// <summary>
    /// Gets the next Bible track.
    /// For non-sectioned publications, navigates through tracks with wrap at end.
    /// For sectioned publications, circular queue at publication level: next from last track of last section
    /// wraps to first track of first section (not within a section).
    /// </summary>
    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextBiblePublicationTrack(
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
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(currentSection, nextTrack.Value);
        }

        // No next track in this section: go to next section (wrap within same publication).
        // Cross-publication wrap is not done here because the caller builds the play item with the
        // current publication code and would fail.
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

        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(nextSection.Value, tracks.ElementAt(0).Value);
    }

    private static string ResolveTrackCodeToKey(SortedDictionary<string, BiblePublicationTrack> tracks, string trackCode)
    {
        if (tracks.ContainsKey(trackCode))
        {
            return trackCode;
        }
        foreach (var kvp in tracks)
        {
            if (TrackCodeHelper.GetFromTrack(kvp.Value) == trackCode)
            {
                return kvp.Key;
            }
        }
        throw new InvalidOperationException($"Track not found for trackCode={trackCode}");
    }

    /// <summary>
    /// Gets the previous Bible track.
    /// For non-sectioned publications, navigates through tracks with wrap at start.
    /// For sectioned publications, circular queue at publication level: previous from first track of first section
    /// wraps to last track of last section (not within a section).
    /// </summary>
    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(
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
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(currentSection, previousTrack.Value);
        }

        // No previous track in this section: go to previous section (wrap within same publication).
        // Cross-publication wrap is not done here because the caller builds the play item with the
        // current publication code and would fail (e.g. "Track not found: pub=CurrentPub, track=OtherPubTrackCode").
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

        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(previousSection.Value, tracks.Last().Value);
    }

    /// <summary>
    /// Gets the previous Bible section in publication order.
    /// Circular: previous of first section is the last section.
    /// Checks discovered sections and catalogs missing ones if needed.
    /// </summary>
    public async Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(
        string languageCode,
        string publicationCode,
        string sectionCode,
        IFetchProgress? sectionFetchProgress = null)
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

        // Order discovered section codes by natural comparer
        var orderedDiscoveredKeys = discoveredSectionCodes.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();
        var currentIndex = orderedDiscoveredKeys.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Bible section not found in discovered sections: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        // Try to find a valid previous section by iterating through discovered sections
        // Skip sections that can't be cataloged
        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        var prevIndex = (currentIndex - 1 + orderedDiscoveredKeys.Count) % orderedDiscoveredKeys.Count;
        var attempts = 0;
        var maxAttempts = orderedDiscoveredKeys.Count;

        while (attempts < maxAttempts)
        {
            var prevSectionCode = orderedDiscoveredKeys[prevIndex];

            // Check if section is already cataloged
            if (sections.TryGetValue(prevSectionCode, out var prevSection))
            {
                var tracks = await GetTracksCachedAsync(languageCode, publicationCode, prevSectionCode);
                if (tracks.Count > 0)
                {
                    return new KeyValuePair<string, BiblePublicationSection>(prevSectionCode, prevSection);
                }
            }

            var cataloged = await SectionCataloger.EnsureSectionCatalogedAsync(
                languageCode,
                publicationCode,
                prevSectionCode,
                sectionFetchProgress,
                () => { lock (cacheLock) { sectionsCache.Remove(new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode)); } },
                () => { lock (cacheLock) { tracksCache.Remove(new TracksCacheKey(languageCode.ToUpperInvariant(), publicationCode, prevSectionCode.ToUpperInvariant())); } });
            if (cataloged)
            {
                sections = await GetSectionsCachedAsync(languageCode, publicationCode);

                if (sections.TryGetValue(prevSectionCode, out prevSection))
                {
                    // Verify tracks exist after cataloging
                    var tracks = await GetTracksCachedAsync(languageCode, publicationCode, prevSectionCode);
                    if (tracks.Count > 0)
                    {
                        return new KeyValuePair<string, BiblePublicationSection>(prevSectionCode, prevSection);
                    }
                }
            }

            // Move to next previous section (wrap around)
            prevIndex = (prevIndex - 1 + orderedDiscoveredKeys.Count) % orderedDiscoveredKeys.Count;
            attempts++;
        }

        throw new InvalidOperationException($"No valid previous section found after attempting {maxAttempts} sections: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
    }

    /// <summary>
    /// Gets the next Bible section in publication order.
    /// Circular: next of last section is the first section.
    /// Checks discovered sections and catalogs missing ones if needed.
    /// </summary>
    public async Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(
        string languageCode,
        string publicationCode,
        string sectionCode,
        IFetchProgress? sectionFetchProgress = null)
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

        // Order discovered section codes by natural comparer
        var orderedDiscoveredKeys = discoveredSectionCodes.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();
        var currentIndex = orderedDiscoveredKeys.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Bible section not found in discovered sections: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        // Try to find a valid next section by iterating through discovered sections
        // Skip sections that can't be cataloged
        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        var nextIndex = (currentIndex + 1) % orderedDiscoveredKeys.Count;
        var attempts = 0;
        var maxAttempts = orderedDiscoveredKeys.Count;

        while (attempts < maxAttempts)
        {
            var nextSectionCode = orderedDiscoveredKeys[nextIndex];

            // Check if section is already cataloged
            if (sections.TryGetValue(nextSectionCode, out var nextSection))
            {
                var tracks = await GetTracksCachedAsync(languageCode, publicationCode, nextSectionCode);
                if (tracks.Count > 0)
                {
                    return new KeyValuePair<string, BiblePublicationSection>(nextSectionCode, nextSection);
                }
            }

            var cataloged = await SectionCataloger.EnsureSectionCatalogedAsync(
                languageCode,
                publicationCode,
                nextSectionCode,
                sectionFetchProgress,
                () => { lock (cacheLock) { sectionsCache.Remove(new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode)); } },
                () => { lock (cacheLock) { tracksCache.Remove(new TracksCacheKey(languageCode.ToUpperInvariant(), publicationCode, nextSectionCode.ToUpperInvariant())); } });
            if (cataloged)
            {
                sections = await GetSectionsCachedAsync(languageCode, publicationCode);

                if (sections.TryGetValue(nextSectionCode, out nextSection))
                {
                    // Verify tracks exist after cataloging
                    var tracks = await GetTracksCachedAsync(languageCode, publicationCode, nextSectionCode);
                    if (tracks.Count > 0)
                    {
                        return new KeyValuePair<string, BiblePublicationSection>(nextSectionCode, nextSection);
                    }
                }
            }

            // Move to next section (wrap around)
            nextIndex = (nextIndex + 1) % orderedDiscoveredKeys.Count;
            attempts++;
        }

        throw new InvalidOperationException($"No valid next section found after attempting {maxAttempts} sections: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
    }

    private async Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?> GetFirstTrackOfPublicationAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? sectionFetchProgress = null)
    {
        var pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
        if (pubWithTracks?.Tracks != null && pubWithTracks.Tracks.Count > 0)
        {
            var first = pubWithTracks.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).First();
            return (null, first);
        }

        if (languageContentService != null && (pubWithTracks == null || pubWithTracks.Tracks == null || pubWithTracks.Tracks.Count == 0))
        {
            var ensured = await languageContentService.EnsurePublicationExistsAsync(
                publicationCode,
                languageCode,
                CancellationToken.None,
                sectionFetchProgress);
            if (ensured)
            {
                biblePublicationService.InvalidatePublicationCaches(languageCode, publicationCode);
                pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
                if (pubWithTracks?.Tracks != null && pubWithTracks.Tracks.Count > 0)
                {
                    var first = pubWithTracks.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).First();
                    return (null, first);
                }
            }
        }

        var discoveredSectionCodes = await SectionCataloger.GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
        if (discoveredSectionCodes.Count == 0)
        {
            return null;
        }

        var orderedSectionCodes = discoveredSectionCodes.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();

        foreach (var candidateSectionCode in orderedSectionCodes)
        {
            var cataloged = await SectionCataloger.EnsureSectionCatalogedAsync(
                languageCode,
                publicationCode,
                candidateSectionCode,
                sectionFetchProgress,
                () => { lock (cacheLock) { sectionsCache.Remove(new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode)); } },
                () => { lock (cacheLock) { tracksCache.Remove(new TracksCacheKey(languageCode.ToUpperInvariant(), publicationCode, candidateSectionCode.ToUpperInvariant())); } });
            if (!cataloged)
            {
                continue;
            }

            var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
            var tracks = await GetTracksCachedAsync(languageCode, publicationCode, candidateSectionCode);
            if (sections.TryGetValue(candidateSectionCode, out var section) && tracks.Count > 0)
            {
                return (section, tracks.ElementAt(0).Value);
            }

            logger?.Information("GetFirstTrackOfPublicationAsync: Section {SectionCode} has no tracks after catalog, trying next section",
                candidateSectionCode);
        }

        return null;
    }

    private async Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?> GetLastTrackOfPublicationAsync(
        string languageCode,
        string publicationCode,
        IFetchProgress? sectionFetchProgress = null)
    {
        var pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
        if (pubWithTracks?.Tracks != null && pubWithTracks.Tracks.Count > 0)
        {
            var last = pubWithTracks.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).Last();
            return (null, last);
        }

        if (languageContentService != null && (pubWithTracks == null || pubWithTracks.Tracks == null || pubWithTracks.Tracks.Count == 0))
        {
            var ensured = await languageContentService.EnsurePublicationExistsAsync(
                publicationCode,
                languageCode,
                CancellationToken.None,
                sectionFetchProgress);
            if (ensured)
            {
                biblePublicationService.InvalidatePublicationCaches(languageCode, publicationCode);
                pubWithTracks = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
                if (pubWithTracks?.Tracks != null && pubWithTracks.Tracks.Count > 0)
                {
                    var last = pubWithTracks.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).Last();
                    return (null, last);
                }
            }
        }

        var discoveredSectionCodes = await SectionCataloger.GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
        if (discoveredSectionCodes.Count == 0)
        {
            return null;
        }

        var orderedSectionCodes = discoveredSectionCodes.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();

        for (var i = orderedSectionCodes.Count - 1; i >= 0; i--)
        {
            var candidateSectionCode = orderedSectionCodes[i];

            var cataloged = await SectionCataloger.EnsureSectionCatalogedAsync(
                languageCode,
                publicationCode,
                candidateSectionCode,
                sectionFetchProgress,
                () => { lock (cacheLock) { sectionsCache.Remove(new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode)); } },
                () => { lock (cacheLock) { tracksCache.Remove(new TracksCacheKey(languageCode.ToUpperInvariant(), publicationCode, candidateSectionCode.ToUpperInvariant())); } });
            if (!cataloged)
            {
                continue;
            }

            var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
            var tracks = await GetTracksCachedAsync(languageCode, publicationCode, candidateSectionCode);
            if (sections.TryGetValue(candidateSectionCode, out var section) && tracks.Count > 0)
            {
                return (section, tracks.Last().Value);
            }

            logger?.Information("GetLastTrackOfPublicationAsync: Section {SectionCode} has no tracks after catalog, trying previous section",
                candidateSectionCode);
        }

        return null;
    }

}
