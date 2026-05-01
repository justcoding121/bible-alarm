#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule;

public sealed record MusicSelectionContainerViewModelDeps(
    ILogger Logger,
    INavigationService NavigationService,
    IScheduleSelectionService ScheduleSelectionService,
    IMediaService MediaService,
    IState<ApplicationState> ApplicationState,
    IDispatcher Dispatcher,
    IMapper Mapper,
    IServiceProvider ServiceProvider,
    IToastService ToastService);
