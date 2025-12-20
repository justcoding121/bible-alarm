using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public class PublicationListViewItemModel(Publication publication) : ObservableObject, IComparable
{
    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string Name => publication.Name;
    public string Code => publication.Code;

    public int CompareTo(object obj)
    {
        return string.Compare(Name, (obj as PublicationListViewItemModel)?.Name, StringComparison.Ordinal);
    }
}
