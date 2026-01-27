using Bible.Alarm.Shared.Models.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.Shared;

public sealed class CategoryListViewItemModel(Category category) : ObservableObject, IComparable
{
    public int Id { get; set; } = category.Id;
    public string Name { get; set; } = category.CategoryName;

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

    public int CompareTo(object obj) => string.Compare(Name, (obj as CategoryListViewItemModel)?.Name, StringComparison.Ordinal);
}
