#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers.TrackNavigatorHelpers;

/// <summary>
/// Handles cross-publication navigation for non-Bible, non-Music sectioned publications.
/// When a sectioned publication exhausts all sections/tracks, this helper advances to the
/// next (or previous) publication in the same category.
/// </summary>
public sealed class TrackNavigatorCrossPublicationHelper
{
    private readonly IBiblePublicationService biblePublicationService;
    private readonly Func<string, string, IFetchProgress?, Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?>> getFirstTrackOfPublicationAsync;
    private readonly Func<string, string, IFetchProgress?, Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?>> getLastTrackOfPublicationAsync;
    private readonly ILogger? logger;

    public TrackNavigatorCrossPublicationHelper(
        IBiblePublicationService biblePublicationService,
        Func<string, string, IFetchProgress?, Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?>> getFirstTrackOfPublicationAsync,
        Func<string, string, IFetchProgress?, Task<(BiblePublicationSection? Section, BiblePublicationTrack Track)?>> getLastTrackOfPublicationAsync,
        ILogger? logger = null)
    {
        this.biblePublicationService = biblePublicationService;
        this.getFirstTrackOfPublicationAsync = getFirstTrackOfPublicationAsync;
        this.getLastTrackOfPublicationAsync = getLastTrackOfPublicationAsync;
        this.logger = logger;
    }

    /// <summary>
    /// Tries to advance to the first track of the next publication in the same category.
    /// Returns null if the category is Bible or Music, or if no valid next publication is found.
    /// </summary>
    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>?> TryGetNextAsync(
        string languageCode, string publicationCode, IFetchProgress? sectionFetchProgress)
    {
        var target = await FindTargetPublicationAsync(languageCode, publicationCode, direction: 1);
        if (target == null)
        {
            return null;
        }

        foreach (var nextPubCode in target)
        {
            try
            {
                var firstTrack = await getFirstTrackOfPublicationAsync(languageCode, nextPubCode, sectionFetchProgress);
                if (firstTrack is { } ft)
                {
                    logger?.Information("Cross-publication next: {FromPub} -> {ToPub}", publicationCode, nextPubCode);
                    return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(ft.Section, ft.Track);
                }
            }
            catch (Exception ex)
            {
                logger?.Debug(ex, "Failed to get first track for next pub {PublicationCode}, skipping", nextPubCode);
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to go back to the last track of the previous publication in the same category.
    /// Returns null if the category is Bible or Music, or if no valid previous publication is found.
    /// </summary>
    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>?> TryGetPreviousAsync(
        string languageCode, string publicationCode, IFetchProgress? sectionFetchProgress)
    {
        var target = await FindTargetPublicationAsync(languageCode, publicationCode, direction: -1);
        if (target == null)
        {
            return null;
        }

        foreach (var prevPubCode in target)
        {
            try
            {
                var lastTrack = await getLastTrackOfPublicationAsync(languageCode, prevPubCode, sectionFetchProgress);
                if (lastTrack is { } lt)
                {
                    logger?.Information("Cross-publication previous: {FromPub} -> {ToPub}", publicationCode, prevPubCode);
                    return new KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>(lt.Section, lt.Track);
                }
            }
            catch (Exception ex)
            {
                logger?.Debug(ex, "Failed to get last track for previous pub {PublicationCode}, skipping", prevPubCode);
            }
        }

        return null;
    }

    private async Task<List<string>?> FindTargetPublicationAsync(
        string languageCode, string publicationCode, int direction)
    {
        var categoryInfo = await biblePublicationService.GetPublicationCategoryInfoAsync(languageCode, publicationCode);
        if (categoryInfo is not { } info || string.IsNullOrWhiteSpace(info.CategoryCode) ||
            string.Equals(info.CategoryCode, "Bible", StringComparison.OrdinalIgnoreCase) || info.IsMusic)
        {
            return null;
        }

        var orderedPubCodes = await biblePublicationService.GetPublicationCodesInCategoryOrderAsync(languageCode, info.CategoryCode);
        var pubIndex = orderedPubCodes.FindIndex(c => string.Equals(c, publicationCode, StringComparison.OrdinalIgnoreCase));
        if (pubIndex < 0)
        {
            return null;
        }

        var candidates = new List<string>();
        for (var i = 1; i <= orderedPubCodes.Count; i++)
        {
            var candidateIndex = (pubIndex + direction * i + orderedPubCodes.Count * i) % orderedPubCodes.Count;
            var candidatePubCode = orderedPubCodes[candidateIndex];
            var candidateInfo = await biblePublicationService.GetPublicationCategoryInfoAsync(languageCode, candidatePubCode);
            if (candidateInfo is { IsMusic: true })
            {
                continue;
            }

            candidates.Add(candidatePubCode);
        }

        return candidates.Count > 0 ? candidates : null;
    }
}
