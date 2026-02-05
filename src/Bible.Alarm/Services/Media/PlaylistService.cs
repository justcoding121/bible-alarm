#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Helpers;
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

        if (trackMetadata.PlayType == PlayType.Bible)
        {
            trackChangeDetector.SetLastKnownBibleTrack(
                (int)trackMetadata.ScheduleId,
                trackMetadata.SectionCode,
                trackMetadata.TrackNumber);
        }

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
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => PlaylistTrackUpdater.UpdateScheduleForPlayedTrackInternal(schedule, trackMetadata, nextTrackNumber),
            cancellationTokenSource.Token);
    }
    public async Task MarkTrackAsFinished(TrackMetadata trackMetadata)
    {
        // Get next track/track before updating
        var nextTrackInfo = await GetNextTrackInfoAsync(trackMetadata);

        // Update schedule using service
        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => UpdateScheduleForFinishedTrackInternal(schedule, trackMetadata, nextTrackInfo),
            cancellationTokenSource.Token);

        if (trackMetadata.PlayType == PlayType.Bible && nextTrackInfo.NextTrack != null)
        {
            trackChangeDetector.SetLastKnownBibleTrack(
                (int)trackMetadata.ScheduleId,
                nextTrackInfo.NextTrack.Value.Key?.SectionCode,
                nextTrackInfo.NextTrack.Value.Value.Number);
        }

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
                trackMetadata.SectionCode,
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
        if (schedule.MusicEnabled && schedule.Music != null)
        {
            return await musicTrackBuilder.NextMusicUrlToPlay(schedule);
        }

        var biblePublicationSchedule = schedule.BiblePublicationSchedule
            ?? throw new InvalidOperationException($"No playable content configured for schedule {scheduleId} (MusicEnabled={schedule.MusicEnabled}, BiblePublicationSchedule is null)");
        
        // Use the biblePublicationTrackBuilder which correctly handles both sectioned and non-sectioned publications
        var trackInfo = await biblePublicationTrackBuilder.GetInitialTrackInfo(biblePublicationSchedule);
        
        // Check if this is a no-language publication (e.g., instrumental music)
        var isNoLanguagePublication = await BiblePublicationService.IsNoLanguagePublicationAsync(biblePublicationSchedule.PublicationCode);
        var effectiveLanguageCode = isNoLanguagePublication ? "E" : (biblePublicationSchedule.LanguageCode ?? "E");
        
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            LanguageCode = effectiveLanguageCode,
            SectionCode = trackInfo.SectionCode,
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

        if (schedule.MusicEnabled && schedule.Music != null)
        {
            result.Add(await musicTrackBuilder.NextMusicUrlToPlay(schedule));
        }

        // Bible reading is optional for a schedule. If not configured, just return music tracks (if any).
        if (schedule.BiblePublicationSchedule != null)
        {
            var biblePublicationTracks = await biblePublicationTrackBuilder.BuildBiblePublicationTracks(
                scheduleId,
                schedule,
                schedule.BiblePublicationSchedule,
                (lang, pub, sectionCode, track) => trackNavigator.GetNextBiblePublicationTrack(lang, pub, sectionCode, track));
            result.AddRange(biblePublicationTracks);
        }

        return result;
    }



    public async Task MoveToNextBiblePublicationTrack(int scheduleId)
    {
        var schedule = await scheduleUpdater.GetScheduleWithBiblePublicationAsync(scheduleId);
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        var languageCode = schedule.BiblePublicationSchedule.LanguageCode ?? "E";
        var next = await trackNavigator.GetNextBiblePublicationTrack(
            languageCode,
            schedule.BiblePublicationSchedule.PublicationCode,
            schedule.BiblePublicationSchedule.SectionCode,
            schedule.BiblePublicationSchedule.TrackNumber);

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToNextTrackAsync(scheduleId, next);
        trackChangeDetector.SetLastKnownBibleTrack(
            scheduleId,
            next.Key?.SectionCode,
            next.Value.Number);
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task MoveToPreviousBiblePublicationTrack(int scheduleId)
    {
        var schedule = await scheduleUpdater.GetScheduleWithBiblePublicationAsync(scheduleId);
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        var languageCode = schedule.BiblePublicationSchedule.LanguageCode ?? "E";
        var previous = await trackNavigator.GetPreviousBiblePublicationTrack(
            languageCode,
            schedule.BiblePublicationSchedule.PublicationCode,
            schedule.BiblePublicationSchedule.SectionCode,
            schedule.BiblePublicationSchedule.TrackNumber);

        // For non-sectioned publications, Key (section) will be null
        if (previous.Value == null)
        {
            throw new InvalidOperationException("Previous track Value is null");
        }

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToPreviousTrackAsync(scheduleId, previous);
        trackChangeDetector.SetLastKnownBibleTrack(
            scheduleId,
            previous.Key?.SectionCode,
            previous.Value.Number);
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextBiblePublicationTrack(string languageCode,
        string publicationCode, string? sectionCode, int track)
    {
        return await trackNavigator.GetNextBiblePublicationTrack(languageCode, publicationCode, sectionCode, track);
    }

    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(string languageCode,
        string publicationCode, string? sectionCode, int track)
    {
        return await trackNavigator.GetPreviousBiblePublicationTrack(languageCode, publicationCode, sectionCode, track);
    }

    public async Task<KeyValuePair<string, BiblePublicationSection>> GetPreviousBiblePublicationSection(string languageCode, string publicationCode,
        string sectionCode)
    {
        return await trackNavigator.GetPreviousBiblePublicationSection(languageCode, publicationCode, sectionCode);
    }

    public async Task<KeyValuePair<string, BiblePublicationSection>> GetNextBiblePublicationSection(string languageCode, string publicationCode,
        string sectionCode)
    {
        return await trackNavigator.GetNextBiblePublicationSection(languageCode, publicationCode, sectionCode);
    }

    private async Task<PlayItem> NextMusicUrlToPlay(AlarmSchedule schedule, bool next = false)
    {
        if (schedule.Music == null)
        {
            throw new InvalidOperationException($"Music is null for schedule {schedule.Id}");
        }

        // Music type is inferred from LanguageCode: null/empty = melody (instrumental), otherwise = vocal
        var isMelodyMusic = string.IsNullOrEmpty(schedule.Music.LanguageCode);
        return isMelodyMusic
            ? await GetNextMusicTrackAsync(schedule, next)
            : await GetNextVocalMusicTrackAsync(schedule, next);
    }

    private async Task<PlayItem> GetNextMusicTrackAsync(AlarmSchedule schedule, bool next)
    {
        var music = schedule.Music;
        if (music == null)
        {
            throw new InvalidOperationException("Schedule music is null");
        }

        // IMPORTANT: Sectioned melody publications (e.g., "iam") have duplicate track numbers across discs.
        // Always select tracks from the schedule's selected disc (SectionCode) when sectioned.
        SortedDictionary<int, MusicTrack> musicTracks;
        if (PublicationTypeHelper.HasSectionStructure(music.PublicationCode))
        {
            if (string.IsNullOrWhiteSpace(music.SectionCode))
            {
                musicTracks = new SortedDictionary<int, MusicTrack>();
            }
            else
            {
                musicTracks = await mediaService.GetMelodyMusicTracksBySection(music.PublicationCode, music.SectionCode);
            }
        }
        else
        {
            musicTracks = await mediaService.GetMelodyMusicTracks(music.PublicationCode);
        }
        if (musicTracks.Count == 0)
        {
            throw new InvalidOperationException($"No music tracks found for publication {music.PublicationCode}");
        }

        var musicTrackKey = GetNextTrackKey(musicTracks, music.TrackNumber, next);
        var musicTrack = musicTracks[musicTrackKey];

        return await CreateMusicPlayItem(schedule, music, musicTrack);
    }

    private static int GetNextTrackKey(SortedDictionary<int, MusicTrack> tracks, int currentTrackNumber, bool next)
    {
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException("No tracks available");
        }

        // Keep current track if present; otherwise use first key.
        if (!next)
        {
            return tracks.ContainsKey(currentTrackNumber) ? currentTrackNumber : tracks.Keys.First();
        }

        // Advance to next available key (handles gaps). Wrap to first.
        var keys = tracks.Keys.ToList();
        var currentIndex = keys.IndexOf(currentTrackNumber);
        if (currentIndex < 0)
        {
            return keys[0];
        }
        return keys[(currentIndex + 1) % keys.Count];
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

    public async Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata)
    {
        ArgumentNullException.ThrowIfNull(currentTrackMetadata);

        if (currentTrackMetadata.PlayType == PlayType.Music)
        {
            if (currentTrackMetadata.ScheduleId <= 0)
            {
                throw new InvalidOperationException("Invalid schedule ID in current track metadata");
            }

            var scheduleId = (int)currentTrackMetadata.ScheduleId;
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: true,
                includeBiblePublication: false,
                cancellationTokenSource.Token) ?? throw new InvalidOperationException($"Schedule not found: {scheduleId}");

            if (schedule.Music == null)
            {
                throw new InvalidOperationException($"Schedule {scheduleId} has no music configured");
            }

            // Ensure the builder advances relative to the current track.
            schedule.Music.TrackNumber = currentTrackMetadata.TrackNumber;

            return await musicTrackBuilder.NextMusicUrlToPlay(schedule, next: true);
        }

        // Bible content
        var next = await trackNavigator.GetNextBiblePublicationTrack(
            currentTrackMetadata.LanguageCode,
            currentTrackMetadata.PublicationCode,
            currentTrackMetadata.SectionCode,
            currentTrackMetadata.TrackNumber);

        if (next.Value == null)
        {
            throw new InvalidOperationException("Next track Value is null");
        }

        var nextTrackNumber = next.Value.Number;

        var metadata = new TrackMetadata
        {
            ScheduleId = currentTrackMetadata.ScheduleId,
            IsBibleContent = true,
            LanguageCode = currentTrackMetadata.LanguageCode,
            PublicationCode = currentTrackMetadata.PublicationCode,
            SectionCode = next.Key?.SectionCode,
            TrackNumber = nextTrackNumber,
            IsLastTrack = false
        };

        // Disc-style melody publications (e.g. "iam" with section codes like "iam-1") are stored as sectioned Bible content,
        // but metadata/title lookup should treat the section code as the download/disc code.
        TryApplyDiscStyleDownloadCode(metadata);

        var url = await urlRefreshService.RefreshUrlAsync(metadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException("Failed to refresh URL for next play item");
        }

        return new PlayItem(metadata, url);
    }

    public async Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata)
    {
        ArgumentNullException.ThrowIfNull(currentTrackMetadata);

        if (currentTrackMetadata.PlayType == PlayType.Music)
        {
            if (currentTrackMetadata.ScheduleId <= 0)
            {
                throw new InvalidOperationException("Invalid schedule ID in current track metadata");
            }

            var scheduleId = (int)currentTrackMetadata.ScheduleId;
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: true,
                includeBiblePublication: false,
                cancellationTokenSource.Token) ?? throw new InvalidOperationException($"Schedule not found: {scheduleId}");

            if (schedule.Music == null)
            {
                throw new InvalidOperationException($"Schedule {scheduleId} has no music configured");
            }

            schedule.Music.TrackNumber = currentTrackMetadata.TrackNumber;
            return await musicTrackBuilder.PreviousMusicUrlToPlay(schedule);
        }

        // Bible content
        var previous = await trackNavigator.GetPreviousBiblePublicationTrack(
            currentTrackMetadata.LanguageCode,
            currentTrackMetadata.PublicationCode,
            currentTrackMetadata.SectionCode,
            currentTrackMetadata.TrackNumber);

        if (previous.Value == null)
        {
            throw new InvalidOperationException("Previous track Value is null");
        }

        var prevTrackNumber = previous.Value.Number;

        var metadata = new TrackMetadata
        {
            ScheduleId = currentTrackMetadata.ScheduleId,
            IsBibleContent = true,
            LanguageCode = currentTrackMetadata.LanguageCode,
            PublicationCode = currentTrackMetadata.PublicationCode,
            SectionCode = previous.Key?.SectionCode,
            TrackNumber = prevTrackNumber,
            IsLastTrack = false
        };

        // Disc-style melody publications (e.g. "iam" with section codes like "iam-1") are stored as sectioned Bible content,
        // but metadata/title lookup should treat the section code as the download/disc code.
        TryApplyDiscStyleDownloadCode(metadata);

        var url = await urlRefreshService.RefreshUrlAsync(metadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException("Failed to refresh URL for previous play item");
        }

        return new PlayItem(metadata, url);
    }

    private static void TryApplyDiscStyleDownloadCode(TrackMetadata metadata)
    {
        // Example: publicationCode="iam", sectionCode="iam-1"
        var sectionCode = SectionCodeHelper.Normalize(metadata.SectionCode);
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return;
        }

        if (!sectionCode.Contains('-'))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.PublicationCode) ||
            !sectionCode.StartsWith(metadata.PublicationCode + "-", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Validate suffix is a non-zero digit sequence.
        var parts = sectionCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return;
        }

        var suffix = parts[^1];
        if (string.IsNullOrEmpty(suffix) || !suffix.All(char.IsDigit) || suffix.All(c => c == '0'))
        {
            return;
        }

        metadata.DownloadCode = sectionCode;
        metadata.OriginalTrackNumber = metadata.TrackNumber;
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
