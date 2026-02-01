#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed class BiblePublicationSectionListViewItemModel(BiblePublicationSection section) : ObservableObject, IComparable
{
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
    /// Exposes the underlying section for comparison.
    /// </summary>
    public BiblePublicationSection Section => section;

    public string SectionCode => section.SectionCode;

    /// <summary>
    /// Gets the section name with HTML entities decoded (e.g., &#160; → space) and non-breaking spaces replaced with regular spaces.
    /// </summary>
    public string Name => System.Net.WebUtility.HtmlDecode(section.Name).Replace('\u00A0', ' ');

    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationSectionListViewItemModel other)
        {
            return 1;
        }

        // Natural sort via shared comparer:
        // numeric section codes sort numerically, otherwise as strings.
        return SectionCodeHelper.SectionCodeComparer.Compare(section.SectionCode, other.Section.SectionCode);
    }
}

