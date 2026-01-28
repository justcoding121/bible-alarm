#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Services.Media.Interfaces;
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
        int sectionNumber,
        BiblePublicationSchedule? current,
        ObservableCollection<BiblePublicationTrackListViewItemModel> tracks,
        Action<BiblePublicationTrackListViewItemModel?> setSelectedTrack)
    {
        Log.Debug("TrackSelectionDataProvider.PopulateTracks: languageCode={LanguageCode}, publicationCode={PublicationCode}, sectionNumber={SectionNumber}",
            languageCode, publicationCode, sectionNumber);

        // Do ALL processing on background thread to avoid blocking spinner animation
        var (trackViewModelList, selectedTrack) = await Task.Run(async () =>
        {
            IEnumerable<BiblePublicationTrack> tracksFromDb;

            // If sectionNumber is 0, this is a non-sectioned publication (drama/video)
            // Load tracks directly from the publication
            if (sectionNumber <= 0 && biblePublicationService != null)
            {
                Log.Debug("TrackSelectionDataProvider.PopulateTracks: Loading non-sectioned tracks for publication={PublicationCode}", publicationCode);
                var publication = await biblePublicationService.GetByLanguageAndCodeWithTracksAsync(languageCode, publicationCode);
                tracksFromDb = publication?.Tracks?.OrderBy(t => t.Number) ?? Enumerable.Empty<BiblePublicationTrack>();
                Log.Debug("TrackSelectionDataProvider.PopulateTracks: Loaded {TrackCount} non-sectioned tracks", tracksFromDb.Count());
            }
            else
            {
                // Sectioned publication - use standard approach
                Log.Debug("TrackSelectionDataProvider.PopulateTracks: Loading sectioned tracks for section={SectionNumber}", sectionNumber);
                var sectionedTracks = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionNumber);
                tracksFromDb = sectionedTracks.Values;
                Log.Debug("TrackSelectionDataProvider.PopulateTracks: Loaded {TrackCount} sectioned tracks", sectionedTracks.Count);
            }

            var vms = new List<BiblePublicationTrackListViewItemModel>();
            BiblePublicationTrackListViewItemModel? selected = null;

            Log.Debug("TrackSelectionDataProvider.PopulateTracks: current TrackNumber={CurrentTrackNumber}",
                current?.TrackNumber ?? -1);

            foreach (var track in tracksFromDb)
            {
                var trackVm = new BiblePublicationTrackListViewItemModel(track);
                vms.Add(trackVm);

                if (current != null && current.TrackNumber == track.Number)
                {
                    selected = trackVm;
                    selected.IsSelected = true;
                    Log.Debug("TrackSelectionDataProvider.PopulateTracks: Matched track {TrackNumber} ({TrackTitle}) as selected",
                        track.Number, track.Title);
                }
            }

            return (vms, selected);
        });

        Log.Debug("TrackSelectionDataProvider.PopulateTracks: Created {VmCount} track VMs, selectedTrack={HasSelected} (number={SelectedNumber})",
            trackViewModelList.Count, selectedTrack != null, selectedTrack?.Number ?? -1);

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

    public void SetSelectedTrack(
        BiblePublicationSchedule? current,
        ObservableCollection<BiblePublicationTrackListViewItemModel> tracks,
        BiblePublicationTrackListViewItemModel? currentSelectedTrack,
        Action<BiblePublicationTrackListViewItemModel?> setSelectedTrack)
    {
        Log.Debug("TrackSelectionDataProvider.SetSelectedTrack: current TrackNumber={CurrentTrackNumber}, tracksCount={TracksCount}",
            current?.TrackNumber ?? -1, tracks?.Count ?? 0);

        if (current == null || tracks == null || tracks.Count == 0)
        {
            return;
        }

        if (currentSelectedTrack != null)
        {
            currentSelectedTrack.IsSelected = false;
        }

        var track = tracks.FirstOrDefault(c => c.Number == current.TrackNumber);
        if (track != null)
        {
            Log.Debug("TrackSelectionDataProvider.SetSelectedTrack: Setting selected track {TrackNumber} ({TrackTitle})",
                track.Number, track.Title);
            setSelectedTrack(track);
            track.IsSelected = true;
        }
        else
        {
            Log.Warning("TrackSelectionDataProvider.SetSelectedTrack: Could not find track {TrackNumber} in tracks collection",
                current.TrackNumber);
        }
    }
}

