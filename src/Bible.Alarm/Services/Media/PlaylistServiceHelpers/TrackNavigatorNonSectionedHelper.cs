#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles next/previous track navigation for non-sectioned publications (dramas, videos).
/// When at end/start of publication, can move to next/previous publication in category (same language) if not Bible and not Music.
/// </summary>
public sealed class TrackNavigatorNonSectionedHelper
{
    private readonly IBiblePublicationService biblePublicationService;
    private readonly Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getFirstTrackOfPublicationAsync;
    private readonly Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getLastTrackOfPublicationAsync;

    public TrackNavigatorNonSectionedHelper(
        IBiblePublicationService biblePublicationService,
        Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getFirstTrackOfPublicationAsync = null,
        Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getLastTrackOfPublicationAsync = null)
    {
        this.biblePublicationService = biblePublicationService;
        this.getFirstTrackOfPublicationAsync = getFirstTrackOfPublicationAsync;
        this.getLastTrackOfPublicationAsync = getLastTrackOfPublicationAsync;
    }

    public async System.Threading.Tasks.Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextAsync(
        string languageCode,
        string publicationCode,
        string trackCode,
        IFetchProgress? sectionFetchProgress = null)
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

        // At last track: wrap within same publication only. Cross-publication wrap would require
        // the caller to use the other publication's code when building the play item, which is not
        // supported by the current API.
        return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(null, orderedTracks.First());
    }

    public async System.Threading.Tasks.Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousAsync(
        string languageCode,
        string publicationCode,
        string trackCode,
        IFetchProgress? sectionFetchProgress = null)
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

        // At first track: wrap within same publication only. Cross-publication wrap would require
        // the caller to use the other publication's code when building the play item, which is not
        // supported by the current API and would fail (e.g. "Track not found: pub=CurrentPub, track=OtherPubTrackCode").
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
