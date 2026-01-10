#nullable enable
using Bible.Alarm.Shared.Models.Media.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicTrackListViewItemModel : ObservableObject, IComparable
{
    private readonly MusicTrack track;
    private readonly bool isMelody;

    public MusicTrackListViewItemModel(MusicTrack track, bool isMelody)
    {
        this.track = track;
        this.isMelody = isMelody;

        ToggleRepeatCommand = new RelayCommand(() => Repeat = !Repeat);
    }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    // LookUpPath is no longer stored in the database - it's computed at runtime by TrackMetadata
    public int Number => track.Number;

    public string Title => isMelody ? $"Melody Number(s) {track.Title}" : track.Title;
    public string Url => track.Source?.Url ?? string.Empty;

    private bool repeat;

    public bool Repeat
    {
        get => repeat;
        set => SetProperty(ref repeat, value);
    }

    public IRelayCommand ToggleRepeatCommand { get; }

    public int CompareTo(object? obj)
    {
        if (obj is MusicTrackListViewItemModel other)
        {
            return Number.CompareTo(other.Number);
        }

        return 0;
    }
}
