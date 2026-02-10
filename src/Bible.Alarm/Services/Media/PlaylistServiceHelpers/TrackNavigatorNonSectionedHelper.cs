#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles next/previous track navigation for non-sectioned publications (dramas, videos).
/// </summary>
public sealed class TrackNavigatorNonSectionedHelper
{
    private readonly IBiblePublicationService biblePublicationService;

    public TrackNavigatorNonSectionedHelper(IBiblePublicationService biblePublicationService)
    {
        this.biblePublicationService = biblePublicationService;
    }

    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextAsync(
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

        var nextTrack = orderedTracks.FirstOrDefault(t => TrackCodeComparer.Comparer.Compare(t.TrackCode, currentKey) > 0);

        if (nextTrack != null)
        {
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, nextTrack);
        }

        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, orderedTracks.First());
    }

    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousAsync(
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

        var previousTrack = orderedTracks.LastOrDefault(t => TrackCodeComparer.Comparer.Compare(t.TrackCode, currentKey) < 0);

        if (previousTrack != null)
        {
            return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, previousTrack);
        }

        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, orderedTracks.Last());
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
}
