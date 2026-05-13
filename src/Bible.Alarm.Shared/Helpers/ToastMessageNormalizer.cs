#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Normalizes user-visible toast messages before WinUI allocation.
/// </summary>
public static class ToastMessageNormalizer
{
    public static string Normalize(string message) =>
        string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
}
