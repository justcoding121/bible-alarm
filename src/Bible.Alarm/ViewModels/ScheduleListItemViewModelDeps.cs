#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed record ScheduleListItemViewModelDeps(
    ILogger Logger,
    ISchedulePlaybackService PlaybackService,
    IPlaybackService StopPlaybackService,
    IScheduleStateService ScheduleStateService,
    IState<ApplicationState> ApplicationState,
    IState<PlaybackState> PlaybackState,
    IDispatcher Dispatcher,
    IMapper Mapper,
    ICategoryNameService CategoryNameService);
