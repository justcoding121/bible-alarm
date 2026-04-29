#nullable enable
using Android.OS;
using AndroidX.Media;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.LegacyMediaBrowserHelpers;

/// <summary>
/// Handles client validation for LegacyMediaBrowserService.
/// </summary>
public sealed class ClientValidator(ILogger logger)
{
    private const string RootId = "__ID_ROOT__";

    /// <summary>
    /// Validates if a client package is a car host and returns appropriate BrowserRoot.
    /// </summary>
    public MediaBrowserServiceCompat.BrowserRoot? ValidateClientAndGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
    {
        logger.Information(AppConstants.Logging.LegacyMediaBrowserClientValidatorDiagnosticsLog.OnGetRootCalledForClient,
            clientPackageName, clientUid);

        // Block non-car clients (e.g., Samsung SystemUI) from binding and generating a phone media card.
        if (!IsCarHostPackage(clientPackageName))
        {
            logger.Information(AppConstants.Logging.LegacyMediaBrowserClientValidatorDiagnosticsLog.RejectingMediaBrowserClientNonCarHost, clientPackageName);
            return null;
        }

        // Standard media root ID for Android Auto/AAOS compatibility
        // The root ID "__ID_ROOT__" is a common practice for media apps
        // Android Auto and AAOS hosts are trusted by default when connecting to MediaBrowserService
        //
        // DEFAULT RECOMMENDATIONS: We return null for rootHints to use default behavior.
        // Android Auto will automatically pull the top items from the root browse tree for "For You" recommendations.
        // The system reuses the exact MediaDescriptionCompat for those items, including their icons.
        // We do NOT provide explicit recommendations via EXTRA_SUGGESTED to keep it simple and let the system handle it.
        return new MediaBrowserServiceCompat.BrowserRoot(RootId, null);
    }

    /// <summary>
    /// Checks if the client package is a car host.
    /// Samsung/phone SystemUI may bind to any exported MediaBrowserService and show a phone media card.
    /// We only want car hosts (Android Auto / AAOS) to connect to this service.
    /// Reuse shared validation logic from AndroidAutoHostValidator.
    /// </summary>
    private static bool IsCarHostPackage(string clientPackageName)
    {
        return AndroidAutoHostValidator.IsCarHostPackage(clientPackageName);
    }
}
