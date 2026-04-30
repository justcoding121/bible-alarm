#nullable enable
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;

/// <summary>
/// Handles property management for MusicTrackSelectionViewModel.
/// </summary>
public sealed partial class MusicTrackPropertyManager : ObservableObject
{
    private bool isBusy = true;
    private ObservableCollection<MusicTrackListViewItemModel> tracks = [];
    private MusicTrackListViewItemModel? selectedTrack;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public ObservableCollection<MusicTrackListViewItemModel> Tracks
    {
        get => tracks;
        set => SetProperty(ref tracks, value);
    }

    public MusicTrackListViewItemModel? SelectedTrack
    {
        get => selectedTrack;
        set
        {
            // Clear previous selection
            if (selectedTrack != null)
            {
                selectedTrack.IsSelected = false;
                selectedTrack.Repeat = false;
            }

            // Set new selection
            if (SetProperty(ref selectedTrack, value) && value != null)
            {
                value.IsSelected = true;
            }
        }
    }

    public void SetSelectedTrack(MusicTrackListViewItemModel? track)
    {
        selectedTrack = track;
    }
}
