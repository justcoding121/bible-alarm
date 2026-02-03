#nullable enable
using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class CategoryListViewItemModel(Category category) : ObservableObject, IComparable
{
    public int Id { get; set; } = category.Id;
    public string Name { get; set; } = category.CategoryName;

    private bool isSelected;
    private bool isNavigating;
    private double downloadProgress;

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
            // Clamp value between 0.0 and 1.0
            var clamped = Math.Max(0.0, Math.Min(1.0, value));

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
            var percent = (int)Math.Round(downloadProgress * 100);
            return $"{percent}%";
        }
    }

    public int CompareTo(object? obj) => string.Compare(Name, (obj as CategoryListViewItemModel)?.Name, StringComparison.Ordinal);
}
