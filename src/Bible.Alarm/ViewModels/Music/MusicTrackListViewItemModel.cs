#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.ViewModels.Music;

public sealed class MusicTrackListViewItemModel : ObservableObject, IComparable
{
    private readonly Shared.Models.Media.Music.MusicTrack track;
    private readonly bool isMelody;

    public MusicTrackListViewItemModel(Shared.Models.Media.Music.MusicTrack track, bool isMelody)
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

    public string LookUpPath => track.Source?.LookUpPath ?? string.Empty;
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
