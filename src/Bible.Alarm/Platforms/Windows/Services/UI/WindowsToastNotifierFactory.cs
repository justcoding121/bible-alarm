#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Shared.Constants;
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

            Log.Warning(AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.UnableToCreateToastNotifierHints);
        }
        catch (Exception ex)
        {
            // Catch any unexpected exceptions during notifier creation
            Log.Warning(ex, AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.UnexpectedErrorCreatingToastNotifier);
        }

        return null;
    }

    private static ToastNotifier? TryCreateNotifierWithoutParameters()
    {
        try
        {
            Log.Debug(AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.AttemptingToCreateNotifierWithoutParameters);
            var notifier = ToastNotificationManager.CreateToastNotifier();
            if (notifier is not null)
            {
                Log.Debug(AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.SuccessfullyCreatedNotifierWithoutParameters);
                return notifier;
            }
            Log.Warning(AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.CreateToastNotifierReturnedNull);
        }
        catch (COMException ex)
        {
            // Check HResult - 0x80070490 = Element not found
            // This is common in debug mode or when app is not registered for notifications
            if (ex.HResult == unchecked((int)0x80070490))
            {
                // This is expected and handled gracefully - no need to log as error
                Log.Debug(AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.FailedWithoutParameters80070490DebugHint);
            }
            else
            {
                // Catch any other COM exceptions
                Log.Debug(ex, AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.COMExceptionCreatingNotifierWithoutParametersHResultTryingAumid, ex.HResult);
            }
        }
        catch (Exception ex)
        {
            // Catch any other exceptions - safely get HResult if available
            var hResult = ex.HResult;
            Log.Debug(ex, AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.ExceptionCreatingNotifierWithoutParametersHResultTryingAumid, hResult);
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

            Log.Warning(AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.FailedToCreateNotifierWithAnyAumidPackage,
                packageId.Name, packageId.FamilyName, packageId.Publisher);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.PackageCurrentUnavailableUnpackagedHint);
        }
        catch (Exception ex)
        {
            Log.Error(ex, AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.ExceptionTryingToCreateNotifierWithAumid);
        }
        return null;
    }

    private static ToastNotifier? TryCreateNotifierWithAumid(string aumid)
    {
        try
        {
            Log.Debug(AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.TryingToCreateNotifierWithAumid, aumid);
            var notifier = ToastNotificationManager.CreateToastNotifier(aumid);
            if (notifier is not null)
            {
                Log.Information(AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.SuccessfullyCreatedNotifierWithAumid, aumid);
                return notifier;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, AppConstants.Logging.WindowsToastNotifierFactoryDiagnosticsLog.FailedToCreateNotifierWithAumidHResult, aumid, ex.HResult);
        }
        return null;
    }
}

