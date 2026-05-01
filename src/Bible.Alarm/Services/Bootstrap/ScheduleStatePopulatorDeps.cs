#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Services.Bootstrap;

public sealed record ScheduleStatePopulatorDeps(
    IBiblePublicationService? BiblePublicationService,
    IBiblePublicationSectionService? BiblePublicationSectionService,
    IMapper Mapper,
    IMediaService? MediaService,
    IMelodyMusicService? MelodyMusicService,
    IVocalMusicService? VocalMusicService,
    IServiceScopeFactory ScopeFactory);
