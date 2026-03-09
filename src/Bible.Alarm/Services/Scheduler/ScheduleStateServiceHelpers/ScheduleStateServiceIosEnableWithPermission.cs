#if IOS
#nullable enable

using System.Linq;
using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Microsoft.Extensions.DependencyInjection;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.General;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
using Bible.Alarm.Platforms.iOS.Services.Helpers;

namespace Bible.Alarm.Services.Scheduler.ScheduleStateServiceHelpers;

/// <summary>
/// iOS: when enabling a schedule but permission not granted, enable IsEnabled in DB and show
/// notification permission modal; on dismiss, update IsEnabled in DB and Fluxor store.
/// </summary>
internal static class ScheduleStateServiceIosEnableWithPermission
{
    public static async Task<(bool handled, AlarmSchedule? updatedSchedule)> TryHandleEnableAsync(
        int scheduleId,
        bool isEnabled,
        ILogger logger,
        IAlarmScheduleService alarmScheduleService,
        IAlarmService alarmService,
        IDispatcher dispatcher,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        Func<Exception, bool> isSecurityException,
        Func<int, Exception, Task<bool>> handleSecurityExceptionAsync,
        Action<AlarmSchedule?> updateFluxorStore)
    {
        try
        {
            var permissionService = IOSNotificationPermissionService.Instance;
            bool isPermissionGranted = false;
            try
            {
                permissionService.InvalidateCache();
                isPermissionGranted = await permissionService.IsGrantedAsync();
                logger.Debug("EnableScheduleAsync (iOS): Permission check result - Granted: {IsGranted}", isPermissionGranted);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "EnableScheduleAsync (iOS): Exception checking permission - assuming not granted");
                try
                {
                    isPermissionGranted = permissionService.IsGranted;
                }
                catch (Exception syncEx)
                {
                    logger.Error(syncEx, "EnableScheduleAsync (iOS): Exception in fallback sync permission check");
                    isPermissionGranted = false;
                }
            }

            if (isPermissionGranted)
                return (false, null);

            logger.Information("Schedule {ScheduleId} has IsEnabled=true but permission not granted (iOS) - enabling reminder and showing permission modal", scheduleId);

            AlarmSchedule? iosUpdatedSchedule = null;
            try
            {
                iosUpdatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
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

            await alarmService.Update(iosUpdatedSchedule);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var notificationViewModel = new NotificationPermissionViewModel(
                        logger,
                        navigationService,
                        serviceProvider,
                        onModalDismissed: (permissionGranted) =>
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                try
                                {
                                    try
                                    {
                                        var dbUpdatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
                                            scheduleId,
                                            s => s.IsEnabled = permissionGranted,
                                            cancellationToken);
                                        updateFluxorStore(dbUpdatedSchedule);
                                        logger.Information("EnableScheduleAsync (iOS): Permission {PermissionStatus} from modal - set IsEnabled to {IsEnabled} in DB for schedule {ScheduleId}.",
                                            permissionGranted ? "granted" : "denied", permissionGranted, scheduleId);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.Error(ex, "EnableScheduleAsync (iOS): Error updating IsEnabled in DB after permission check");
                                        var state = serviceProvider.GetRequiredService<IState<ApplicationState>>();
                                        var schedules = state.Value.Schedules;
                                        var scheduleToUpdate = schedules?.FirstOrDefault(s => s.Id == scheduleId);
                                        if (scheduleToUpdate != null)
                                        {
                                            var mapper = serviceProvider.GetRequiredService<IMapper>();
                                            var updatedSchedule = mapper.Map<ScheduleStateItem>(scheduleToUpdate);
                                            updatedSchedule.IsEnabled = permissionGranted;
                                            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, false, false, shouldSave: false));
                                        }
                                        else
                                            logger.Warning("EnableScheduleAsync (iOS): Schedule not found in Schedules collection when trying to set IsEnabled. ScheduleId={ScheduleId}", scheduleId);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    logger.Error(ex, "EnableScheduleAsync (iOS): Error updating IsEnabled after permission check");
                                }
                            });
                        });
                    notificationViewModel.StartPermissionCheckTimer();
                    await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "EnableScheduleAsync (iOS): Error opening notification permission modal");
                }
            });

            return (true, iosUpdatedSchedule);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "EnableScheduleAsync (iOS): Exception checking notification permission");
            return (false, null);
        }
    }
}
#endif
