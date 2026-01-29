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

        // Compare SectionCode (string) with Number (int) by converting Number to string or parsing SectionCode
        // Handle both numeric codes (e.g., "1") and non-numeric codes (e.g., "iam-1")
        return sections.FirstOrDefault(b =>
            b.Number.ToString() == sectionCode ||
            (int.TryParse(sectionCode, out var num) && num == b.Number) ||
            (sectionCode.Contains('-') &&
             sectionCode.Split('-').Length > 1 &&
             int.TryParse(sectionCode.Split('-')[^1], out var extractedNum) &&
             extractedNum == b.Number));
    }
}

