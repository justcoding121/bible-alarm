#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;

namespace Bible.Alarm.Stores.Effects;

/// <summary>
/// Optional <see cref="ScheduleEffects"/> overrides resolved from DI.
/// </summary>
public sealed record ScheduleEffectsOptionalDeps(
    IBiblePublicationService? BiblePublicationService = null,
    IBiblePublicationSectionService? BiblePublicationSectionService = null,
    IAlarmScheduleService? AlarmScheduleService = null,
    IAlarmService? AlarmService = null,
    IMediaCacheService? MediaCacheService = null,
    IMediaService? MediaService = null,
    IState<ApplicationState>? State = null,
    IScheduleDisplayNameService? ScheduleDisplayNameService = null);
