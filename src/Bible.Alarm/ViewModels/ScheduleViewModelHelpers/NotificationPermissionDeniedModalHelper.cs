#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.General;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.ViewModels.ScheduleViewModelHelpers;

/// <summary>
/// Opens the notification permission modal when permission was denied.
/// The onPermissionGranted callback is invoked on the main thread when the user grants permission from the modal.
/// </summary>
public static class NotificationPermissionDeniedModalHelper
{
    public static async Task ShowAsync(
        ILogger logger,
        INavigationService navigationService,
        Action onPermissionGranted)
    {
        try
        {
            var notificationViewModel = new NotificationPermissionViewModel(
                logger,
                navigationService,
                onModalDismissed: permissionGranted =>
                {
                    if (permissionGranted)
                    {
                        MainThread.BeginInvokeOnMainThread(onPermissionGranted);
                    }
                });

            notificationViewModel.StartPermissionCheckTimer();
            await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error opening notification permission modal after permission denied");
        }
    }
}
