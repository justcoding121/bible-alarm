#nullable enable

using System;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed partial class BiblePublicationSectionListViewItemModel(BiblePublicationSection section)
    : ObservableObject, IComparable, IComparable<BiblePublicationSectionListViewItemModel>, IEquatable<BiblePublicationSectionListViewItemModel>
{
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
    /// Download/fetch progress for this section selection operation (0.0 to 1.0).
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

    /// <summary>
    /// Exposes the underlying section for comparison.
    /// </summary>
    public BiblePublicationSection Section => section;

    public string SectionCode => section.SectionCode;

    /// <summary>
    /// Gets the section name with HTML entities decoded (e.g., &#160; → space) and non-breaking spaces replaced with regular spaces.
    /// </summary>
    public string Name => MediaTrackTitleHelper.DecodeHtmlTitle(section.Name);

    public int CompareTo(BiblePublicationSectionListViewItemModel? other)
    {
        if (other is null)
        {
            return 1;
        }

        return SectionCodeHelper.SectionCodeComparer.Compare(section.SectionCode, other.Section.SectionCode);
    }

    public int CompareTo(object? obj) => CompareTo(obj as BiblePublicationSectionListViewItemModel);

    public bool Equals(BiblePublicationSectionListViewItemModel? other) =>
        other is not null && SectionCodeHelper.SectionCodeComparer.Compare(SectionCode, other.SectionCode) == 0;

    public override bool Equals(object? obj) => Equals(obj as BiblePublicationSectionListViewItemModel);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(SectionCode);

    public static bool operator ==(BiblePublicationSectionListViewItemModel? left, BiblePublicationSectionListViewItemModel? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(BiblePublicationSectionListViewItemModel? left, BiblePublicationSectionListViewItemModel? right) => !(left == right);

    public static bool operator <(BiblePublicationSectionListViewItemModel? left, BiblePublicationSectionListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(BiblePublicationSectionListViewItemModel? left, BiblePublicationSectionListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(BiblePublicationSectionListViewItemModel? left, BiblePublicationSectionListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(BiblePublicationSectionListViewItemModel? left, BiblePublicationSectionListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}

