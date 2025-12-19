using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public class NumberOfChaptersListViewItemModel(int number) : ObservableObject, IComparable
{
    public string Text => $"{Value} {(Value == 1 ? "chapter" : "chapters")}";
    public int Value { get; set; } = number;

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public int CompareTo(object obj)
    {
        return obj is not NumberOfChaptersListViewItemModel other ? 1 : Value.CompareTo(other.Value);
    }
}
