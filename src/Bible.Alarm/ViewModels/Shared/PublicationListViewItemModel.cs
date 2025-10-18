using Bible.Alarm.Models;
using Mvvmicro;

namespace Bible.Alarm.ViewModels;

public class PublicationListViewItemModel(Publication publication) : ViewModel, IComparable
{
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.Set(ref _isSelected, value);
    }

    public string Name => publication.Name;
    public string Code => publication.Code;

    public int CompareTo(object obj)
    {
        return Name.CompareTo((obj as PublicationListViewItemModel).Name);
    }
}