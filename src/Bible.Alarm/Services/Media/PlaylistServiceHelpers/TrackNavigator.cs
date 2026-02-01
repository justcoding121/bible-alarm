#nullable enable
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
    private readonly Dictionary<TracksCacheKey, CacheEntry<SortedDictionary<int, BiblePublicationTrack>>> tracksCache = new();
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

    private async Task<SortedDictionary<int, BiblePublicationTrack>> GetTracksCachedAsync(string languageCode, string publicationCode, string? sectionCode)
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
            tracksCache[key] = new CacheEntry<SortedDictionary<int, BiblePublicationTrack>>(now, tracks);
        }

        return tracks;
    }

    /// <summary>
    /// Gets the next Bible track.
    /// For non-sectioned publications (sectionCode == 0), navigates through tracks directly.
    /// For sectioned publications, navigates through tracks within sections.
    /// </summary>
    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextBiblePublicationTrack(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        int track)
    {
        // Handle non-sectioned publications (dramas, videos)
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return await GetNextNonSectionedTrack(languageCode, publicationCode, track);
        }

        // Sectioned publication logic
        //
        // IMPORTANT:
        // Some sectioned publications are stored in the media index WITHOUT a language FK (LanguageId == null),
        // e.g. melody music "iam" with section codes like "iam-1".
        //
        // In that case, calling GetBiblePublicationSection(languageCode, pub, sectionIndex) will fail because
        // the section isn't language-bound. Always resolve "current section" from the sections dictionary
        // returned by GetBiblePublicationSections(...) (which is already no-language aware).
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        if (string.IsNullOrEmpty(normalizedSectionCode))
        {
            return await GetNextNonSectionedTrack(languageCode, publicationCode, track);
        }

        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        if (!sections.TryGetValue(normalizedSectionCode, out var currentSection) || currentSection == null)
        {
            throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        var tracks = await GetTracksCachedAsync(languageCode, publicationCode, normalizedSectionCode);
        var nextTrack = tracks.SkipWhile(kvp => kvp.Key <= track).FirstOrDefault();

        if (!nextTrack.Equals(default(KeyValuePair<int, BiblePublicationTrack>)))
        {
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(currentSection, nextTrack.Value);
        }

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

        // Start at the first track of the next section (index 0)
        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(nextSection.Value, tracks.ElementAt(0).Value);
    }

    /// <summary>
    /// Gets the previous Bible track.
    /// For non-sectioned publications (sectionCode == 0), navigates through tracks directly.
    /// For sectioned publications, navigates through tracks within sections.
    /// </summary>
    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        int track)
    {
        // Handle non-sectioned publications (dramas, videos)
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return await GetPreviousNonSectionedTrack(languageCode, publicationCode, track);
        }

        // Sectioned publication logic
        // See GetNextBiblePublicationTrack for why we resolve sections via GetSectionsCachedAsync.
        var normalizedSectionCode = SectionCodeHelper.Normalize(sectionCode);
        if (string.IsNullOrEmpty(normalizedSectionCode))
        {
            return await GetPreviousNonSectionedTrack(languageCode, publicationCode, track);
        }

        var sections = await GetSectionsCachedAsync(languageCode, publicationCode);
        if (!sections.TryGetValue(normalizedSectionCode, out var currentSection) || currentSection == null)
        {
            throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        var tracks = await GetTracksCachedAsync(languageCode, publicationCode, normalizedSectionCode);
        var previousTrack = tracks.Reverse().SkipWhile(kvp => kvp.Key >= track).FirstOrDefault();

        if (!previousTrack.Equals(default(KeyValuePair<int, BiblePublicationTrack>)))
        {
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(currentSection, previousTrack.Value);
        }

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

        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(previousSection.Value, tracks.ElementAt(tracks.Count - 1).Value);
    }

    /// <summary>
    /// Gets the next track for a non-sectioned publication (dramas, videos).
    /// Wraps around to the first track when at the end.
    /// </summary>
    private async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextNonSectionedTrack(
        string languageCode,
        string publicationCode,
        int currentTrackNumber)
    {
        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode)
            ?? throw new InvalidOperationException($"Publication not found: languageCode={languageCode}, publicationCode={publicationCode}");

        if (publication.Tracks == null || publication.Tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks found for non-sectioned publication: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        // Find the next track
        var orderedTracks = publication.Tracks.OrderBy(t => t.Number).ToList();
        var nextTrack = orderedTracks.FirstOrDefault(t => t.Number > currentTrackNumber);

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
        int currentTrackNumber)
    {
        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode)
            ?? throw new InvalidOperationException($"Publication not found: languageCode={languageCode}, publicationCode={publicationCode}");

        if (publication.Tracks == null || publication.Tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks found for non-sectioned publication: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        // Find the previous track
        var orderedTracks = publication.Tracks.OrderBy(t => t.Number).ToList();
        var previousTrack = orderedTracks.LastOrDefault(t => t.Number < currentTrackNumber);

        if (previousTrack != null)
        {
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, previousTrack);
        }

        // Wrap around to the last track.
        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, orderedTracks.Last());
    }

    /// <summary>
    /// Gets the previous Bible section.
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

        var keys = sections.Keys.ToList();
        var currentIndex = keys.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        var prevIndex = (currentIndex - 1 + keys.Count) % keys.Count;
        var prevKey = keys[prevIndex];
        if (!sections.TryGetValue(prevKey, out var prevSection))
        {
            throw new InvalidOperationException($"Bible section with key {prevKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<string, BiblePublicationSection>(prevKey, prevSection);
    }

    /// <summary>
    /// Gets the next Bible section.
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

        var keys = sections.Keys.ToList();
        var currentIndex = keys.FindIndex(k => string.Equals(k, normalizedSectionCode, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionCode={normalizedSectionCode}");
        }

        var nextIndex = (currentIndex + 1) % keys.Count;
        var nextKey = keys[nextIndex];
        if (!sections.TryGetValue(nextKey, out var nextSection))
        {
            throw new InvalidOperationException($"Bible section with key {nextKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<string, BiblePublicationSection>(nextKey, nextSection);
    }
}
