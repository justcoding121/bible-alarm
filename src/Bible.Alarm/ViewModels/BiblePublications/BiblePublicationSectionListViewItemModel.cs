#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;
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

    /// <summary>
    /// Gets the section name with HTML entities decoded (e.g., &#160; → space) and non-breaking spaces replaced with regular spaces.
    /// </summary>
    public string Name => System.Net.WebUtility.HtmlDecode(section.Name).Replace('\u00A0', ' ');

    /// <summary>
    /// Gets the section number for sorting/comparison.
    /// Tries to parse SectionCode as int.
    /// For non-numeric codes like "iam-1", extracts the number part.
    /// Returns 0 if SectionCode cannot be parsed or number cannot be extracted.
    /// </summary>
    public int Number
    {
        get
        {
            // Try to parse SectionCode as int (for numeric codes like "1", "2")
            if (int.TryParse(section.SectionCode, out var num))
            {
                return num;
            }

            // For non-numeric codes like "iam-1", extract the number part
            // e.g., "iam-1" -> 1, "iam-2" -> 2
            var parts = section.SectionCode.Split('-');
            if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out var extractedNumber))
            {
                return extractedNumber;
            }

            return 0;
        }
    }

    public int CompareTo(object? obj)
    {
        if (obj is not BiblePublicationSectionListViewItemModel other)
        {
            return 1;
        }

        // Natural sort: if SectionCode is numeric, sort as int; otherwise sort as string
        var thisIsNumeric = int.TryParse(section.SectionCode, out var thisNum);
        var otherIsNumeric = int.TryParse(other.Section.SectionCode, out var otherNum);

        if (thisIsNumeric && otherIsNumeric)
        {
            // Both are numeric - compare as integers
            return thisNum.CompareTo(otherNum);
        }

        if (thisIsNumeric && !otherIsNumeric)
        {
            // This is numeric, other is not - numeric comes first
            return -1;
        }

        if (!thisIsNumeric && otherIsNumeric)
        {
            // This is not numeric, other is - numeric comes first
            return 1;
        }

        // Both are non-numeric - compare as strings
        return string.Compare(section.SectionCode, other.Section.SectionCode, StringComparison.OrdinalIgnoreCase);
    }
}

