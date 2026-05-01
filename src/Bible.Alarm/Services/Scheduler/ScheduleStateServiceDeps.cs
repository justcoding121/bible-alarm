#nullable enable

using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Scheduler;

/// <summary>Constructor dependencies for <see cref="ScheduleStateService"/>.</summary>
public sealed record ScheduleStateServiceDeps(
    ILogger Logger,
    IAlarmScheduleService AlarmScheduleService,
    IAlarmService AlarmService,
    INotificationService NotificationService,
    IToastService ToastService,
    IDispatcher Dispatcher,
    INavigationService NavigationService,
    IServiceProvider ServiceProvider);
