#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Services.Media;

/// <summary>
/// Constructor dependencies for <see cref="MediaService"/>.
/// </summary>
public readonly record struct MediaServiceDependencies(
    IMediaIndexService MediaIndexService,
    IBiblePublicationService BiblePublicationService,
    IBiblePublicationSectionService BiblePublicationSectionService,
    IBiblePublicationTrackService BiblePublicationTrackService,
    IMelodyMusicService MelodyMusicService,
    IVocalMusicService VocalMusicService,
    ILanguageContentService LanguageContentService,
    IServiceScopeFactory ScopeFactory);
