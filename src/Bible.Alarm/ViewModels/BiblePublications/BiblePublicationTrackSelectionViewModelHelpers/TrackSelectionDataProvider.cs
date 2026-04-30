#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationTrackSelectionViewModelHelpers;

/// <summary>
/// Handles data population for BiblePublicationTrackSelectionViewModel.
/// </summary>
public sealed class TrackSelectionDataProvider(IMediaService mediaService, IBiblePublicationService? biblePublicationService = null)
{
    public async Task PopulateTracks(
        string languageCode,
        string publicationCode,
        string? sectionCode,
        BiblePublicationSchedule? current,
        ObservableCollection<BiblePublicationTrackListViewItemModel> tracks,
        Action<BiblePublicationTrackListViewItemModel?> setSelectedTrack)
    {
        Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksLanguagePublicationSection,
            languageCode, publicationCode, sectionCode);

        IEnumerable<BiblePublicationTrack> tracksFromDb;

        // If sectionCode is null/empty, this is a non-sectioned publication (drama/video)
        // Load tracks directly from the publication
        if (string.IsNullOrWhiteSpace(sectionCode) && biblePublicationService != null)
        {
            Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksLoadingNonSectionedForPublication, publicationCode);
            var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
            tracksFromDb = publication?.Tracks?.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))) ?? Enumerable.Empty<BiblePublicationTrack>();
            Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksLoadedNonSectionedTrackCount, tracksFromDb.Count());
        }
        else
        {
            // Sectioned publication - use standard approach
            Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksLoadingSectionedForSection, sectionCode);
            var sectionedTracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionCode);
            tracksFromDb = sectionedTracks.Values;
            Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksLoadedSectionedTrackCount, sectionedTracks.Count);
        }

        var trackViewModelList = new List<BiblePublicationTrackListViewItemModel>();
        BiblePublicationTrackListViewItemModel? selectedTrack = null;

        Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksCurrentTrackCode,
            current?.TrackCode ?? "(null)");

        foreach (var track in tracksFromDb)
        {
            var trackVm = new BiblePublicationTrackListViewItemModel(track);
            trackViewModelList.Add(trackVm);

            if (current != null && !string.IsNullOrWhiteSpace(current.TrackCode))
            {
                var trackCodeFromTrack = TrackCodeHelper.GetFromTrack(track);
                if (CodeComparisonHelper.Equals(current.TrackCode, trackCodeFromTrack))
                {
                    selectedTrack = trackVm;
                    selectedTrack.IsSelected = true;
                    Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksMatchedTrackAsSelected,
                        trackCodeFromTrack, track.Title);
                }
            }
        }

        Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.PopulateTracksCreatedVmSummary,
            trackViewModelList.Count, selectedTrack != null, selectedTrack?.TrackCode ?? "(none)");

        // Minimal UI thread work - just swap the collection contents
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            tracks.Clear();
            foreach (var track in trackViewModelList)
            {
                tracks.Add(track);
            }

            if (selectedTrack is not null)
            {
                setSelectedTrack(selectedTrack);
            }
        });
    }

    public static void SetSelectedTrack(
        BiblePublicationSchedule? current,
        ObservableCollection<BiblePublicationTrackListViewItemModel> tracks,
        BiblePublicationTrackListViewItemModel? currentSelectedTrack,
        Action<BiblePublicationTrackListViewItemModel?> setSelectedTrack)
    {
        Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.SetSelectedTrackCurrentTrackAndTracksCount,
            current?.TrackCode ?? "(none)", tracks?.Count ?? 0);

        if (current == null || tracks == null || tracks.Count == 0)
        {
            return;
        }

        if (currentSelectedTrack != null)
        {
            currentSelectedTrack.IsSelected = false;
        }

        var track = tracks.FirstOrDefault(c => !string.IsNullOrWhiteSpace(current.TrackCode) &&
            CodeComparisonHelper.Equals(c.TrackCode, current.TrackCode));
        if (track != null)
        {
            Log.Debug(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.SetSelectedTrackSettingSelected,
                track.TrackCode, track.Title);
            setSelectedTrack(track);
            track.IsSelected = true;
        }
        else
        {
            Log.Warning(AppConstants.Logging.TrackSelectionDataProviderDiagnosticsLog.SetSelectedTrackCouldNotFindInCollection,
                current.TrackCode);
        }
    }
}

