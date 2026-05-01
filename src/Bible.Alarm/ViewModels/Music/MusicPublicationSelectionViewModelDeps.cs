#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public sealed record MusicPublicationSelectionViewModelDeps(
    IMediaService MediaService,
    IServiceScopeFactory ScopeFactory,
    IState<ApplicationState> ApplicationState,
    IDispatcher Dispatcher,
    INavigationService NavigationService,
    IServiceProvider ServiceProvider);
