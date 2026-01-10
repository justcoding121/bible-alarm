#nullable enable
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Bible.TrackSelectionViewModelHelpers;

/// <summary>
/// Handles property management for TrackSelectionViewModel.
/// </summary>
public sealed class TrackSelectionPropertyManager : ObservableObject
{
    private bool isBusy = true;
    private ObservableCollection<BibleTrackListViewItemModel>? tracks;
    private BibleTrackListViewItemModel? selectedTrack;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public ObservableCollection<BibleTrackListViewItemModel> Tracks
    {
        get => tracks ??= [];
        set => SetProperty(ref tracks, value);
    }

    public BibleTrackListViewItemModel? SelectedTrack
    {
        get => selectedTrack;
        set => SetProperty(ref selectedTrack, value);
    }
}

