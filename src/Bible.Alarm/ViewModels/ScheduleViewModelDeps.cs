#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed record ScheduleViewModelDeps(
    ILogger Logger,
    IServiceProvider ServiceProvider,
    IMapper Mapper,
    IState<ApplicationState> ApplicationState,
    IState<PlaybackState> PlaybackState,
    IDispatcher Dispatcher,
    IScheduleInitializationService ScheduleInitializationService,
    IScheduleCommandService ScheduleCommandService,
    IScheduleMediaCacheService ScheduleMediaCacheService,
    IScheduleContainerService ScheduleContainerService);
