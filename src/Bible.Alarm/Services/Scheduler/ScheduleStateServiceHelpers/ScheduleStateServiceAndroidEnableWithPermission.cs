#if ANDROID
#nullable enable

using System.Linq;
using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Platforms.Android.Services.Helpers;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.General;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Scheduler.ScheduleStateServiceHelpers;

/// <summary>
/// Android: when enabling a schedule with NotificationEnabled=true but permission not granted,
/// enable IsEnabled in DB and show notification permission modal; state is updated on modal dismiss.
/// </summary>
internal static class ScheduleStateServiceAndroidEnableWithPermission
{
    public static async Task<(bool handled, AlarmSchedule? updatedSchedule)> TryHandleEnableAsync(
        ScheduleAndroidEnablePermissionRequest req)
    {
        var scheduleId = req.ScheduleId;
        var isEnabled = req.IsEnabled;
        var logger = req.Logger;
        var alarmScheduleService = req.AlarmScheduleService;
        var alarmService = req.AlarmService;
        var dispatcher = req.Dispatcher;
        var navigationService = req.NavigationService;
        var serviceProvider = req.ServiceProvider;
        var isSecurityException = req.IsSecurityException;
        var handleSecurityExceptionAsync = req.HandleSecurityExceptionAsync;
        var cancellationToken = req.CancellationToken;

        var schedule = await alarmScheduleService.GetScheduleByIdAsync(scheduleId, false, false);
        if (schedule == null || !schedule.NotificationEnabled)
            return (false, null);

        var granted = NotificationPermissionHelper.IsNotificationPermissionGranted();
        if (granted)
            return (false, null);

        logger.Information("Schedule {ScheduleId} has NotificationEnabled=true but permission not granted - enabling reminder and showing permission modal", scheduleId);

        AlarmSchedule? androidUpdatedSchedule = null;
        try
        {
            androidUpdatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
                scheduleId,
                s => { s.IsEnabled = isEnabled; },
                cancellationToken);
        }
        catch (Exception ex)
        {
            if (isSecurityException(ex))
            {
                _ = await handleSecurityExceptionAsync(scheduleId, ex);
                return (true, null);
            }
            throw;
        }

        await alarmService.Update(androidUpdatedSchedule);

        BeginAndroidNotificationPermissionModalAfterEnable(logger, navigationService, serviceProvider, dispatcher, scheduleId);

        return (true, androidUpdatedSchedule);
    }

    private static void BeginAndroidNotificationPermissionModalAfterEnable(
        ILogger logger,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IDispatcher dispatcher,
        int scheduleId)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var notificationViewModel = new NotificationPermissionViewModel(
                    logger,
                    navigationService,
                    onModalDismissed: permissionGranted =>
                        DispatchAndroidNotificationPermissionResult(serviceProvider, dispatcher, logger, scheduleId, permissionGranted));
                notificationViewModel.StartPermissionCheckTimer();
                await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "EnableScheduleAsync: Error opening notification permission modal");
            }
        });
    }

    private static void DispatchAndroidNotificationPermissionResult(
        IServiceProvider serviceProvider,
        IDispatcher dispatcher,
        ILogger logger,
        int scheduleId,
        bool permissionGranted)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
                var schedules = state.Value.Schedules;
                var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == scheduleId);
                if (scheduleToUpdate != null)
                {
                    var mapper = serviceProvider.GetRequiredService<IMapper>();
                    var updatedSchedule = mapper.Map<ScheduleStateItem>(scheduleToUpdate);
                    updatedSchedule.NotificationEnabled = permissionGranted;
                    dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                    logger.Information("EnableScheduleAsync: Permission {PermissionStatus} from modal - set NotificationEnabled to {NotificationEnabled} in state for schedule {ScheduleId}. DB will be updated on save.",
                        permissionGranted ? "granted" : "denied", permissionGranted, scheduleId);
                }
                else
                {
                    logger.Warning("EnableScheduleAsync: Schedule not found in Schedules collection when trying to set NotificationEnabled. ScheduleId={ScheduleId}", scheduleId);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "EnableScheduleAsync: Error updating NotificationEnabled in state after permission check");
            }
        });
    }
}
#endif
