#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// WinUI toast popup ARGB values separated from WinRT factories for coverage on net10 test runs.
/// </summary>
public static class ToastPopupThemeArgb
{
    public static (byte A, byte R, byte G, byte B) Background(bool isDarkTheme) =>
        isDarkTheme
            ? ((byte)0xF2, (byte)0x2A, (byte)0x2A, (byte)0x2A)
            : ((byte)0xCC, (byte)0, (byte)0, (byte)0);
}
