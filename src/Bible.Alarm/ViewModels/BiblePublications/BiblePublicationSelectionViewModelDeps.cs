#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.BiblePublications;

public sealed record BiblePublicationSelectionViewModelDeps(
    IMediaService MediaService,
    IServiceScopeFactory ScopeFactory,
    IState<ApplicationState> ApplicationState,
    IDispatcher Dispatcher,
    INavigationService NavigationService,
    IServiceProvider ServiceProvider,
    IBiblePublicationService? BiblePublicationService = null);
