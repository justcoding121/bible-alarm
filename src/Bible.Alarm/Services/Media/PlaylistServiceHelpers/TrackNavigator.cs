#nullable enable
using Bible.Alarm.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles Bible track and section navigation.
/// </summary>
public sealed class TrackNavigator(IMediaService mediaService)
{
    /// <summary>
    /// Gets the next Bible track.
    /// </summary>
    public async Task<KeyValuePair<BiblePublicationSection, BiblePublicationTrack>> GetNextBiblePublicationTrack(
        string languageCode,
        string publicationCode,
        int sectionNumber,
        int track)
    {
        var currentSection = await mediaService.GetBiblePublicationSection(languageCode, publicationCode, sectionNumber)
            ?? throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={sectionNumber}");

        var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionNumber);
        var nextTrack = tracks.SkipWhile(kvp => kvp.Key <= track).FirstOrDefault();

        if (!nextTrack.Equals(default(KeyValuePair<int, BiblePublicationTrack>)))
        {
            return new KeyValuePair<BiblePublicationSection, BiblePublicationTrack>(currentSection, nextTrack.Value);
        }

        var nextSection = await GetNextBiblePublicationSection(languageCode, publicationCode, sectionNumber);
        if (nextSection.Value == null)
        {
            throw new InvalidOperationException($"Next bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={sectionNumber}");
        }

        tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, nextSection.Key);
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks in next section: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={nextSection.Key}");
        }

        // Start at the first track of the next section (index 0)
        return new KeyValuePair<BiblePublicationSection, BiblePublicationTrack>(nextSection.Value, tracks.ElementAt(0).Value);
    }

    /// <summary>
    /// Gets the previous Bible track.
    /// </summary>
    public async Task<KeyValuePair<BiblePublicationSection, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(
        string languageCode,
        string publicationCode,
        int sectionNumber,
        int track)
    {
        var currentSection = await mediaService.GetBiblePublicationSection(languageCode, publicationCode, sectionNumber)
            ?? throw new InvalidOperationException($"Bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={sectionNumber}");

        var tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionNumber);
        var previousTrack = tracks.Reverse().SkipWhile(kvp => kvp.Key >= track).FirstOrDefault();

        if (!previousTrack.Equals(default(KeyValuePair<int, BiblePublicationTrack>)))
        {
            return new KeyValuePair<BiblePublicationSection, BiblePublicationTrack>(currentSection, previousTrack.Value);
        }

        var previousSection = await GetPreviousBiblePublicationSection(languageCode, publicationCode, sectionNumber);
        if (previousSection.Value == null)
        {
            throw new InvalidOperationException($"Previous bible section not found: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={sectionNumber}");
        }

        tracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, previousSection.Key);
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks in previous section: languageCode={languageCode}, publicationCode={publicationCode}, sectionNumber={previousSection.Key}");
        }

        return new KeyValuePair<BiblePublicationSection, BiblePublicationTrack>(previousSection.Value, tracks.ElementAt(tracks.Count - 1).Value);
    }

    /// <summary>
    /// Gets the previous Bible section.
    /// </summary>
    public async Task<KeyValuePair<int, BiblePublicationSection>> GetPreviousBiblePublicationSection(
        string languageCode,
        string publicationCode,
        int sectionNumber)
    {
        var sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
        if (sections.Count == 0)
        {
            throw new InvalidOperationException($"No bible sections found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var previousSection = sections.Reverse().SkipWhile(kvp => kvp.Key >= sectionNumber).FirstOrDefault();

        if (!previousSection.Equals(default(KeyValuePair<int, BiblePublicationSection>)))
        {
            return previousSection;
        }

        var maxKey = sections.Keys.Max();
        if (!sections.TryGetValue(maxKey, out var maxSection))
        {
            throw new InvalidOperationException($"Bible section with key {maxKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<int, BiblePublicationSection>(maxKey, maxSection);
    }

    /// <summary>
    /// Gets the next Bible section.
    /// </summary>
    public async Task<KeyValuePair<int, BiblePublicationSection>> GetNextBiblePublicationSection(
        string languageCode,
        string publicationCode,
        int sectionNumber)
    {
        var sections = await mediaService.GetBiblePublicationSections(languageCode, publicationCode);
        if (sections.Count == 0)
        {
            throw new InvalidOperationException($"No bible sections found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var nextSection = sections.SkipWhile(kvp => kvp.Key <= sectionNumber).FirstOrDefault();

        if (!nextSection.Equals(default(KeyValuePair<int, BiblePublicationSection>)))
        {
            return nextSection;
        }

        var minKey = sections.Keys.Min();
        if (!sections.TryGetValue(minKey, out var minSection))
        {
            throw new InvalidOperationException($"Bible section with key {minKey} not found: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        return new KeyValuePair<int, BiblePublicationSection>(minKey, minSection);
    }
}
