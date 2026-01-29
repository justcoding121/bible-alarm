#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
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
    private readonly PlaylistBiblePublicationTrackBuilder biblePublicationTrackBuilder;
    private readonly PlaylistMusicTrackBuilder musicTrackBuilder;

    // Helper classes
    private readonly TrackChangeDetector trackChangeDetector;
    private readonly TrackNavigator trackNavigator;
    private readonly ScheduleUpdater scheduleUpdater;

    private readonly IMediaUrlRefreshService urlRefreshService;

    public PlaylistService(
        ILogger logger,
        IMediaService mediaService,
        IDispatcher dispatcher,
        IAlarmScheduleService alarmScheduleService,
        IGeneralSettingsService generalSettingsService,
        IBiblePublicationService BiblePublicationService,
        IMelodyMusicService melodyMusicService,
        IMediaUrlRefreshService urlRefreshService,
        IDiskCacheService? diskCacheService,
        IUrlConstructionService? urlConstructionService = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.dispatcher = dispatcher;
        this.alarmScheduleService = alarmScheduleService;
        this.generalSettingsService = generalSettingsService;
        this.BiblePublicationService = BiblePublicationService;
        this.melodyMusicService = melodyMusicService;
        this.urlRefreshService = urlRefreshService;
        this.diskCacheService = diskCacheService;
        biblePublicationTrackBuilder = new PlaylistBiblePublicationTrackBuilder(logger, mediaService, urlRefreshService, BiblePublicationService, urlConstructionService);
        musicTrackBuilder = new PlaylistMusicTrackBuilder(logger, mediaService, melodyMusicService, urlRefreshService, urlConstructionService);
        trackChangeDetector = new TrackChangeDetector(alarmScheduleService, cancellationTokenSource.Token);
        trackNavigator = new TrackNavigator(mediaService, BiblePublicationService);
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

        const string CacheKey = "ScheduleList";
        diskCacheService?.Remove(CacheKey);

        // Update schedule using service
        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => UpdateScheduleForFinishedTrackInternal(schedule, trackMetadata, nextTrackInfo),
            cancellationTokenSource.Token);

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
            var nextTrack = await trackNavigator.GetNextBiblePublicationTrack(
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
            PlaylistTrackUpdater.UpdateBiblePublicationTrackForFinished(schedule, trackMetadata, nextTrackInfo.NextTrack);
        }
    }
    private record NextTrackInfo(int? NextTrackNumber, KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>? NextTrack);

    public async Task<PlayItem> NextTrack(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, true, true, cancellationTokenSource.Token) ?? throw new ArgumentException($"Invalid schedule Id {scheduleId}");
        if (schedule.MusicEnabled)
        {
            return await musicTrackBuilder.NextMusicUrlToPlay(schedule);
        }

        var biblePublicationSchedule = schedule.BiblePublicationSchedule ?? throw new InvalidOperationException($"BiblePublicationSchedule is null for schedule {scheduleId}");
        
        // Use the biblePublicationTrackBuilder which correctly handles both sectioned and non-sectioned publications
        var trackInfo = await biblePublicationTrackBuilder.GetInitialTrackInfo(biblePublicationSchedule);
        
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            LanguageCode = biblePublicationSchedule.LanguageCode,
            SectionNumber = trackInfo.SectionNumber,
            TrackNumber = trackInfo.Track.Number,
            IsLastTrack = false
        };

        // Note: LookUpPath will be set by GetInitialTrackInfo if UrlConstructionService is available
        // The URL is already constructed in GetInitialTrackInfo, so we use trackInfo.Url directly
        return new PlayItem(trackMetadata, trackInfo.Url);
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

        var biblePublicationSchedule = schedule.BiblePublicationSchedule ??
            throw new InvalidOperationException($"BiblePublicationSchedule is null for schedule {scheduleId}");
        var biblePublicationTracks = await biblePublicationTrackBuilder.BuildBiblePublicationTracks(scheduleId, schedule, biblePublicationSchedule,
            (lang, pub, section, track) => trackNavigator.GetNextBiblePublicationTrack(lang, pub, section, track));
        result.AddRange(biblePublicationTracks);

        return result;
    }



    public async Task MoveToNextBiblePublicationTrack(int scheduleId)
    {
        var schedule = await scheduleUpdater.GetScheduleWithBiblePublicationAsync(scheduleId);
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        // Convert SectionCode to int for track navigator
        var sectionNumber = await ConvertSectionCodeToIntAsync(
            schedule.BiblePublicationSchedule.SectionCode,
            schedule.BiblePublicationSchedule.LanguageCode,
            schedule.BiblePublicationSchedule.PublicationCode);

        var next = await trackNavigator.GetNextBiblePublicationTrack(
            schedule.BiblePublicationSchedule.LanguageCode,
            schedule.BiblePublicationSchedule.PublicationCode,
            sectionNumber,
            schedule.BiblePublicationSchedule.TrackNumber);

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToNextTrackAsync(scheduleId, next);
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task MoveToPreviousBiblePublicationTrack(int scheduleId)
    {
        var schedule = await scheduleUpdater.GetScheduleWithBiblePublicationAsync(scheduleId);
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        // Convert SectionCode to int for track navigator
        var sectionNumber = await ConvertSectionCodeToIntAsync(
            schedule.BiblePublicationSchedule.SectionCode,
            schedule.BiblePublicationSchedule.LanguageCode,
            schedule.BiblePublicationSchedule.PublicationCode);

        var previous = await trackNavigator.GetPreviousBiblePublicationTrack(
            schedule.BiblePublicationSchedule.LanguageCode,
            schedule.BiblePublicationSchedule.PublicationCode,
            sectionNumber,
            schedule.BiblePublicationSchedule.TrackNumber);

        // For non-sectioned publications, Key (section) will be null
        if (previous.Value == null)
        {
            throw new InvalidOperationException("Previous track Value is null");
        }

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToPreviousTrackAsync(scheduleId, previous);
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextBiblePublicationTrack(string languageCode,
        string publicationCode, int sectionNumber, int track)
    {
        return await trackNavigator.GetNextBiblePublicationTrack(languageCode, publicationCode, sectionNumber, track);
    }

    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(string languageCode,
        string publicationCode, int sectionNumber, int track)
    {
        return await trackNavigator.GetPreviousBiblePublicationTrack(languageCode, publicationCode, sectionNumber, track);
    }

    public async Task<KeyValuePair<int, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode, string publicationCode,
        int sectionNumber)
    {
        return await trackNavigator.GetPreviousBiblePublicationSection(languageCode, publicationCode, sectionNumber);
    }

    public async Task<KeyValuePair<int, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode,
        int sectionNumber)
    {
        return await trackNavigator.GetNextBiblePublicationSection(languageCode, publicationCode, sectionNumber);
    }

    private async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        if (schedule.Music == null)
        {
            throw new InvalidOperationException($"Music is null for schedule {schedule.Id}");
        }

        return schedule.Music.MusicType switch
        {
            MusicType.Music => await GetNextMusicTrackAsync(schedule, next),
            MusicType.VocalMusic => await GetNextVocalMusicTrackAsync(schedule, next),
            _ => throw new ApplicationException("Invalid MusicType.")
        };
    }

    private async Task<PlayItem> GetNextMusicTrackAsync(AlarmSchedule schedule, bool next)
    {
        var music = schedule.Music;
        if (music == null)
        {
            throw new InvalidOperationException("Schedule music is null");
        }
        var musicTracks = await mediaService.GetMelodyMusicTracks(music.PublicationCode);
        if (musicTracks.Count == 0)
        {
            throw new InvalidOperationException($"No music tracks found for publication {music.PublicationCode}");
        }

        var musicTrackIndex = CalculateTrackIndex(music.TrackNumber, musicTracks.Count, next);
        var musicTrack = musicTracks[musicTrackIndex];

        return await CreateMusicPlayItem(schedule, music, musicTrack);
    }

    private async Task<PlayItem> GetNextVocalMusicTrackAsync(AlarmSchedule schedule, bool next)
    {
        var vocalMusic = schedule.Music;
        if (vocalMusic == null)
        {
            throw new InvalidOperationException("Schedule music is null");
        }
        if (vocalMusic.LanguageCode == null)
        {
            throw new InvalidOperationException("LanguageCode is null for vocal music");
        }
        var vocalTracks = await mediaService.GetVocalMusicTracks(vocalMusic.LanguageCode, vocalMusic.PublicationCode);
        if (vocalTracks.Count == 0)
        {
            throw new InvalidOperationException($"No vocal tracks found for language {vocalMusic.LanguageCode}, publication {vocalMusic.PublicationCode}");
        }

        var vocalTrackIndex = CalculateTrackIndex(vocalMusic.TrackNumber, vocalTracks.Count, next);
        var vocalTrack = vocalTracks[vocalTrackIndex];

        return await CreateVocalMusicPlayItem(schedule, vocalMusic, vocalTrack);
    }

    /// <summary>
    /// Converts SectionCode (string) to the int section number needed for media service calls.
    /// Tries to parse SectionCode to int.
    /// Returns 0 for null/empty (non-sectioned publications).
    /// </summary>
    private async Task<int> ConvertSectionCodeToIntAsync(string? sectionCode, string languageCode, string publicationCode)
    {
        if (string.IsNullOrEmpty(sectionCode))
        {
            return 0;
        }

        // Try to parse SectionCode directly to int
        if (int.TryParse(sectionCode, out var sectionNumber))
        {
            return sectionNumber;
        }

        // If parsing fails, SectionCode is not numeric (e.g., "gen" for Genesis)
        // For non-numeric section codes, return 0
        return 0;
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

    private async Task<PlayItem> CreateMusicPlayItem(AlarmSchedule schedule, AlarmMusic music, MusicTrack musicTrack)
    {
        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = music.PublicationCode,
            TrackNumber = musicTrack.Number,
            DownloadCode = musicTrack.DownloadCode, // Store disc code (e.g., "iam-1", "iam-2") for music
            OriginalTrackNumber = musicTrack.OriginalTrackNumber // Store original track number from API (within the disc)
            // LanguageCode is empty for music without language
        };

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for music track {musicTrack.Number}");
        }

        return new PlayItem(trackMetadata, url);
    }

    private async Task<PlayItem> CreateVocalMusicPlayItem(AlarmSchedule schedule, AlarmMusic vocalMusic, MusicTrack vocalTrack)
    {
        // Compute URL on-demand using TrackMetadata
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = vocalMusic.PublicationCode,
            LanguageCode = vocalMusic.LanguageCode ?? string.Empty,
            TrackNumber = vocalTrack.Number,
            DownloadCode = vocalTrack.DownloadCode, // Typically same as publication code, but store for consistency
            OriginalTrackNumber = vocalTrack.OriginalTrackNumber // Typically same as Number for vocal music
        };

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for vocal track {vocalTrack.Number}");
        }

        return new PlayItem(trackMetadata, url);
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
