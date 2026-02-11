#nullable enable

using System.Collections.ObjectModel;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer.ListPopulation;

/// <summary>
/// Populates the number of tracks list view for the schedule editor.
/// Handles drama episode count lookup and builds the observable list.
/// </summary>
public sealed class NumberOfTracksListPopulator
{
    private readonly ILogger logger;
    private readonly IBiblePublicationService? biblePublicationService;

    public NumberOfTracksListPopulator(ILogger logger, IBiblePublicationService? biblePublicationService)
    {
        this.logger = logger;
        this.biblePublicationService = biblePublicationService;
    }

    public async Task<NumberOfTracksListPopulatorResult> PopulateAsync(
        ScheduleStateItem? currentSchedule,
        int? preservedSelection,
        int? currentNumberOfTracks)
    {
        const int defaultTracks = 1;
        var numberOfTracksFromSchedule = currentSchedule?.NumberOfTracksToPlay ?? defaultTracks;
        if (numberOfTracksFromSchedule <= 0)
        {
            numberOfTracksFromSchedule = defaultTracks;
        }
        var numberOfTracks = preservedSelection ?? currentNumberOfTracks ?? numberOfTracksFromSchedule;

        var (unitSingularLower, unitPluralLower) = TracksUnitTextProvider.GetUnitTextLowerCase(currentSchedule?.BiblePublicationCategoryName);

        const int maxTracksCap = 21;
        var maxTracks = await GetMaxTracksAsync(currentSchedule, maxTracksCap);

        var trackVMs = new ObservableCollection<NumberOfTracksListViewItemModel>();
        NumberOfTracksListViewItemModel? selectedItem = null;

        for (var i = 1; i <= maxTracks; i++)
        {
            var tracksVm = new NumberOfTracksListViewItemModel(i, unitSingularLower, unitPluralLower);

            var shouldSelect = preservedSelection.HasValue
                ? preservedSelection.Value == i
                : numberOfTracks == i;

            if (shouldSelect)
            {
                tracksVm.IsSelected = true;
                selectedItem = tracksVm;
            }

            trackVMs.Add(tracksVm);
        }

        return new NumberOfTracksListPopulatorResult(trackVMs, selectedItem);
    }

    private async Task<int> GetMaxTracksAsync(ScheduleStateItem? currentSchedule, int maxTracksCap)
    {
        if (TracksUnitTextProvider.GetTracksUnit(currentSchedule?.BiblePublicationCategoryName) != TracksUnitTextProvider.TracksUnit.Episode ||
            currentSchedule == null)
        {
            return maxTracksCap;
        }

        try
        {
            if (biblePublicationService == null ||
                string.IsNullOrEmpty(currentSchedule.BiblePublicationLanguageCode) ||
                string.IsNullOrEmpty(currentSchedule.BiblePublicationCode))
            {
                return maxTracksCap;
            }

            var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(
                currentSchedule.BiblePublicationLanguageCode,
                currentSchedule.BiblePublicationCode);

            if (publication?.Tracks == null || publication.Tracks.Count == 0)
            {
                return maxTracksCap;
            }

            var maxTracks = Math.Min(publication.Tracks.Count, maxTracksCap);
            logger.Debug("PopulateNumberOfTracksListView: Non-sectioned publication has {TrackCount} episodes, setting max to {MaxTracks}",
                publication.Tracks.Count, maxTracks);
            return maxTracks;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "PopulateNumberOfTracksListView: Failed to get track count for non-sectioned publication, using default max of 21");
            return maxTracksCap;
        }
    }
}
