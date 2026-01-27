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

    public int CompareTo(object obj) => string.Compare(Name, (obj as LanguageListViewItemModel)?.Name, StringComparison.Ordinal);
}
