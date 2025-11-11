using CommunityToolkit.Mvvm.ComponentModel;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.ViewModels.Shared;

public class PublicationListViewItemModel(Publication publication) : ObservableObject, IComparable
{
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Name => publication.Name;
    public string Code => publication.Code;

    public int CompareTo(object obj)
    {
        return string.Compare(Name, (obj as PublicationListViewItemModel)?.Name, StringComparison.Ordinal);
    }
}