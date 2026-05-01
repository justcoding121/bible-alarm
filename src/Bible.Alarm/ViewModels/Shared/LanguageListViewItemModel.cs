#nullable enable
using System;
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

/// <param name="language">Language entity (Code and Direction come from here).</param>
/// <param name="displayName">Localized name for display (e.g. from LanguageNamesByLanguage for "E").</param>
public sealed class LanguageListViewItemModel(Language language, string displayName)
    : ObservableObject, IComparable, IComparable<LanguageListViewItemModel>, IEquatable<LanguageListViewItemModel>
{
    public string Name { get; set; } = displayName;
    public string Code { get; set; } = language.LanguageCode;
    public string Direction { get; set; } = language.Direction;

    private bool isSelected;
    private bool isNavigating;
    private double downloadProgress = -1.0; // -1 means "not set", >= 0 means "fetch in progress or completed"

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
    /// Ad-hoc fetch progress for this language selection (0.0 to 1.0).
    /// Used to show per-row progress percent alongside the spinner.
    /// </summary>
    public double DownloadProgress
    {
        get => downloadProgress;
        set
        {
            var clamped = value;
            // Allow -1.0 as sentinel value meaning "not set", otherwise clamp to 0.0-1.0
            if (clamped >= 0.0 && clamped > 1.0) clamped = 1.0;
            if (clamped < -1.0) clamped = -1.0;

            if (SetProperty(ref downloadProgress, clamped))
            {
                OnPropertyChanged(nameof(DownloadProgressText));
            }
        }
    }

    public string DownloadProgressText
    {
        get
        {
            // Return empty string if progress is not set (< 0), otherwise return percentage
            if (downloadProgress < 0.0)
            {
                return string.Empty;
            }
            var percent = (int)Math.Round(downloadProgress * 100.0, MidpointRounding.AwayFromZero);
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            return $"{percent}%";
        }
    }

    public int CompareTo(LanguageListViewItemModel? other) =>
        other is null ? 1 : string.Compare(Name, other.Name, StringComparison.Ordinal);

    public int CompareTo(object? obj) => CompareTo(obj as LanguageListViewItemModel);

    public bool Equals(LanguageListViewItemModel? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as LanguageListViewItemModel);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

    public static bool operator ==(LanguageListViewItemModel? left, LanguageListViewItemModel? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(LanguageListViewItemModel? left, LanguageListViewItemModel? right) => !(left == right);

    public static bool operator <(LanguageListViewItemModel? left, LanguageListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(LanguageListViewItemModel? left, LanguageListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(LanguageListViewItemModel? left, LanguageListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(LanguageListViewItemModel? left, LanguageListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
