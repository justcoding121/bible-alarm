#nullable enable

using AutoMapper;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels;

public sealed record HomeViewModelDeps(
    ILogger Logger,
    IServiceProvider ServiceProvider,
    IState<ApplicationState> ApplicationState,
    IState<PlaybackState> PlaybackState,
    IDispatcher Dispatcher,
    INavigationService NavigationService,
    IMapper Mapper);
