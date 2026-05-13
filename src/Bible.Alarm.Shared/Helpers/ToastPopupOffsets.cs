#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Pure arithmetic for centered toast popup offsets (DIP).
/// </summary>
public static class ToastPopupOffsets
{
    public static double HorizontalCenterOffset(double outerWidth, double innerWidth) =>
        (outerWidth - innerWidth) / 2;

    public static double VerticalOffsetAboveBottom(double outerHeight, double innerHeight, double bottomMarginDip) =>
        outerHeight - innerHeight - bottomMarginDip;
}
