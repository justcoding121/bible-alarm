#nullable enable
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class CategoryListViewItemModel : ObservableObject, IComparable
{
    public CategoryListViewItemModel(Category category, string? displayName = null)
    {
        Id = category.Id;
        CategoryCode = category.CategoryCode;
        Name = displayName ?? category.CategoryCode;
    }

    public int Id { get; set; }
    /// <summary>Category code for filtering and state (e.g. Bible, Music).</summary>
    public string CategoryCode { get; set; } = string.Empty;
    /// <summary>Display name for UI (localized when cache warmed for "E").</summary>
    public string Name { get; set; } = string.Empty;

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
    /// Download progress for this category selection operation (0.0 to 1.0).
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
            var percent = (int)Math.Round(downloadProgress * 100);
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            return $"{percent}%";
        }
    }

    public int CompareTo(object? obj) => string.Compare(Name, (obj as CategoryListViewItemModel)?.Name, StringComparison.Ordinal);
}
