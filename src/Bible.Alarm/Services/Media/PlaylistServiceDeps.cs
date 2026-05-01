#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media;

public sealed record PlaylistServiceDeps(
    ILogger Logger,
    IMediaService MediaService,
    IDispatcher Dispatcher,
    IState<ApplicationState> ApplicationState,
    IAlarmScheduleService AlarmScheduleService,
    IGeneralSettingsService GeneralSettingsService,
    IBiblePublicationService BiblePublicationService,
    IMediaUrlRefreshService UrlRefreshService,
    IUrlConstructionService UrlConstructionService,
    ILanguageContentService? LanguageContentService = null,
    IServiceScopeFactory? ScopeFactory = null,
    IScheduleDisplayNameService? ScheduleDisplayNameService = null);
