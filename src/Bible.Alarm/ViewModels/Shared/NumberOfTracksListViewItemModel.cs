#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class NumberOfTracksListViewItemModel : ObservableObject, IComparable
{
    private string unitSingular;
    private string unitPlural;

    public NumberOfTracksListViewItemModel(int number, string unitSingular, string unitPlural)
    {
        Value = number;
        this.unitSingular = unitSingular;
        this.unitPlural = unitPlural;
    }

    public string Text => $"{Value} {(Value == 1 ? unitSingular : unitPlural)}";
    public int Value { get; set; }

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

    /// <summary>
    /// Updates the unit labels and notifies Text property changed.
    /// </summary>
    public void UpdateUnitLabels(string newUnitSingular, string newUnitPlural)
    {
        unitSingular = newUnitSingular;
        unitPlural = newUnitPlural;
        OnPropertyChanged(nameof(Text));
    }

    public int CompareTo(object? obj) => obj is not NumberOfTracksListViewItemModel other ? 1 : Value.CompareTo(other.Value);
}
