#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Bible.Alarm.Common;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Network.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory? scopeFactory;
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
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

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
        sectionFetchProgress?.UpdateProgress(0.0);
        try
        {
            // Handle non-sectioned publications (dramas, videos)
            if (string.IsNullOrWhiteSpace(sectionCode))
            {
                return await GetNextNonSectionedTrack(languageCode, publicationCode, trackCode);
            }

            // Sectioned publication: circle within the whole publication (all sections), not within a section.
            var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
            if (string.IsNullOrEmpty(normalizedSectionCode))
            {
                return await GetNextNonSectionedTrack(languageCode, publicationCode, trackCode);
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

            // No next track in this section: go to next section (wraps to first section at end of publication).
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
        finally
        {
            sectionFetchProgress?.UpdateProgress(1.0);
        }
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
        sectionFetchProgress?.UpdateProgress(0.0);
        try
        {
            // Handle non-sectioned publications (dramas, videos)
            if (string.IsNullOrWhiteSpace(sectionCode))
            {
                return await GetPreviousNonSectionedTrack(languageCode, publicationCode, trackCode);
            }

            // Sectioned publication: circle within the whole publication (all sections), not within a section.
            var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
            if (string.IsNullOrEmpty(normalizedSectionCode))
            {
                return await GetPreviousNonSectionedTrack(languageCode, publicationCode, trackCode);
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

            // No previous track in this section: go to previous section (wraps to last section at start of publication).
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

            // Use Last() directly since tracks dictionary is already sorted correctly with TrackCodeComparer
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(previousSection.Value, tracks.Last().Value);
        }
        finally
        {
            sectionFetchProgress?.UpdateProgress(1.0);
        }
    }

    /// <summary>
    /// Gets the next track for a non-sectioned publication (dramas, videos).
    /// Wraps around to the first track when at the end.
    /// </summary>
    private async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextNonSectionedTrack(
        string languageCode,
        string publicationCode,
        string trackCode)
    {
        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode)
            ?? throw new InvalidOperationException($"Publication not found: languageCode={languageCode}, publicationCode={publicationCode}");

        if (publication.Tracks == null || publication.Tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks found for non-sectioned publication: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var orderedTracks = publication.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList();
        var tracksDict = new SortedDictionary<string, BiblePublicationTrack>(orderedTracks.ToDictionary(t => t.TrackCode, t => t), TrackCodeComparer.Comparer);
        var currentKey = ResolveTrackCodeToKey(tracksDict, trackCode);

        // Find the next track using TrackCodeComparer (tracks dictionary is already sorted correctly)
        var nextTrack = orderedTracks.FirstOrDefault(t => TrackCodeComparer.Comparer.Compare(t.TrackCode, currentKey) > 0);

        if (nextTrack != null)
        {
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, nextTrack);
        }

        // Wrap around to the first track
        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, orderedTracks.First());
    }

    /// <summary>
    /// Gets the previous track for a non-sectioned publication (dramas, videos).
    /// Wraps around to the last track when at the beginning.
    /// </summary>
    private async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousNonSectionedTrack(
        string languageCode,
        string publicationCode,
        string trackCode)
    {
        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode)
            ?? throw new InvalidOperationException($"Publication not found: languageCode={languageCode}, publicationCode={publicationCode}");

        if (publication.Tracks == null || publication.Tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks found for non-sectioned publication: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var orderedTracks = publication.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList();
        var tracksDict = new SortedDictionary<string, BiblePublicationTrack>(orderedTracks.ToDictionary(t => t.TrackCode, t => t), TrackCodeComparer.Comparer);
        var currentKey = ResolveTrackCodeToKey(tracksDict, trackCode);

        // Find the previous track using TrackCodeComparer (tracks dictionary is already sorted correctly)
        var previousTrack = orderedTracks.LastOrDefault(t => TrackCodeComparer.Comparer.Compare(t.TrackCode, currentKey) < 0);

        if (previousTrack != null)
        {
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, previousTrack);
        }

        // Wrap around to the last track.
        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, orderedTracks.Last());
    }

    /// <summary>
    /// Gets the previous Bible section in publication order.
    /// Circular: previous of first section is the last section.
    /// Checks discovered sections and harvests missing ones if needed.
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

        // Get all discovered section codes for this publication+language
        var discoveredSectionCodes = await GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
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
        // Skip sections that can't be harvested
        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        var prevIndex = (currentIndex - 1 + orderedDiscoveredKeys.Count) % orderedDiscoveredKeys.Count;
        var attempts = 0;
        var maxAttempts = orderedDiscoveredKeys.Count;

        while (attempts < maxAttempts)
        {
            var prevSectionCode = orderedDiscoveredKeys[prevIndex];

            // Check if section is already harvested
            if (sections.TryGetValue(prevSectionCode, out var prevSection))
            {
                var tracks = await GetTracksCachedAsync(languageCode, publicationCode, prevSectionCode);
                if (tracks.Count > 0)
                {
                    return new KeyValuePair<string, BiblePublicationSection>(prevSectionCode, prevSection);
                }
            }

            // Try to harvest the section
            var harvested = await EnsureSectionHarvestedAsync(languageCode, publicationCode, prevSectionCode, sectionFetchProgress);
            if (harvested)
            {
                // Clear cache and reload sections
                var key = new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode);
                lock (cacheLock)
                {
                    sectionsCache.Remove(key);
                }
                sections = await GetSectionsCachedAsync(languageCode, publicationCode);

                if (sections.TryGetValue(prevSectionCode, out prevSection))
                {
                    // Verify tracks exist after harvesting
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
    /// Checks discovered sections and harvests missing ones if needed.
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

        // Get all discovered section codes for this publication+language
        var discoveredSectionCodes = await GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
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
        // Skip sections that can't be harvested
        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        var nextIndex = (currentIndex + 1) % orderedDiscoveredKeys.Count;
        var attempts = 0;
        var maxAttempts = orderedDiscoveredKeys.Count;

        while (attempts < maxAttempts)
        {
            var nextSectionCode = orderedDiscoveredKeys[nextIndex];

            // Check if section is already harvested
            if (sections.TryGetValue(nextSectionCode, out var nextSection))
            {
                var tracks = await GetTracksCachedAsync(languageCode, publicationCode, nextSectionCode);
                if (tracks.Count > 0)
                {
                    return new KeyValuePair<string, BiblePublicationSection>(nextSectionCode, nextSection);
                }
            }

            // Try to harvest the section
            var harvested = await EnsureSectionHarvestedAsync(languageCode, publicationCode, nextSectionCode, sectionFetchProgress);
            if (harvested)
            {
                // Clear cache and reload sections
                var key = new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode);
                lock (cacheLock)
                {
                    sectionsCache.Remove(key);
                }
                sections = await GetSectionsCachedAsync(languageCode, publicationCode);

                if (sections.TryGetValue(nextSectionCode, out nextSection))
                {
                    // Verify tracks exist after harvesting
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

    /// <summary>
    /// Gets all discovered section codes for a publication+language from SectionLanguages table.
    /// For no-language publications (LanguageId == null), queries sections directly from BiblePublicationSections.
    /// </summary>
    private async Task<List<string>> GetDiscoveredSectionCodesAsync(string languageCode, string publicationCode)
    {
        // Check if this is a no-language publication (like iam)
        // For no-language publications, query sections directly instead of using SectionLanguages
        var isNoLanguagePublication = await IsPublicationWithoutLanguageAsync(publicationCode);
        if (isNoLanguagePublication)
        {
            // For no-language publications, use harvested sections directly
            var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
            return sections.Keys.ToList();
        }

        if (scopeFactory == null)
        {
            // Fallback: use harvested sections only if we can't query discovered sections
            var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
            return sections.Keys.ToList();
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();
            var normalizedPublicationCode = publicationCode.ToLowerInvariant();

            // Handle case-sensitive publication codes for dramas
            var isDrama = Bible.Alarm.Shared.Helpers.PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // Get all section codes from SectionLanguages for this publication+language
            var sectionCodes = await db.SectionLanguages
                .AsNoTracking()
                .Include(sl => sl.Language)
                .Where(sl => sl.PublicationCode == publicationCodeForDb &&
                           sl.Language != null &&
                           sl.Language.LanguageCode == normalizedLanguageCode)
                .Select(sl => sl.SectionCode)
                .Distinct()
                .ToListAsync();

            return sectionCodes;
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Failed to get discovered section codes, falling back to harvested sections: languageCode={LanguageCode}, publicationCode={PublicationCode}",
                languageCode, publicationCode);
            // Fallback: use harvested sections only
            var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
            return sections.Keys.ToList();
        }
    }

    /// <summary>
    /// Checks if a publication is a no-language publication (LanguageId == null).
    /// </summary>
    private async Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode)
    {
        if (scopeFactory == null)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var isDrama = Bible.Alarm.Shared.Helpers.PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            // Check if publication has LanguageId == null
            var isNoLanguage = await db.BiblePublications
                .AsNoTracking()
                .AnyAsync(bp => bp.PublicationCode == publicationCodeForDb && bp.LanguageId == null);

            if (isNoLanguage)
            {
                return true;
            }

            // Also check PublicationLanguages for entries with LanguageId == null
            return await db.PublicationLanguages
                .AsNoTracking()
                .AnyAsync(pl => pl.PublicationCode == publicationCodeForDb && pl.LanguageId == null);
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Failed to check if publication is no-language: publicationCode={PublicationCode}",
                publicationCode);
            return false;
        }
    }

    /// <summary>
    /// Ensures a section is harvested. If it's not harvested but exists in discovered sections, harvests it.
    /// Returns true if section is available (harvested or successfully harvested), false otherwise.
    /// </summary>
    private async Task<bool> EnsureSectionHarvestedAsync(string languageCode, string publicationCode, string sectionCode, IFetchProgress? sectionFetchProgress = null)
    {
        // No-language publications (like iam) cannot be ad-hoc harvested with a language code
        // They should be pre-harvested by the harvester tool
        var isNoLanguagePublication = await IsPublicationWithoutLanguageAsync(publicationCode);
        if (isNoLanguagePublication)
        {
            logger?.Debug("Publication {PublicationCode} is a no-language publication, cannot ad-hoc harvest sections. Section should be pre-harvested.",
                publicationCode);
            // Just check if the section exists in harvested sections
            var noLanguageSections = await GetSectionsCachedAsync(languageCode, publicationCode);
            return noLanguageSections.ContainsKey(sectionCode);
        }

        if (languageContentService == null || scopeFactory == null)
        {
            logger?.Warning("ILanguageContentService or IServiceScopeFactory not available, cannot harvest section: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);
            return false;
        }

        // Verify section exists in discovered sections before attempting to harvest
        var discoveredSectionCodes = await GetDiscoveredSectionCodesAsync(languageCode, publicationCode);
        if (!discoveredSectionCodes.Any(sc => string.Equals(sc, sectionCode, StringComparison.OrdinalIgnoreCase)))
        {
            logger?.Warning("Section {SectionCode} not found in discovered sections for languageCode={LanguageCode}, publicationCode={PublicationCode}",
                sectionCode, languageCode, publicationCode);
            return false;
        }

        // Check if section is already harvested
        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        if (sections.ContainsKey(sectionCode))
        {
            // Already harvested, check if it has tracks
            var tracks = await GetTracksCachedAsync(languageCode, publicationCode, sectionCode);
            if (tracks.Count > 0)
            {
                return true; // Section and tracks already exist
            }
        }

        // Section not harvested or missing tracks, harvest it
        // Note: FetchSectionTracksAsync requires the section entity to exist first.
        // We need to create the section entity if it doesn't exist.
        var networkStatusService = ServiceProviderManager.GetService<INetworkStatusService>();
        if (networkStatusService != null && !await networkStatusService.IsInternetAvailable())
        {
            logger?.Warning("No internet - cannot harvest section for navigation: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);
            return false;
        }

        try
        {
            logger?.Information("Harvesting section for navigation: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);

            sectionFetchProgress?.UpdateProgress(0.0);

            var publicationProgress = sectionFetchProgress != null
                ? new Bible.Alarm.Common.Helpers.ScaledFetchProgressAdapter(sectionFetchProgress, 0.5)
                : null;

            var publicationExists = await languageContentService.EnsurePublicationExistsAsync(
                publicationCode,
                languageCode,
                CancellationToken.None,
                publicationProgress);

            if (!publicationExists)
            {
                logger?.Warning("Failed to ensure publication exists: languageCode={LanguageCode}, publicationCode={PublicationCode}",
                    languageCode, publicationCode);
                return false;
            }

            sectionFetchProgress?.UpdateProgress(0.5);

            // Check if section entity exists, create it if it doesn't
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedPublicationCode = publicationCode.ToLowerInvariant();
            var normalizedSectionCode = sectionCode.ToLowerInvariant();
            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            var isDrama = Bible.Alarm.Shared.Helpers.PublicationTypeHelper.IsDrama(normalizedPublicationCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = normalizedPublicationCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = normalizedPublicationCode;
            }

            var publication = await db.BiblePublications
                .Include(bp => bp.Sections)
                .FirstOrDefaultAsync(
                    bp => bp.PublicationCode == publicationCodeForDb &&
                          bp.Language != null &&
                          bp.Language.LanguageCode == normalizedLanguageCode);

            if (publication == null)
            {
                logger?.Warning("Publication {PublicationCode} not found after EnsurePublicationExistsAsync: languageCode={LanguageCode}",
                    publicationCode, languageCode);
                return false;
            }

            // Check if section entity exists
            var section = publication.Sections.FirstOrDefault(s =>
                s.SectionCode.Equals(normalizedSectionCode, StringComparison.OrdinalIgnoreCase));

            if (section == null)
            {
                // Section entity doesn't exist, create it
                // We'll fetch the section name from the API when fetching tracks
                logger?.Information("Section entity doesn't exist, creating it: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
                    languageCode, publicationCode, sectionCode);

                section = new BiblePublicationSection
                {
                    Name = sectionCode, // Temporary name, will be updated when fetching tracks
                    SectionCode = normalizedSectionCode,
                    BiblePublication = publication,
                    BiblePublicationId = publication.Id,
                    Tracks = new List<BiblePublicationTrack>()
                };

                publication.Sections.Add(section);
                await db.SaveChangesAsync();
            }

            // Now fetch tracks for the section
            var success = await languageContentService.FetchSectionTracksAsync(
                publicationCode,
                sectionCode,
                languageCode,
                CancellationToken.None);

            sectionFetchProgress?.UpdateProgress(1.0);

            if (success)
            {
                // Reload section from database to verify name was updated
                // Use ToLower() comparison instead of Equals with StringComparison for EF Core translation
                var reloadedSection = await db.BiblePublicationSections
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.BiblePublicationId == publication.Id && 
                             s.SectionCode.ToLower() == normalizedSectionCode.ToLower());

                if (reloadedSection != null)
                {
                    logger?.Information("Successfully harvested section: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}, sectionName={SectionName}",
                        languageCode, publicationCode, sectionCode, reloadedSection.Name);
                }
                else
                {
                    logger?.Warning("Section harvested but could not reload from database: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
                        languageCode, publicationCode, sectionCode);
                }

                // Clear cache so next call will get the newly harvested section
                var key = new SectionsCacheKey(languageCode.ToUpperInvariant(), publicationCode);
                var tracksKey = new TracksCacheKey(languageCode.ToUpperInvariant(), publicationCode, sectionCode.ToUpperInvariant());
                lock (cacheLock)
                {
                    sectionsCache.Remove(key);
                    tracksCache.Remove(tracksKey);
                }

                return true;
            }
            else
            {
                logger?.Warning("Failed to harvest section: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
                    languageCode, publicationCode, sectionCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "Error harvesting section: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);
            return false;
        }
    }
}
