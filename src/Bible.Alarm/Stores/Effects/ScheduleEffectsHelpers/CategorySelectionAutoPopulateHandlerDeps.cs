#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>Constructor dependencies for <see cref="CategorySelectionAutoPopulateHandler"/>.</summary>
public sealed record CategorySelectionAutoPopulateHandlerDeps(
    IBiblePublicationService BiblePublicationService,
    IMediaService MediaService,
    ILanguageContentService LanguageContentService,
    ILanguageNameService LanguageNameService,
    BiblePublicationSelectionItemSelector ItemSelector,
    IState<ApplicationState> State,
    IServiceScopeFactory ScopeFactory,
    Serilog.ILogger Logger);
