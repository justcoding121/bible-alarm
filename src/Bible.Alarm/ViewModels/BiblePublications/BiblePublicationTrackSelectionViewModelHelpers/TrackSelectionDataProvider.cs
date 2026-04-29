#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Services.Media.Interfaces;
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
        Log.Debug("TrackSelectionDataProvider.PopulateTracks: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionCode={SectionCode}",
            languageCode, publicationCode, sectionCode);

        IEnumerable<BiblePublicationTrack> tracksFromDb;

        // If sectionCode is null/empty, this is a non-sectioned publication (drama/video)
        // Load tracks directly from the publication
        if (string.IsNullOrWhiteSpace(sectionCode) && biblePublicationService != null)
        {
            Log.Debug("TrackSelectionDataProvider.PopulateTracks: Loading non-sectioned tracks for publication={PublicationCode}", publicationCode);
            var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
            tracksFromDb = publication?.Tracks?.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))) ?? Enumerable.Empty<BiblePublicationTrack>();
            Log.Debug("TrackSelectionDataProvider.PopulateTracks: Loaded {TrackCount} non-sectioned tracks", tracksFromDb.Count());
        }
        else
        {
            // Sectioned publication - use standard approach
            Log.Debug("TrackSelectionDataProvider.PopulateTracks: Loading sectioned tracks for section={SectionCode}", sectionCode);
            var sectionedTracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionCode);
            tracksFromDb = sectionedTracks.Values;
            Log.Debug("TrackSelectionDataProvider.PopulateTracks: Loaded {TrackCount} sectioned tracks", sectionedTracks.Count);
        }

        var trackViewModelList = new List<BiblePublicationTrackListViewItemModel>();
        BiblePublicationTrackListViewItemModel? selectedTrack = null;

        Log.Debug("TrackSelectionDataProvider.PopulateTracks: current TrackCode={CurrentTrackCode}",
            current?.TrackCode ?? "(null)");

        foreach (var track in tracksFromDb)
        {
            var trackVm = new BiblePublicationTrackListViewItemModel(track);
            trackViewModelList.Add(trackVm);

            if (current != null && !string.IsNullOrWhiteSpace(current.TrackCode))
            {
                var trackCodeFromTrack = TrackCodeHelper.GetFromTrack(track);
                if (current.TrackCode == trackCodeFromTrack)
                {
                    selectedTrack = trackVm;
                    selectedTrack.IsSelected = true;
                    Log.Debug("TrackSelectionDataProvider.PopulateTracks: Matched track {TrackCode} ({TrackTitle}) as selected",
                        trackCodeFromTrack, track.Title);
                }
            }
        }

        Log.Debug("TrackSelectionDataProvider.PopulateTracks: Created {VmCount} track VMs, selectedTrack={HasSelected} (trackCode={SelectedTrackCode})",
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
        Log.Debug("TrackSelectionDataProvider.SetSelectedTrack: current TrackCode={CurrentTrackCode}, tracksCount={TracksCount}",
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
            c.TrackCode == current.TrackCode);
        if (track != null)
        {
            Log.Debug("TrackSelectionDataProvider.SetSelectedTrack: Setting selected track {TrackCode} ({TrackTitle})",
                track.TrackCode, track.Title);
            setSelectedTrack(track);
            track.IsSelected = true;
        }
        else
        {
            Log.Warning("TrackSelectionDataProvider.SetSelectedTrack: Could not find track {TrackCode} in tracks collection",
                current.TrackCode);
        }
    }
}

