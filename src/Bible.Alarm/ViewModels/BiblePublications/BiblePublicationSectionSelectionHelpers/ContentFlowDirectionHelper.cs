#nullable enable

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

internal static class ContentFlowDirectionHelper
{
    internal static FlowDirection GetContentFlowDirection(string? direction)
    {
        return string.Equals(direction, "rtl", StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }
}

