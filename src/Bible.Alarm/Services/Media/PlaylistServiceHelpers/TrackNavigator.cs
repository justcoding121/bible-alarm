#nullable enable
using System.Globalization;
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles Bible track and section navigation.
/// </summary>
public sealed class TrackNavigator(IMediaService mediaService, IBiblePublicationService biblePublicationService)
{
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
        string trackCode)
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
        var nextSection = await GetNextBiblePublicationSection(languageCode, publicationCode, normalizedSectionCode);
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
        string trackCode)
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
        var previousSection = await GetPreviousBiblePublicationSection(languageCode, publicationCode, normalizedSectionCode);
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
    /// </summary>
    public async Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(
        string languageCode,
        string publicationCode,
        string sectionCode)
    {
        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        if (sections.Count == 0)
        {
            throw new InvalidOperationException($"No bible sections found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        if (string.IsNullOrEmpty(normalizedSectionCode))
        {
            throw new InvalidOperationException($"Invalid section code: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        // Order section keys by natural comparer so index 0 = first section (e.g. Genesis), last index = last section (e.g. Revelation).
        var orderedKeys = sections.Keys.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();
        var currentIndex = orderedKeys.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        // Circular wrap at publication level: previous of first section = last section.
        var prevIndex = (currentIndex - 1 + orderedKeys.Count) % orderedKeys.Count;
        var prevKey = orderedKeys[prevIndex];
        if (!sections.TryGetValue(prevKey, out var prevSection))
        {
            throw new InvalidOperationException($"Bible section with key {prevKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<string, BiblePublicationSection>(prevKey, prevSection);
    }

    /// <summary>
    /// Gets the next Bible section in publication order.
    /// Circular: next of last section is the first section.
    /// </summary>
    public async Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(
        string languageCode,
        string publicationCode,
        string sectionCode)
    {
        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        if (sections.Count == 0)
        {
            throw new InvalidOperationException($"No bible sections found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        if (string.IsNullOrEmpty(normalizedSectionCode))
        {
            throw new InvalidOperationException($"Invalid section code: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        // Order section keys by natural comparer so index 0 = first section, last index = last section.
        var orderedKeys = sections.Keys.OrderBy(k => k, SectionCodeHelper.SectionCodeComparer).ToList();
        var currentIndex = orderedKeys.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        // Circular wrap at publication level: next of last section = first section.
        var nextIndex = (currentIndex + 1) % orderedKeys.Count;
        var nextKey = orderedKeys[nextIndex];
        if (!sections.TryGetValue(nextKey, out var nextSection))
        {
            throw new InvalidOperationException($"Bible section with key {nextKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<string, BiblePublicationSection>(nextKey, nextSection);
    }
}
