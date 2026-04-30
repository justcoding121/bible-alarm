#nullable enable
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class NumberOfTracksListViewItemModel : ObservableObject, IComparable, IComparable<NumberOfTracksListViewItemModel>, IEquatable<NumberOfTracksListViewItemModel>
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

    public int CompareTo(NumberOfTracksListViewItemModel? other) =>
        other is null ? 1 : Value.CompareTo(other.Value);

    public int CompareTo(object? obj) => CompareTo(obj as NumberOfTracksListViewItemModel);

    public bool Equals(NumberOfTracksListViewItemModel? other) =>
        other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as NumberOfTracksListViewItemModel);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(NumberOfTracksListViewItemModel? left, NumberOfTracksListViewItemModel? right) =>
        ReferenceEquals(left, right) || left is not null && left.Equals(right);

    public static bool operator !=(NumberOfTracksListViewItemModel? left, NumberOfTracksListViewItemModel? right) => !(left == right);

    public static bool operator <(NumberOfTracksListViewItemModel? left, NumberOfTracksListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(NumberOfTracksListViewItemModel? left, NumberOfTracksListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(NumberOfTracksListViewItemModel? left, NumberOfTracksListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(NumberOfTracksListViewItemModel? left, NumberOfTracksListViewItemModel? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;
}
