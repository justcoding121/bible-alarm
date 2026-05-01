#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
#if ANDROID || IOS
using Bible.Alarm.Services.Scheduler.ScheduleStateServiceHelpers;
#endif
using Bible.Alarm.ViewModels.General;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Shared.Models.Schedule;

#if ANDROID
using Bible.Alarm.Platforms.Android.Services.Helpers;
#elif IOS
using Bible.Alarm.Platforms.iOS.Services.Helpers;
#endif

namespace Bible.Alarm.Services.Scheduler;

public sealed partial class ScheduleStateService(ScheduleStateServiceDeps deps) : IScheduleStateService
{
    private readonly ILogger logger = deps.Logger;
    private readonly IAlarmScheduleService alarmScheduleService = deps.AlarmScheduleService;
    private readonly IAlarmService alarmService = deps.AlarmService;
#if ANDROID || IOS
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Used only when CheckNotificationPermissionsAsync is compiled (#if !ANDROID && !IOS).")]
#endif
    private readonly INotificationService notificationService = deps.NotificationService;
    private readonly IToastService toastService = deps.ToastService;
    private readonly IDispatcher dispatcher = deps.Dispatcher;
#if ANDROID || IOS
    private readonly INavigationService navigationService = deps.NavigationService;
    private readonly IServiceProvider serviceProvider = deps.ServiceProvider;
#endif
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<bool> UpdateScheduleEnabledStateAsync(int scheduleId, bool isEnabled)
    {
#if !ANDROID && !IOS
        if (isEnabled && !await CheckNotificationPermissionsAsync(scheduleId))
        {
            return false;
        }
#endif
        if (isEnabled)
        {
#if ANDROID
            var (androidHandled, androidUpdated) = await ScheduleStateServiceAndroidEnableWithPermission.TryHandleEnableAsync(
                new ScheduleAndroidEnablePermissionRequest(
                    scheduleId,
                    isEnabled,
                    logger,
                    alarmScheduleService,
                    alarmService,
                    dispatcher,
                    navigationService,
                    serviceProvider,
                    IsSecurityException,
                    HandleSecurityExceptionAsync,
                    cancellationTokenSource.Token));
            if (androidHandled)
            {
                if (androidUpdated is AlarmSchedule storedAndroidSchedule)
                {
                    UpdateFluxorStore(storedAndroidSchedule);
                    await ShowNotificationIfEnabledAsync(isEnabled, storedAndroidSchedule);
                }

                return true;
            }
#elif IOS
            var (iosHandled, iosUpdated) = await ScheduleStateServiceIosEnableWithPermission.TryHandleEnableAsync(
                new ScheduleIosEnablePermissionRequest(
                    scheduleId,
                    isEnabled,
                    logger,
                    alarmScheduleService,
                    alarmService,
                    dispatcher,
                    navigationService,
                    serviceProvider,
                    IsSecurityException,
                    HandleSecurityExceptionAsync,
                    UpdateFluxorStore,
                    cancellationTokenSource.Token));
            if (iosHandled)
            {
                if (iosUpdated is AlarmSchedule storedIosSchedule)
                {
                    UpdateFluxorStore(storedIosSchedule);
                    await ShowNotificationIfEnabledAsync(isEnabled, storedIosSchedule);
                }

                return true;
            }
#endif
        }

        // Update database and alarm service
        AlarmSchedule? updatedSchedule = null;
        try
        {
            updatedSchedule = await UpdateScheduleEnabledStateInDatabaseAsync(scheduleId, isEnabled);
        }
        catch (Exception ex)
        {
            if (IsSecurityException(ex))
            {
                return await HandleSecurityExceptionAsync(scheduleId, ex);
            }
            throw;
        }

        UpdateFluxorStore(updatedSchedule);
        await ShowNotificationIfEnabledAsync(isEnabled, updatedSchedule);

        return true;
    }

#if !ANDROID && !IOS
    private async Task<bool> CheckNotificationPermissionsAsync(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, false, false);
        if (DeviceInfo.Platform == DevicePlatform.WinUI
            && schedule != null && schedule.NotificationEnabled
            && !await notificationService.CanScheduleAsync())
        {
            logger.Warning(AppConstants.Logging.ScheduleEnableDiagnosticsLog.CannotEnableNotificationDeniedTapToPlay, scheduleId);
            await toastService.ShowMessage(
                AppConstants.ToastMessages.NotificationPermissionRequiredTapToPlayWinUi,
                7);
            return false;
        }
        return true;
    }
#endif

    private async Task<AlarmSchedule> UpdateScheduleEnabledStateInDatabaseAsync(int scheduleId, bool isEnabled)
    {
        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            scheduleId,
            schedule => schedule.IsEnabled = isEnabled,
            cancellationTokenSource.Token);

        await alarmService.Update(updatedSchedule);
        return updatedSchedule;
    }

    private static bool IsSecurityException(Exception ex)
    {
        for (Exception? cur = ex; cur != null; cur = cur.InnerException)
        {
            var exceptionType = cur.GetType().FullName;
            if (string.Equals(exceptionType, "Java.Lang.SecurityException", StringComparison.Ordinal) ||
                cur.Message.Contains("SCHEDULE_EXACT_ALARM", StringComparison.Ordinal) ||
                cur.Message.Contains("USE_EXACT_ALARM", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> HandleSecurityExceptionAsync(int scheduleId, Exception ex)
    {
        logger.Error(ex, AppConstants.Logging.AndroidExactAlarmSchedulingLog.SecurityExceptionUpdatingSchedule, scheduleId);

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            await toastService.ShowMessage(
                AppConstants.ToastMessages.CannotScheduleReminderExactAlarmPermission,
                7);
        }

        return false;
    }

    private void UpdateFluxorStore(AlarmSchedule? updatedSchedule)
    {
        if (updatedSchedule != null)
        {
            dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    private async Task ShowNotificationIfEnabledAsync(bool isEnabled, AlarmSchedule? updatedSchedule)
    {
        if (isEnabled && updatedSchedule != null)
        {
            await toastService.ShowScheduledNotification(updatedSchedule);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
        }

        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

