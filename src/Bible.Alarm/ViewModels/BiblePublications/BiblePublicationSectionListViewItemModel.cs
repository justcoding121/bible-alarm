#nullable enable

using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class BiblePublicationSectionListViewItemModel(BiblePublicationSection section) : ObservableObject, IComparable
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

    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationSectionListViewItemModel other)
        {
            return 1;
        }

        // Natural sort via shared comparer:
        // numeric section codes sort numerically, otherwise as strings.
        return SectionCodeHelper.SectionCodeComparer.Compare(section.SectionCode, other.Section.SectionCode);
    }
}

