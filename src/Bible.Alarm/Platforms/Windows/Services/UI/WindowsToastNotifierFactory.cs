#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Shared.Helpers;
using Serilog;
using Windows.ApplicationModel;
using Windows.UI.Notifications;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

internal static class WindowsToastNotifierFactory
{
    internal static ToastNotifier? GetToastNotifier()
    {
        try
        {
            var notifier = TryCreateNotifierWithoutParameters();
            if (notifier is not null)
            {
                return notifier;
            }

            notifier = TryCreateNotifierWithAumid();
            if (notifier is not null)
            {
                return notifier;
            }

            Log.Warning(
                "Unable to create toast notifier. Scheduled notifications will not work. " +
                "This is common in debug mode or when the app is not properly registered for notifications. " +
                "Try running the app from an installed package instead of Visual Studio.");
        }
        catch (Exception ex)
        {
            // Catch any unexpected exceptions during notifier creation
            Log.Warning(ex, "Unexpected error creating toast notifier. Scheduled notifications will not work.");
        }

        return null;
    }

    private static ToastNotifier? TryCreateNotifierWithoutParameters()
    {
        try
        {
            Log.Debug("Attempting to create toast notifier without parameters...");
            var notifier = ToastNotificationManager.CreateToastNotifier();
            if (notifier is not null)
            {
                Log.Debug("Successfully created toast notifier without parameters");
                return notifier;
            }
            Log.Warning("ToastNotificationManager.CreateToastNotifier() returned null");
        }
        catch (COMException ex)
        {
            // Check HResult - 0x80070490 = Element not found
            // This is common in debug mode or when app is not registered for notifications
            if (ex.HResult == unchecked((int)0x80070490))
            {
                // This is expected and handled gracefully - no need to log as error
                Log.Debug("Failed to create toast notifier without parameters (0x80070490 - Element not found). This is expected in debug mode. Trying with AUMID...");
            }
            else
            {
                // Catch any other COM exceptions
                Log.Debug(ex, "COMException creating toast notifier without parameters. HResult: 0x{HR:X8}. Trying with AUMID...", ex.HResult);
            }
        }
        catch (Exception ex)
        {
            // Catch any other exceptions - safely get HResult if available
            var hResult = ex.HResult;
            Log.Debug(ex, "Exception creating toast notifier without parameters. HResult: 0x{HR:X8}. Trying with AUMID...", hResult);
        }
        return null;
    }

    private static ToastNotifier? TryCreateNotifierWithAumid()
    {
        try
        {
            var package = Package.Current;
            var packageId = package.Id;

            string[] aumidFormats =
            [
                $"{packageId.FamilyName}!App",
                packageId.FamilyName,
                packageId.Name,
            ];

            foreach (var aumid in aumidFormats)
            {
                var notifier = TryCreateNotifierWithAumid(aumid);
                if (notifier is not null)
                {
                    return notifier;
                }
            }

            Log.Warning(
                "Failed to create toast notifier with any AUMID format. " +
                "Package: {PackageName}, FamilyName: {FamilyName}, Publisher: {Publisher}",
                packageId.Name, packageId.FamilyName, packageId.Publisher);
        }
        catch (InvalidOperationException)
        {
            Log.Warning(
                "Package.Current is not available. This is expected in debug mode or unpackaged WinUI 3 apps. " +
                "Scheduled notifications require the app to be properly packaged and installed.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception while trying to create toast notifier with AUMID");
        }
        return null;
    }

    private static ToastNotifier? TryCreateNotifierWithAumid(string aumid)
    {
        try
        {
            Log.Debug("Trying to create toast notifier with AUMID: {AUMID}", aumid);
            var notifier = ToastNotificationManager.CreateToastNotifier(aumid);
            if (notifier is not null)
            {
                Log.Information("Successfully created toast notifier with AUMID: {AUMID}", aumid);
                return notifier;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to create toast notifier with AUMID '{AUMID}'. HResult: 0x{HR:X8}", aumid, ex.HResult);
        }
        return null;
    }
}

