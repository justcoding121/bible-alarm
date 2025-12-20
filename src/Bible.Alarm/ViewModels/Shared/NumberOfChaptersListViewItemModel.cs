using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class NumberOfChaptersListViewItemModel(int number) : ObservableObject, IComparable
{
    public string Text => $"{Value} {(Value == 1 ? "chapter" : "chapters")}";
    public int Value { get; set; } = number;

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public int CompareTo(object obj) => obj is not NumberOfChaptersListViewItemModel other ? 1 : Value.CompareTo(other.Value);
}
