#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Handles next/previous track navigation for non-sectioned publications (dramas, videos).
/// Jump to another publication only when current pub is not in Bible category and current pub is not music (IsMusic false).
/// When jumping, only consider target publications that are also not music (IsMusic false) to avoid getting stuck inside a sectioned music pub.
/// If no such target exists or the jump fails, wraps to the last/first track of the same publication.
/// </summary>
public sealed class TrackNavigatorNonSectionedHelper
{
    private readonly IBiblePublicationService biblePublicationService;
    private readonly Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getFirstTrackOfPublicationAsync;
    private readonly Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getLastTrackOfPublicationAsync;
    private readonly ILogger? logger;

    public TrackNavigatorNonSectionedHelper(
        IBiblePublicationService biblePublicationService,
        Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getFirstTrackOfPublicationAsync = null,
        Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getLastTrackOfPublicationAsync = null,
        ILogger? logger = null)
    {
        this.biblePublicationService = biblePublicationService;
        this.getFirstTrackOfPublicationAsync = getFirstTrackOfPublicationAsync;
        this.getLastTrackOfPublicationAsync = getLastTrackOfPublicationAsync;
        this.logger = logger;
    }

    public async System.Threading.Tasks.Task<TrackNavigationResult> GetNextAsync(
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
            return new TrackNavigationResult(publicationCode, null, nextTrack);
        }

        if (getFirstTrackOfPublicationAsync != null)
        {
            var categoryInfo = await biblePublicationService.GetPublicationCategoryInfoAsync(languageCode, publicationCode);
            if (categoryInfo is { } info && !string.IsNullOrWhiteSpace(info.CategoryCode) &&
                !string.Equals(info.CategoryCode, "Bible", StringComparison.OrdinalIgnoreCase) && !info.IsMusic)
            {
                var orderedPubCodes = await biblePublicationService.GetPublicationCodesInCategoryOrderAsync(languageCode, info.CategoryCode);
                var pubIndex = orderedPubCodes.FindIndex(c => string.Equals(c, publicationCode, StringComparison.OrdinalIgnoreCase));
                if (pubIndex >= 0)
                {
                    for (var i = 1; i <= orderedPubCodes.Count; i++)
                    {
                        var nextPubIndex = (pubIndex + i) % orderedPubCodes.Count;
                        var nextPubCode = orderedPubCodes[nextPubIndex];
                        var nextPubInfo = await biblePublicationService.GetPublicationCategoryInfoAsync(languageCode, nextPubCode);
                        if (nextPubInfo is { } np && np.IsMusic)
                        {
                            continue;
                        }

                        try
                        {
                            var firstTrack = await getFirstTrackOfPublicationAsync(languageCode, nextPubCode, sectionFetchProgress);
                            if (firstTrack is { } ft)
                            {
                                return new TrackNavigationResult(nextPubCode, ft.Item1, ft.Item2);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.Debug(ex, "Failed to get first track for next pub {PublicationCode}, skipping", nextPubCode);
                        }
                    }
                }
            }
        }

        return new TrackNavigationResult(publicationCode, null, orderedTracks[0]);
    }

    public async System.Threading.Tasks.Task<TrackNavigationResult> GetPreviousAsync(
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
            return new TrackNavigationResult(publicationCode, null, previousTrack);
        }

        if (getLastTrackOfPublicationAsync != null)
        {
            var categoryInfo = await biblePublicationService.GetPublicationCategoryInfoAsync(languageCode, publicationCode);
            if (categoryInfo is { } info && !string.IsNullOrWhiteSpace(info.CategoryCode) &&
                !string.Equals(info.CategoryCode, "Bible", StringComparison.OrdinalIgnoreCase) && !info.IsMusic)
            {
                var orderedPubCodes = await biblePublicationService.GetPublicationCodesInCategoryOrderAsync(languageCode, info.CategoryCode);
                var pubIndex = orderedPubCodes.FindIndex(c => string.Equals(c, publicationCode, StringComparison.OrdinalIgnoreCase));
                if (pubIndex >= 0)
                {
                    for (var i = 1; i <= orderedPubCodes.Count; i++)
                    {
                        var prevPubIndex = (pubIndex - i + orderedPubCodes.Count) % orderedPubCodes.Count;
                        var prevPubCode = orderedPubCodes[prevPubIndex];
                        var prevPubInfo = await biblePublicationService.GetPublicationCategoryInfoAsync(languageCode, prevPubCode);
                        if (prevPubInfo is { } pp && pp.IsMusic)
                        {
                            continue;
                        }

                        try
                        {
                            var lastTrack = await getLastTrackOfPublicationAsync(languageCode, prevPubCode, sectionFetchProgress);
                            if (lastTrack is { } lt)
                            {
                                return new TrackNavigationResult(prevPubCode, lt.Item1, lt.Item2);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger?.Debug(ex, "Failed to get last track for previous pub {PublicationCode}, skipping", prevPubCode);
                        }
                    }
                }
            }
        }

        return new TrackNavigationResult(publicationCode, null, orderedTracks[^1]);
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

        throw new InvalidOperationException("Sequence contains no matching element.");
    }
}
