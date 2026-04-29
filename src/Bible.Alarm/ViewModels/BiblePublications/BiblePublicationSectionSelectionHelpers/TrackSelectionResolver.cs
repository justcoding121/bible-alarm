#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

internal sealed class TrackSelectionResolver
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;

    public TrackSelectionResolver(ILogger logger, IMediaService mediaService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
    }

    internal async Task<BiblePublicationStateItem?> BuildSelectionAsync(
        BiblePublicationSectionListViewItemModel sectionItem,
        ScheduleStateItem currentSchedule,
        Func<ILanguageContentService?> getLanguageContentService)
    {
        if (string.IsNullOrWhiteSpace(sectionItem.Section.SectionCode))
        {
            logger.Warning("TrackSelectionResolver: Invalid section code for publication={PublicationCode}",
                currentSchedule.BiblePublicationCode);
            return null;
        }

        var languageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;

        // Check if the selected section is the same as the current section
        var currentSectionCode = currentSchedule.BiblePublicationSectionCode;
        var isSameSection = string.Equals(currentSectionCode, sectionItem.Section.SectionCode, StringComparison.OrdinalIgnoreCase);

        // Get tracks for the selected section using the latest language/publication from CurrentSchedule
        var tracks = await mediaService.GetBiblePublicationTracks(languageCode, currentSchedule.BiblePublicationCode!, sectionItem.Section.SectionCode);

        // If no tracks found, check if section exists and catalog tracks if needed
        if ((tracks == null || tracks.Count == 0) &&
            !string.IsNullOrEmpty(languageCode) &&
            !languageCode.Equals(AppConstants.Media.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            logger.Information(
                "TrackSelectionResolver: No tracks found for section={SectionCode}, publication={PublicationCode}, language={LanguageCode}. Attempting to fetch tracks...",
                sectionItem.Section.SectionCode,
                currentSchedule.BiblePublicationCode,
                languageCode);

            var languageContentService = getLanguageContentService();
            if (languageContentService != null)
            {
                var fetchSuccess = await languageContentService.FetchSectionTracksAsync(
                    currentSchedule.BiblePublicationCode!,
                    sectionItem.Section.SectionCode,
                    languageCode);

                if (fetchSuccess)
                {
                    tracks = await mediaService.GetBiblePublicationTracks(languageCode, currentSchedule.BiblePublicationCode!, sectionItem.Section.SectionCode);
                }
            }
        }

        if (tracks == null || tracks.Count == 0)
        {
            logger.Warning(
                "TrackSelectionResolver: No tracks found for section={SectionCode}, publication={PublicationCode}, language={LanguageCode}",
                sectionItem.Section.SectionCode,
                currentSchedule.BiblePublicationCode,
                languageCode ?? "(null)");
            return null;
        }

        // If it's the same section, preserve the current track code (if valid)
        // Otherwise, use the first track
        string trackCode;
        string? trackTitle;

        if (isSameSection && !string.IsNullOrWhiteSpace(currentSchedule.BiblePublicationTrackCode) &&
            tracks.TryGetValue(currentSchedule.BiblePublicationTrackCode, out var existingTrack))
        {
            trackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(existingTrack);
            trackTitle = existingTrack.Title;
        }
        else
        {
            using var trackEnumerator = tracks.Values.GetEnumerator();
            _ = trackEnumerator.MoveNext();
            var firstTrack = trackEnumerator.Current;
            trackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(firstTrack);
            trackTitle = firstTrack.Title;
        }

        return new BiblePublicationStateItem
        {
            // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
            CategoryId = currentSchedule.BiblePublicationCategoryId,
            CategoryName = currentSchedule.BiblePublicationCategoryName,
            LanguageCode = languageCode, // Can be empty for publications without language
            PublicationCode = currentSchedule.BiblePublicationCode!,
            SectionCode = sectionItem.Section.SectionCode,
            TrackCode = trackCode ?? string.Empty,
            // Store display names from list items and current state
            LanguageName = currentSchedule.BiblePublicationLanguageName,
            LanguageDirection = currentSchedule.BiblePublicationLanguageDirection,
            PublicationName = currentSchedule.BiblePublicationName,
            SectionName = sectionItem.Name,
            TrackTitle = trackTitle
        };
    }
}

