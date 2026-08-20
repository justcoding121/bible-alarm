#nullable enable

using System.Linq;
using AutoMapper;
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

namespace Bible.Alarm.Platforms.iOS.Services.Helpers;

internal sealed record ScheduleIosEnablePermissionRequest(
    int ScheduleId,
    bool IsEnabled,
    ILogger Logger,
    IAlarmScheduleService AlarmScheduleService,
    IAlarmService AlarmService,
    IDispatcher Dispatcher,
    INavigationService NavigationService,
    IServiceProvider ServiceProvider,
    Func<Exception, bool> IsSecurityException,
    Func<int, Exception, Task<bool>> HandleSecurityExceptionAsync,
    Action<AlarmSchedule?> UpdateFluxorStore,
    CancellationToken CancellationToken);

/// <summary>
/// iOS: when enabling a schedule but permission not granted, enable IsEnabled in DB and show
/// notification permission modal; on dismiss, update IsEnabled in DB and Fluxor store.
/// </summary>
internal static class ScheduleStateServiceIosEnableWithPermission
{
    public static async Task<(bool handled, AlarmSchedule? updatedSchedule)> TryHandleEnableAsync(
        ScheduleIosEnablePermissionRequest req)
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
        var updateFluxorStore = req.UpdateFluxorStore;
        var cancellationToken = req.CancellationToken;

        try
        {
            var permissionService = IosNotificationPermissionService.Instance;
            bool isPermissionGranted = false;
            try
            {
                permissionService.InvalidateCache();
                isPermissionGranted = await IosNotificationPermissionService.IsGrantedAsync();
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

            BeginIosNotificationPermissionModalAfterEnable(new BeginIosNotificationPermissionModalAfterEnableArgs
            {
                Logger = logger,
                NavigationService = navigationService,
                AlarmScheduleService = alarmScheduleService,
                Dispatcher = dispatcher,
                ServiceProvider = serviceProvider,
                UpdateFluxorStore = updateFluxorStore,
                CancellationToken = cancellationToken,
                ScheduleId = scheduleId
            });

            return (true, iosUpdatedSchedule);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "EnableScheduleAsync (iOS): Exception checking notification permission");
            return (false, null);
        }
    }

    private static void BeginIosNotificationPermissionModalAfterEnable(BeginIosNotificationPermissionModalAfterEnableArgs args)
    {
        var logger = args.Logger;
        var navigationService = args.NavigationService;
        var alarmScheduleService = args.AlarmScheduleService;
        var dispatcher = args.Dispatcher;
        var serviceProvider = args.ServiceProvider;
        var updateFluxorStore = args.UpdateFluxorStore;
        var cancellationToken = args.CancellationToken;
        var scheduleId = args.ScheduleId;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var notificationViewModel = new NotificationPermissionViewModel(
                    logger,
                    navigationService,
                    onModalDismissed: permissionGranted =>
                        MainThread.BeginInvokeOnMainThread(async () =>
                            await ApplyIosPermissionDismissAsync(new ApplyIosPermissionDismissArgs
                            {
                                Logger = logger,
                                AlarmScheduleService = alarmScheduleService,
                                Dispatcher = dispatcher,
                                ServiceProvider = serviceProvider,
                                UpdateFluxorStore = updateFluxorStore,
                                CancellationToken = cancellationToken,
                                ScheduleId = scheduleId,
                                PermissionGranted = permissionGranted
                            })));
                notificationViewModel.StartPermissionCheckTimer();
                await navigationService.OpenNotificationPermissionModalAsync(notificationViewModel);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "EnableScheduleAsync (iOS): Error opening notification permission modal");
            }
        });
    }

    private static async Task ApplyIosPermissionDismissAsync(ApplyIosPermissionDismissArgs args)
    {
        var logger = args.Logger;
        var alarmScheduleService = args.AlarmScheduleService;
        var dispatcher = args.Dispatcher;
        var serviceProvider = args.ServiceProvider;
        var updateFluxorStore = args.UpdateFluxorStore;
        var cancellationToken = args.CancellationToken;
        var scheduleId = args.ScheduleId;
        var permissionGranted = args.PermissionGranted;

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
                {
                    logger.Warning("EnableScheduleAsync (iOS): Schedule not found in Schedules collection when trying to set IsEnabled. ScheduleId={ScheduleId}", scheduleId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "EnableScheduleAsync (iOS): Error updating IsEnabled after permission check");
        }
    }

    private sealed class BeginIosNotificationPermissionModalAfterEnableArgs
    {
        public required ILogger Logger { get; init; }
        public required INavigationService NavigationService { get; init; }
        public required IAlarmScheduleService AlarmScheduleService { get; init; }
        public required IDispatcher Dispatcher { get; init; }
        public required IServiceProvider ServiceProvider { get; init; }
        public required Action<AlarmSchedule?> UpdateFluxorStore { get; init; }
        public required CancellationToken CancellationToken { get; init; }
        public required int ScheduleId { get; init; }
    }

    private sealed class ApplyIosPermissionDismissArgs
    {
        public required ILogger Logger { get; init; }
        public required IAlarmScheduleService AlarmScheduleService { get; init; }
        public required IDispatcher Dispatcher { get; init; }
        public required IServiceProvider ServiceProvider { get; init; }
        public required Action<AlarmSchedule?> UpdateFluxorStore { get; init; }
        public required CancellationToken CancellationToken { get; init; }
        public required int ScheduleId { get; init; }
        public required bool PermissionGranted { get; init; }
    }
}
