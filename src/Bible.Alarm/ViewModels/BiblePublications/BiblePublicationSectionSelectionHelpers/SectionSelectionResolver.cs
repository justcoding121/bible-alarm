#nullable enable

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

internal static class SectionSelectionResolver
{
    internal static BiblePublicationSectionListViewItemModel? FindSectionToSelect(
        string? sectionCode,
        IEnumerable<BiblePublicationSectionListViewItemModel> sections)
    {
        if (string.IsNullOrEmpty(sectionCode))
        {
            return null;
        }

        // Treat section code strictly as a string (case-insensitive).
        return sections.FirstOrDefault(b =>
            string.Equals(b.Section.SectionCode, sectionCode, StringComparison.OrdinalIgnoreCase));
    }
}

