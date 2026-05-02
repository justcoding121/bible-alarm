#nullable enable
using System;
using System.Globalization;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Bible.Alarm.ViewModels.Music;

public sealed partial class MusicTrackListViewItemModel : ObservableObject, IComparable, IComparable<MusicTrackListViewItemModel>, IEquatable<MusicTrackListViewItemModel>
{
    private readonly MusicTrack track;

    public MusicTrackListViewItemModel(MusicTrack track)
    {
        this.track = track;

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
    public string Title => MediaTrackTitleHelper.DecodeHtmlTitle(track.Title);
    /// <summary>URL from the hydrated track when set (playback may resolve otherwise).</summary>
    public string Url => track.Url ?? string.Empty;

    private bool repeat;

    public bool Repeat
    {
        get => repeat;
        set => SetProperty(ref repeat, value);
    }

    public IRelayCommand ToggleRepeatCommand { get; }

    public int CompareTo(MusicTrackListViewItemModel? other) =>
        other is null ? 1 : CodeComparisonHelper.Compare(TrackCode, other.TrackCode);

    public int CompareTo(object? obj) => CompareTo(obj as MusicTrackListViewItemModel);

    public bool Equals(MusicTrackListViewItemModel? other) =>
        other is not null && CodeComparisonHelper.Compare(TrackCode, other.TrackCode) == 0;

    public override bool Equals(object? obj) => Equals(obj as MusicTrackListViewItemModel);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(TrackCode);

    public static bool operator ==(MusicTrackListViewItemModel? left, MusicTrackListViewItemModel? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(MusicTrackListViewItemModel? left, MusicTrackListViewItemModel? right) => !(left == right);

    public static bool operator <(MusicTrackListViewItemModel? left, MusicTrackListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(MusicTrackListViewItemModel? left, MusicTrackListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(MusicTrackListViewItemModel? left, MusicTrackListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(MusicTrackListViewItemModel? left, MusicTrackListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
