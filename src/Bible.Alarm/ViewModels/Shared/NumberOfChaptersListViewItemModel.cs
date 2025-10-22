using Bible.Alarm.Common.Mvvm;

namespace Bible.Alarm.ViewModels.Shared;

public class NumberOfChaptersListViewItemModel(int number) : ViewModel, IComparable
{
    public string Text => $"{Value} {(Value == 1 ? "chapter" : "chapters")}";
    public int Value { get; set; } = number;

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.Set(ref _isSelected, value);
    }

    public int CompareTo(object obj)
    {
        return Value.CompareTo((obj as NumberOfChaptersListViewItemModel).Value);
    }
}