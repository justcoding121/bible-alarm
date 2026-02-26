#nullable enable
using System.Globalization;
using System.Net;
using Bible.Alarm.Shared.Helpers;
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
    private bool isNavigating;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsNavigating
    {
        get => isNavigating;
        set => SetProperty(ref isNavigating, value);
    }

    /// <summary>
    /// Track code for schedule persistence and lookup (numeric or non-numeric, e.g. "1", "jwb-201708").
    /// </summary>
    public string TrackCode => TrackCodeHelper.GetFromTrack(track);

    /// <summary>
    /// Gets the track title with HTML entities decoded (e.g., &#160; → space) and non-breaking spaces replaced with regular spaces.
    /// </summary>
    public string Title => WebUtility.HtmlDecode(track.Title).Replace('\u00A0', ' ');
    // URLs are now computed on-demand, not stored
    public string Url => string.Empty;

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
            return Bible.Alarm.Shared.Helpers.CodeComparisonHelper.Compare(TrackCode, other.TrackCode);
        return 0;
    }
}
