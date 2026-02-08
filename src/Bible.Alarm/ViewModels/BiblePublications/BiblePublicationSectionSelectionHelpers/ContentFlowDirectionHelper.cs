#nullable enable

using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

internal static class ContentFlowDirectionHelper
{
    internal static FlowDirection GetContentFlowDirection(string? direction)
    {
        return string.Equals(direction, AppConstants.Media.TextDirectionRightToLeft, StringComparison.OrdinalIgnoreCase)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }
}

