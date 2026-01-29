#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
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
        if (sectionItem.Number <= 0)
        {
            logger.Warning("TrackSelectionResolver: Invalid section number {SectionNumber} for publication={PublicationCode}",
                sectionItem.Number, currentSchedule.BiblePublicationCode);
            return null;
        }

        var languageCode = currentSchedule.BiblePublicationLanguageCode ?? string.Empty;

        // Check if the selected section is the same as the current section
        var currentSectionNumber = currentSchedule.BiblePublicationSectionNumber;
        var isSameSection = currentSectionNumber.HasValue && currentSectionNumber.Value == sectionItem.Number;

        // Get tracks for the selected section using the latest language/publication from CurrentSchedule
        var tracks = await mediaService.GetBiblePublicationTracks(languageCode, currentSchedule.BiblePublicationCode!, sectionItem.Number);

        // If no tracks found, check if section exists and harvest tracks if needed
        if ((tracks == null || tracks.Count == 0) &&
            !string.IsNullOrEmpty(languageCode) &&
            !languageCode.Equals("E", StringComparison.OrdinalIgnoreCase))
        {
            logger.Information(
                "TrackSelectionResolver: No tracks found for section={SectionNumber}, publication={PublicationCode}, language={LanguageCode}. Attempting to fetch tracks...",
                sectionItem.Number,
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
                    tracks = await mediaService.GetBiblePublicationTracks(languageCode, currentSchedule.BiblePublicationCode!, sectionItem.Number);
                }
            }
        }

        if (tracks == null || tracks.Count == 0)
        {
            logger.Warning(
                "TrackSelectionResolver: No tracks found for section={SectionNumber}, publication={PublicationCode}, language={LanguageCode}",
                sectionItem.Number,
                currentSchedule.BiblePublicationCode,
                languageCode ?? "(null)");
            return null;
        }

        // If it's the same section, preserve the current track number (if valid)
        // Otherwise, use the first track
        int trackNumber;
        string? trackTitle;

        if (isSameSection && currentSchedule.BiblePublicationTrackNumber.HasValue)
        {
            var currentTrackNumber = currentSchedule.BiblePublicationTrackNumber.Value;
            // Verify the current track exists in the tracks list
            if (tracks.TryGetValue(currentTrackNumber, out var existingTrack))
            {
                trackNumber = currentTrackNumber;
                trackTitle = existingTrack.Title;
            }
            else
            {
                // Current track doesn't exist in this section, use first track
                var firstTrack = tracks.Values.First();
                trackNumber = firstTrack.Number;
                trackTitle = firstTrack.Title;
            }
        }
        else
        {
            // Different section selected, use first track
            var firstTrack = tracks.Values.First();
            trackNumber = firstTrack.Number;
            trackTitle = firstTrack.Title;
        }

        return new BiblePublicationStateItem
        {
            // IMPORTANT: Always preserve category from current schedule - category can only be changed via CategorySelectionAction
            CategoryId = currentSchedule.BiblePublicationCategoryId,
            CategoryName = currentSchedule.BiblePublicationCategoryName,
            LanguageCode = languageCode, // Can be empty for publications without language
            PublicationCode = currentSchedule.BiblePublicationCode!,
            SectionNumber = sectionItem.Number,
            TrackNumber = trackNumber,
            // Store display names from list items and current state
            LanguageName = currentSchedule.BiblePublicationLanguageName,
            LanguageDirection = currentSchedule.BiblePublicationLanguageDirection,
            PublicationName = currentSchedule.BiblePublicationName,
            SectionName = sectionItem.Name,
            TrackTitle = trackTitle
        };
    }
}

