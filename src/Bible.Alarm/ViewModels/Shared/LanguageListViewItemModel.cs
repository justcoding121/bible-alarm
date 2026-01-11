using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class LanguageListViewItemModel(Language language) : ObservableObject, IComparable
{
    public string Name { get; set; } = language.Name;
    public string Code { get; set; } = language.Code;
    public string Direction { get; set; } = language.Direction;

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public int CompareTo(object obj) => string.Compare(Name, (obj as LanguageListViewItemModel)?.Name, StringComparison.Ordinal);
}
