using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public class LanguageListViewItemModel(Language language) : ObservableObject, IComparable
{
    public string Name { get; set; } = language.Name;
    public string Code { get; set; } = language.Code;

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public int CompareTo(object obj)
    {
        return string.Compare(Name, (obj as LanguageListViewItemModel)?.Name, StringComparison.Ordinal);
    }
}
