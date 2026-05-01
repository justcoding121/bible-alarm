#nullable enable

namespace Bible.Alarm.Services.Scheduler.ScheduleStateServiceHelpers;

#if ANDROID

/// <summary>Parameters for <see cref="ScheduleStateServiceAndroidEnableWithPermission.TryHandleEnableAsync"/>.</summary>
public sealed record ScheduleAndroidEnablePermissionRequest(
    int ScheduleId,
    bool IsEnabled,
    Serilog.ILogger Logger,
    Bible.Alarm.Shared.Services.Schedule.Interfaces.IAlarmScheduleService AlarmScheduleService,
    Bible.Alarm.Services.Scheduler.Interfaces.IAlarmService AlarmService,
    Fluxor.IDispatcher Dispatcher,
    Bible.Alarm.Services.UI.Interfaces.INavigationService NavigationService,
    System.IServiceProvider ServiceProvider,
    Func<Exception, bool> IsSecurityException,
    Func<int, Exception, Task<bool>> HandleSecurityExceptionAsync,
    CancellationToken CancellationToken);

#elif IOS

/// <summary>Parameters for <see cref="ScheduleStateServiceIosEnableWithPermission.TryHandleEnableAsync"/>.</summary>
public sealed record ScheduleIosEnablePermissionRequest(
    int ScheduleId,
    bool IsEnabled,
    Serilog.ILogger Logger,
    Bible.Alarm.Shared.Services.Schedule.Interfaces.IAlarmScheduleService AlarmScheduleService,
    Bible.Alarm.Services.Scheduler.Interfaces.IAlarmService AlarmService,
    Fluxor.IDispatcher Dispatcher,
    Bible.Alarm.Services.UI.Interfaces.INavigationService NavigationService,
    System.IServiceProvider ServiceProvider,
    Func<Exception, bool> IsSecurityException,
    Func<int, Exception, Task<bool>> HandleSecurityExceptionAsync,
    Action<Bible.Alarm.Shared.Models.Schedule.AlarmSchedule?> UpdateFluxorStore,
    CancellationToken CancellationToken);

#else

internal static partial class SchedulePlatformEnablePermissionRequestsPlatExcluded
{
}

#endif
