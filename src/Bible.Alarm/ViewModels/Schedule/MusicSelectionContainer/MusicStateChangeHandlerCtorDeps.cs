#nullable enable

using AutoMapper;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

public sealed record MusicStateChangeHandlerServices(
    ILogger Logger,
    IState<ApplicationState> State,
    IDispatcher Dispatcher,
    IMapper Mapper,
    IServiceProvider ServiceProvider);

public sealed record MusicStateChangeHandlerCollaborators(
    MusicStateTracker StateTracker,
    MusicPropertyNotifier PropertyNotifier,
    MusicDisplayTextProvider DisplayTextProvider);
