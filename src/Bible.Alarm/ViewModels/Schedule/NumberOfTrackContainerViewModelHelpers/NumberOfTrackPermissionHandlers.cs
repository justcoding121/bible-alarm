#nullable enable

using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Microsoft.Maui.ApplicationModel;
using Serilog;

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainerViewModelHelpers;

public static class NumberOfTrackPermissionHandlers
{
    public static void HandlePermissionGranted(
        bool isWaitingForPermissionResponse,
        ILogger logger,
        Action setNotificationEnabledOn,
        Action<bool> setIsUpdatingFromPermissionCheck,
        Action<bool> setIsWaitingForPermissionResponse)
    {
        logger.Information(
            "NotificationPermissionService: Permission granted event received. isWaitingForPermissionResponse={IsWaiting}",
            isWaitingForPermissionResponse);

        if (!isWaitingForPermissionResponse)
        {
            logger.Debug("Permission granted event received but not waiting for response - ignoring");
            return;
        }

        setIsUpdatingFromPermissionCheck(true);
        try
        {
            setNotificationEnabledOn();
            setIsWaitingForPermissionResponse(false);
        }
        finally
        {
            setIsUpdatingFromPermissionCheck(false);
        }
    }

    public static void HandlePermissionDenied(
        bool isWaitingForPermissionResponse,
        ILogger logger,
        INavigationService navigationService,
        Action setNotificationEnabledOff,
        Action onGrantedFromModal,
        Action<bool> setIsUpdatingFromPermissionCheck,
        Action<bool> setIsWaitingForPermissionResponse)
    {
        logger.Information("NotificationPermissionService: Permission denied event received");

        if (!isWaitingForPermissionResponse)
        {
            return;
        }

        setIsUpdatingFromPermissionCheck(true);
        try
        {
            setNotificationEnabledOff();
            logger.Debug("Set NotificationEnabled to false after permission denied");

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await NotificationPermissionDeniedModalHelper.ShowAsync(
                    logger, navigationService,
                    onPermissionGranted: () =>
                    {
                        setIsUpdatingFromPermissionCheck(true);
                        try
                        {
                            onGrantedFromModal();
                        }
                        finally
                        {
                            setIsUpdatingFromPermissionCheck(false);
                        }
                    });
            });
        }
        finally
        {
            setIsUpdatingFromPermissionCheck(false);
            setIsWaitingForPermissionResponse(false);
        }
    }
}
