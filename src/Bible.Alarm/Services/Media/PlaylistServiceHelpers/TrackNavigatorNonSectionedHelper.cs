#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
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
    private readonly ILogger logger;

    public TrackNavigatorNonSectionedHelper(
        IBiblePublicationService biblePublicationService,
        ILogger logger,
        Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getFirstTrackOfPublicationAsync = null,
        Func<string, string, IFetchProgress?, System.Threading.Tasks.Task<(BiblePublicationSection?, BiblePublicationTrack)?>>? getLastTrackOfPublicationAsync = null)
    {
        this.biblePublicationService = biblePublicationService;
        this.logger = logger;
        this.getFirstTrackOfPublicationAsync = getFirstTrackOfPublicationAsync;
        this.getLastTrackOfPublicationAsync = getLastTrackOfPublicationAsync;
    }

    public async System.Threading.Tasks.Task<TrackNavigationResult> GetNextAsync(
        string languageCode,
        string publicationCode,
        string trackCode,
        IFetchProgress? sectionFetchProgress = null)
    {
        var (orderedTracks, currentKey) =
            await PrepareNavigationStateAsync(languageCode, publicationCode, trackCode).ConfigureAwait(false);

        var nextTrack = orderedTracks.FirstOrDefault(t => TrackCodeComparer.Comparer.Compare(t.TrackCode, currentKey) > 0);

        if (nextTrack != null)
        {
            return new TrackNavigationResult(publicationCode, null, nextTrack);
        }

        var crossPublication = await TryNavigateAcrossCategoryPublicationsAsync(languageCode, publicationCode,
            navigateForward: true, sectionFetchProgress).ConfigureAwait(false);

        return crossPublication ?? new TrackNavigationResult(publicationCode, null, orderedTracks[0]);
    }

    public async System.Threading.Tasks.Task<TrackNavigationResult> GetPreviousAsync(
        string languageCode,
        string publicationCode,
        string trackCode,
        IFetchProgress? sectionFetchProgress = null)
    {
        var (orderedTracks, currentKey) =
            await PrepareNavigationStateAsync(languageCode, publicationCode, trackCode).ConfigureAwait(false);

        var previousTrack = orderedTracks.LastOrDefault(t => TrackCodeComparer.Comparer.Compare(t.TrackCode, currentKey) < 0);

        if (previousTrack != null)
        {
            return new TrackNavigationResult(publicationCode, null, previousTrack);
        }

        var crossPublication = await TryNavigateAcrossCategoryPublicationsAsync(languageCode, publicationCode,
            navigateForward: false, sectionFetchProgress).ConfigureAwait(false);

        return crossPublication ?? new TrackNavigationResult(publicationCode, null, orderedTracks[^1]);
    }

    private async System.Threading.Tasks.Task<(List<BiblePublicationTrack> Ordered, string CurrentKey)>
        PrepareNavigationStateAsync(string languageCode, string publicationCode, string trackCode)
    {
        var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode)
            ?? throw new InvalidOperationException($"Publication not found: languageCode={languageCode}, publicationCode={publicationCode}");

        if (publication.Tracks == null || publication.Tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks found for non-sectioned publication: languageCode={languageCode}, publicationCode={publicationCode}");
        }

        var orderedTracks = publication.Tracks.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList();
        var tracksDict = new SortedDictionary<string, BiblePublicationTrack>(orderedTracks.ToDictionary(t => t.TrackCode, t => t, StringComparer.Ordinal), TrackCodeComparer.Comparer);
        var currentKey = ResolveTrackCodeToKey(tracksDict, trackCode);
        return (orderedTracks, currentKey);
    }

    private async System.Threading.Tasks.Task<TrackNavigationResult?> TryNavigateAcrossCategoryPublicationsAsync(
        string languageCode,
        string publicationCode,
        bool navigateForward,
        IFetchProgress? sectionFetchProgress)
    {
        var getEndpoint = navigateForward ? getFirstTrackOfPublicationAsync : getLastTrackOfPublicationAsync;
        if (getEndpoint == null)
        {
            return null;
        }

        var categoryInfo = await biblePublicationService.GetPublicationCategoryInfoAsync(languageCode, publicationCode);
        if (categoryInfo is not { } info || string.IsNullOrWhiteSpace(info.CategoryCode) ||
            string.Equals(info.CategoryCode, AppConstants.Media.BiblePublicationCategoryBible, StringComparison.OrdinalIgnoreCase) || info.IsMusic)
        {
            return null;
        }

        var orderedPubCodes = await biblePublicationService.GetPublicationCodesInCategoryOrderAsync(languageCode, info.CategoryCode);
        var pubIndex = orderedPubCodes.FindIndex(c => string.Equals(c, publicationCode, StringComparison.OrdinalIgnoreCase));
        if (pubIndex < 0)
        {
            return null;
        }

        var edgeLabel = navigateForward ? "first" : "last";

        for (var step = 1; step <= orderedPubCodes.Count; step++)
        {
            var adjacentIndex = navigateForward
                ? (pubIndex + step) % orderedPubCodes.Count
                : (pubIndex - step + orderedPubCodes.Count) % orderedPubCodes.Count;
            var adjacentPubCode = orderedPubCodes[adjacentIndex];
            var adjacentInfo = await biblePublicationService.GetPublicationCategoryInfoAsync(languageCode, adjacentPubCode);
            if (adjacentInfo is { } adj && adj.IsMusic)
            {
                continue;
            }

            try
            {
                var corner = await getEndpoint(languageCode, adjacentPubCode, sectionFetchProgress);
                if (corner is { } c)
                {
                    return new TrackNavigationResult(adjacentPubCode, c.Item1, c.Item2);
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Failed to get {Edge} track for adjacent pub {PublicationCode}, skipping", edgeLabel, adjacentPubCode);
            }
        }

        return null;
    }

    private static string ResolveTrackCodeToKey(SortedDictionary<string, BiblePublicationTrack> tracks, string trackCode)
    {
        if (tracks.ContainsKey(trackCode))
        {
            return trackCode;
        }
        return tracks.First(kvp => TrackCodeHelper.GetFromTrack(kvp.Value) == trackCode).Key;
    }
}
