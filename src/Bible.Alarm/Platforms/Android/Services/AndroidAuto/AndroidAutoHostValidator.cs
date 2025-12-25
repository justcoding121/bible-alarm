#nullable enable

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// Shared helper for validating Android Auto/AAOS host packages.
/// Used to prevent non-car hosts (e.g., lock screen, SystemUI) from connecting to MediaSession
/// and accessing metadata/controls.
/// </summary>
public static class AndroidAutoHostValidator
{
    /// <summary>
    /// Checks if the given package name is a valid Android Auto/AAOS host.
    /// Prevents lock screen, SystemUI, and other non-car hosts from connecting.
    /// </summary>
    /// <param name="packageName">The package name of the client trying to connect</param>
    /// <returns>True if the package is a valid car host, false otherwise</returns>
    public static bool IsCarHostPackage(string? packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return false;
        }

        // Android Auto (phone projection)
        if (packageName.Equals("com.google.android.projection.gearhead", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Emulator DHU / Google automotive projection variants sometimes use these prefixes
        if (packageName.Contains("car", StringComparison.OrdinalIgnoreCase)
            || packageName.Contains("auto", StringComparison.OrdinalIgnoreCase)
            || packageName.StartsWith("com.google.android.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

