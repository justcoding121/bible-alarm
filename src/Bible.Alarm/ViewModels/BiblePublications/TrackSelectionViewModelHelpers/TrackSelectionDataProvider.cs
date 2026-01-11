#nullable enable
using System.Collections.ObjectModel;
using Bible.Alarm.Services.Media.Interfaces;

namespace Bible.Alarm.ViewModels.BiblePublications.TrackSelectionViewModelHelpers;

/// <summary>
/// Handles data population for TrackSelectionViewModel.
/// </summary>
public sealed class TrackSelectionDataProvider(IMediaService mediaService)
{
    public async Task PopulateTracks(
        string languageCode,
        string publicationCode,
        int sectionNumber,
        BiblePublicationSchedule? current,
        ObservableCollection<BiblePublicationTrackListViewItemModel> tracks,
        Action<BiblePublicationTrackListViewItemModel?> setSelectedTrack)
    {
        // Do ALL processing on background thread to avoid blocking spinner animation
        var (trackViewModelList, selectedTrack) = await Task.Run(async () =>
        {
            var tracksFromDb = await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, sectionNumber);
            var vms = new List<BiblePublicationTrackListViewItemModel>();
            BiblePublicationTrackListViewItemModel? selected = null;

            foreach (var track in tracksFromDb.Values)
            {
                var trackVm = new BiblePublicationTrackListViewItemModel(track);
                vms.Add(trackVm);

                if (current != null && current.TrackNumber == track.Number)
                {
                    selected = trackVm;
                    selected.IsSelected = true;
                }
            }

            return (vms, selected);
        });

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
            setSelectedTrack(track);
            track.IsSelected = true;
        }
    }
}

