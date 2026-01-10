#nullable enable
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media;

public sealed class PlaylistService : IPlaylistService, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaService mediaService;
    private readonly IDispatcher dispatcher;
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly IGeneralSettingsService generalSettingsService;
    private readonly IBiblePublicationService BiblePublicationService;
    private readonly IMelodyMusicService melodyMusicService;
    private readonly IDiskCacheService? diskCacheService;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;
    private PlaylistScheduleManager? _scheduleManager;
    private PlaylistScheduleManager scheduleManager => _scheduleManager ??= new PlaylistScheduleManager(
        logger,
        alarmScheduleService,
        generalSettingsService,
        BiblePublicationService,
        melodyMusicService,
        cancellationTokenSource.Token);
    private readonly PlaylistBibleTrackBuilder bibleTrackBuilder;
    private readonly PlaylistMusicTrackBuilder musicTrackBuilder;

    // Helper classes
    private readonly TrackChangeDetector trackChangeDetector;
    private readonly TrackNavigator trackNavigator;
    private readonly ScheduleUpdater scheduleUpdater;

    public PlaylistService(
        ILogger logger,
        IMediaService mediaService,
        IDispatcher dispatcher,
        IAlarmScheduleService alarmScheduleService,
        IGeneralSettingsService generalSettingsService,
        IBiblePublicationService BiblePublicationService,
        IMelodyMusicService melodyMusicService,
        IDiskCacheService? diskCacheService)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.dispatcher = dispatcher;
        this.alarmScheduleService = alarmScheduleService;
        this.generalSettingsService = generalSettingsService;
        this.BiblePublicationService = BiblePublicationService;
        this.melodyMusicService = melodyMusicService;
        this.diskCacheService = diskCacheService;
        bibleTrackBuilder = new PlaylistBibleTrackBuilder(logger, mediaService);
        musicTrackBuilder = new PlaylistMusicTrackBuilder(logger, mediaService, melodyMusicService);
        trackChangeDetector = new TrackChangeDetector(alarmScheduleService, cancellationTokenSource.Token);
        trackNavigator = new TrackNavigator(mediaService);
        scheduleUpdater = new ScheduleUpdater(alarmScheduleService, cancellationTokenSource.Token);
    }

    public async Task<int> GetRelevantScheduleToPlay()
    {
        return await scheduleManager.GetRelevantScheduleToPlay();
    }

    public async Task SaveLastPlayed(int scheduleId)
    {
        await scheduleManager.SaveLastPlayed(scheduleId);
    }

    public async Task MarkTrackAsPlayed(TrackMetadata trackMetadata)
    {
        var trackChanged = await trackChangeDetector.CheckIfTrackChanged(trackMetadata);
        var nextTrackNumber = await GetNextTrackNumberIfNeeded(trackMetadata);

        var updatedSchedule = await UpdateScheduleForPlayedTrack(
            trackMetadata,
            nextTrackNumber);

        if (trackChanged)
        {
            dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    private async Task<int?> GetNextTrackNumberIfNeeded(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType != PlayType.Music)
        {
            return null;
        }

        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);

        if (schedule?.Music == null || schedule.Music.Repeat)
        {
            return null;
        }

        var next = await NextMusicUrlToPlay(schedule, true);
        return next.Metadata.TrackNumber;
    }

    private async Task<AlarmSchedule> UpdateScheduleForPlayedTrack(
        TrackMetadata trackMetadata,
        int? nextTrackNumber)
    {
        // Clear cache BEFORE save to prevent stale cache if process crashes
        const string CacheKey = "ScheduleList";
        diskCacheService?.Remove(CacheKey);

        return await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => PlaylistTrackUpdater.UpdateScheduleForPlayedTrackInternal(schedule, trackMetadata, nextTrackNumber),
            cancellationTokenSource.Token);
    }



    public async Task MarkTrackAsFinished(TrackMetadata trackMetadata)
    {
        // Get next track/track before updating
        var nextTrackInfo = await GetNextTrackInfoAsync(trackMetadata);

        // Clear cache BEFORE save to prevent stale cache if process crashes
        const string CacheKey = "ScheduleList";
        diskCacheService?.Remove(CacheKey);

        // Update schedule using service
        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => UpdateScheduleForFinishedTrackInternal(schedule, trackMetadata, nextTrackInfo),
            cancellationTokenSource.Token);

        // Update the Fluxor store to trigger state change and UI refresh
        // ScheduleListItem now subscribes to ApplicationState changes instead of TrackChangedMessage
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    private async Task<NextTrackInfo> GetNextTrackInfoAsync(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            var nextTrackNumber = await GetNextMusicTrackNumberAsync(trackMetadata);
            return new NextTrackInfo(nextTrackNumber, null);
        }
        else
        {
            var nextTrack = await trackNavigator.GetNextBibleTrack(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                trackMetadata.SectionNumber,
                trackMetadata.TrackNumber);
            return new NextTrackInfo(null, nextTrack);
        }
    }

    private async Task<int?> GetNextMusicTrackNumberAsync(TrackMetadata trackMetadata)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);
        if (schedule?.Music != null && !schedule.Music.Repeat)
        {
            var next = await musicTrackBuilder.NextMusicUrlToPlay(schedule, true);
            return next.Metadata.TrackNumber;
        }
        return null;
    }

    private static void UpdateScheduleForFinishedTrackInternal(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        NextTrackInfo nextTrackInfo)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            PlaylistTrackUpdater.UpdateMusicTrackForFinished(schedule, nextTrackInfo.NextTrackNumber);
        }
        else
        {
            PlaylistTrackUpdater.UpdateBibleReadingTrackForFinished(schedule, trackMetadata, nextTrackInfo.NextTrack);
        }
    }


    private record NextTrackInfo(int? NextTrackNumber, KeyValuePair<BibleSection, BiblePublicationTrack>? NextTrack);

    public async Task<PlayItem> NextTrack(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, true, true, cancellationTokenSource.Token) ?? throw new ArgumentException($"Invalid schedule Id {scheduleId}");
        if (schedule.MusicEnabled)
        {
            return await musicTrackBuilder.NextMusicUrlToPlay(schedule);
        }

        var bibleReadingSchedule = schedule.BibleReadingSchedule ?? throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {scheduleId}");
        var sectionNumber = bibleReadingSchedule.SectionNumber ?? throw new InvalidOperationException($"SectionNumber is null for schedule {scheduleId}");
        var track = bibleReadingSchedule.TrackNumber;

        var trackDetail = await mediaService.GetBibleTrack(bibleReadingSchedule.LanguageCode,
            bibleReadingSchedule.PublicationCode, sectionNumber, track);

        if (trackDetail == null)
        {
            logger.Error(
                $"Track: ${track}, section: {sectionNumber}, language: {bibleReadingSchedule.LanguageCode}, pub code: {bibleReadingSchedule.PublicationCode} not in lookup. ");
            throw new InvalidOperationException($"Track not found: {track}, section: {sectionNumber}");
        }

        var publicationCode = bibleReadingSchedule.PublicationCode;
        var languageCode = bibleReadingSchedule.LanguageCode;
        var url = trackDetail.Source?.Url ?? string.Empty;

        // LookUpPath is now computed from LanguageCode, PublicationCode, SectionNumber, TrackNumber
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            PublicationCode = publicationCode,
            LanguageCode = languageCode,
            SectionNumber = sectionNumber,
            TrackNumber = track,
            IsLastTrack = false
        };

        return new PlayItem(trackMetadata, url);
    }

    public async Task<List<PlayItem>> NextTracks(int scheduleId)
    {
        PlaylistScheduleManager.ValidateScheduleId(scheduleId);

        var schedule = await scheduleManager.LoadScheduleForTracks(scheduleId);
        var result = new List<PlayItem>();

        if (schedule.MusicEnabled)
        {
            result.Add(await musicTrackBuilder.NextMusicUrlToPlay(schedule));
        }

        var bibleReadingSchedule = schedule.BibleReadingSchedule ??
            throw new InvalidOperationException($"BibleReadingSchedule is null for schedule {scheduleId}");
        var bibleTracks = await bibleTrackBuilder.BuildBibleTracks(scheduleId, schedule, bibleReadingSchedule, 
            (lang, pub, section, track) => trackNavigator.GetNextBibleTrack(lang, pub, section, track));
        result.AddRange(bibleTracks);

        return result;
    }



    public async Task MoveToNextBibleTrack(int scheduleId)
    {
        var schedule = await scheduleUpdater.GetScheduleWithBibleReadingAsync(scheduleId);
        if (schedule.BibleReadingSchedule == null || !schedule.BibleReadingSchedule.SectionNumber.HasValue)
        {
            return;
        }
        var next = await trackNavigator.GetNextBibleTrack(
            schedule.BibleReadingSchedule.LanguageCode,
            schedule.BibleReadingSchedule.PublicationCode,
            schedule.BibleReadingSchedule.SectionNumber.Value,
            schedule.BibleReadingSchedule.TrackNumber);

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToNextTrackAsync(scheduleId, next);
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task MoveToPreviousBibleTrack(int scheduleId)
    {
        var schedule = await scheduleUpdater.GetScheduleWithBibleReadingAsync(scheduleId);
        if (schedule.BibleReadingSchedule == null || !schedule.BibleReadingSchedule.SectionNumber.HasValue)
        {
            return;
        }
        var previous = await trackNavigator.GetPreviousBibleTrack(
            schedule.BibleReadingSchedule.LanguageCode,
            schedule.BibleReadingSchedule.PublicationCode,
            schedule.BibleReadingSchedule.SectionNumber.Value,
            schedule.BibleReadingSchedule.TrackNumber);

        if (previous.Key == null || previous.Value == null)
        {
            throw new InvalidOperationException("Previous track Key or Value is null");
        }

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToPreviousTrackAsync(scheduleId, previous);
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task<KeyValuePair<BibleSection, BiblePublicationTrack>> GetNextBibleTrack(string languageCode,
        string publicationCode, int sectionNumber, int track)
    {
        return await trackNavigator.GetNextBibleTrack(languageCode, publicationCode, sectionNumber, track);
    }

    public async Task<KeyValuePair<BibleSection, BiblePublicationTrack>> GetPreviousBibleTrack(string languageCode,
        string publicationCode, int sectionNumber, int track)
    {
        return await trackNavigator.GetPreviousBibleTrack(languageCode, publicationCode, sectionNumber, track);
    }

    public async Task<KeyValuePair<int, BibleSection>> GetPreviousBibleSection(string languageCode, string publicationCode,
        int sectionNumber)
    {
        return await trackNavigator.GetPreviousBibleSection(languageCode, publicationCode, sectionNumber);
    }

    public async Task<KeyValuePair<int, BibleSection>> GetNextBibleSection(string languageCode, string publicationCode,
        int sectionNumber)
    {
        return await trackNavigator.GetNextBibleSection(languageCode, publicationCode, sectionNumber);
    }

    private async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        if (schedule.Music == null)
        {
            throw new InvalidOperationException($"Music is null for schedule {schedule.Id}");
        }

        return schedule.Music.MusicType switch
        {
            MusicType.Melodies => await GetNextMelodyTrackAsync(schedule, next),
            MusicType.Vocals => await GetNextVocalTrackAsync(schedule, next),
            _ => throw new ApplicationException("Invalid MusicType.")
        };
    }

    private async Task<PlayItem> GetNextMelodyTrackAsync(AlarmSchedule schedule, bool next)
    {
        var melodyMusic = schedule.Music;
        if (melodyMusic == null)
        {
            throw new InvalidOperationException("Schedule music is null");
        }
        var melodyTracks = await mediaService.GetMelodyMusicTracks(melodyMusic.PublicationCode);
        if (melodyTracks.Count == 0)
        {
            throw new InvalidOperationException($"No melody tracks found for publication {melodyMusic.PublicationCode}");
        }

        var melodyTrackIndex = CalculateTrackIndex(melodyMusic.TrackNumber, melodyTracks.Count, next);
        var melodyTrack = melodyTracks[melodyTrackIndex];
        ValidateTrackSource(melodyTrack.Source, melodyTrackIndex, "Melody");

        return CreateMelodyPlayItem(schedule, melodyMusic, melodyTrack);
    }

    private async Task<PlayItem> GetNextVocalTrackAsync(AlarmSchedule schedule, bool next)
    {
        var vocalMusic = schedule.Music;
        if (vocalMusic == null)
        {
            throw new InvalidOperationException("Schedule music is null");
        }
        var vocalTracks = await mediaService.GetVocalMusicTracks(vocalMusic.LanguageCode, vocalMusic.PublicationCode);
        if (vocalTracks.Count == 0)
        {
            throw new InvalidOperationException($"No vocal tracks found for language {vocalMusic.LanguageCode}, publication {vocalMusic.PublicationCode}");
        }

        var vocalTrackIndex = CalculateTrackIndex(vocalMusic.TrackNumber, vocalTracks.Count, next);
        var vocalTrack = vocalTracks[vocalTrackIndex];
        ValidateTrackSource(vocalTrack.Source, vocalTrackIndex, "Vocal");

        return CreateVocalPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private static int CalculateTrackIndex(int currentTrackNumber, int totalTracks, bool next)
    {
        var trackIndex = next ? currentTrackNumber % totalTracks + 1 : currentTrackNumber;
        if (trackIndex < 1 || trackIndex > totalTracks)
        {
            throw new InvalidOperationException($"Invalid track index {trackIndex} for {totalTracks} tracks");
        }
        return trackIndex;
    }

    private static void ValidateTrackSource(AudioSource? source, int trackIndex, string trackType)
    {
        if (source == null)
        {
            throw new InvalidOperationException($"{trackType} track {trackIndex} Source is null");
        }
    }

    private static PlayItem CreateMelodyPlayItem(AlarmSchedule schedule, AlarmMusic melodyMusic, MusicTrack melodyTrack)
    {
        if (melodyTrack.Source == null)
        {
            throw new InvalidOperationException($"Melody track {melodyTrack.Number} Source is null");
        }
        // LookUpPath is now computed from PublicationCode, LanguageCode (null for melody), and TrackNumber
        return new PlayItem(new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = melodyMusic.PublicationCode,
            TrackNumber = melodyTrack.Number
        }, melodyTrack.Source.Url);
    }

    private static PlayItem CreateVocalPlayItem(AlarmSchedule schedule, AlarmMusic vocalMusic, MusicTrack vocalTrack)
    {
        if (vocalTrack.Source == null)
        {
            throw new InvalidOperationException($"Vocal track {vocalTrack.Number} Source is null");
        }
        // LookUpPath is now computed from PublicationCode, LanguageCode, and TrackNumber
        return new PlayItem(new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = vocalMusic.PublicationCode,
            LanguageCode = vocalMusic.LanguageCode,
            TrackNumber = vocalTrack.Number
        }, vocalTrack.Source.Url);
    }

    public async Task<bool> ShouldResumeFromLastPositionAsync(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, false, cancellationTokenSource.Token);

        if (schedule == null)
        {
            return false;
        }

        // Resume is enabled when AlwaysPlayFromStart is false
        return !schedule.AlwaysPlayFromStart;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // mediaService (MediaService), IServiceScopeFactory, and IDispatcher are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
