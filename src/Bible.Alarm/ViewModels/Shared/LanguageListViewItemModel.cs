using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class LanguageListViewItemModel(Language language) : ObservableObject, IComparable
{
    public string Name { get; set; } = language.Name;
    public string Code { get; set; } = language.LanguageCode;
    public string Direction { get; set; } = language.Direction;

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
    /// Ad-hoc fetch progress for this language selection (0.0 to 1.0).
    /// Used to show per-row progress percent alongside the spinner.
    /// </summary>
    public double DownloadProgress
    {
        get => downloadProgress;
        set
        {
            var clamped = value;
            if (clamped < 0.0) clamped = 0.0;
            if (clamped > 1.0) clamped = 1.0;

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
            var percent = (int)Math.Round(downloadProgress * 100.0, MidpointRounding.AwayFromZero);
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            return $"{percent}%";
        }
    }

    public int CompareTo(object obj) => string.Compare(Name, (obj as LanguageListViewItemModel)?.Name, StringComparison.Ordinal);
}
