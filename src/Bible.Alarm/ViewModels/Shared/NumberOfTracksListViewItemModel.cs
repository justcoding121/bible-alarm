using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class NumberOfTracksListViewItemModel : ObservableObject, IComparable
{
    private string unitSingular;
    private string unitPlural;

    public NumberOfTracksListViewItemModel(int number, bool hasSectionStructure = true)
    {
        Value = number;
        // Use "chapter"/"chapters" for sectioned publications (Bible),
        // "episode"/"episodes" for non-sectioned publications (dramas)
        unitSingular = hasSectionStructure ? "chapter" : "episode";
        unitPlural = hasSectionStructure ? "chapters" : "episodes";
    }

    public string Text => $"{Value} {(Value == 1 ? unitSingular : unitPlural)}";
    public int Value { get; set; }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    /// <summary>
    /// Updates the unit labels (chapter/episode) and notifies Text property changed.
    /// </summary>
    public void UpdateUnitLabels(bool hasSectionStructure)
    {
        unitSingular = hasSectionStructure ? "chapter" : "episode";
        unitPlural = hasSectionStructure ? "chapters" : "episodes";
        OnPropertyChanged(nameof(Text));
    }

    public int CompareTo(object? obj) => obj is not NumberOfTracksListViewItemModel other ? 1 : Value.CompareTo(other.Value);
}
