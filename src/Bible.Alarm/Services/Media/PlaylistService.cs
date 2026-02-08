#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IUrlConstructionService urlConstructionService;
    private readonly ILanguageContentService? languageContentService;
    private readonly IServiceScopeFactory? scopeFactory;
    private readonly IScheduleDisplayNameService? scheduleDisplayNameService;

    public PlaylistService(
        ILogger logger,
        IMediaService mediaService,
        IDispatcher dispatcher,
        IAlarmScheduleService alarmScheduleService,
        IGeneralSettingsService generalSettingsService,
        IBiblePublicationService BiblePublicationService,
        IMelodyMusicService melodyMusicService,
        IMediaUrlRefreshService urlRefreshService,
        IUrlConstructionService urlConstructionService,
        ILanguageContentService? languageContentService = null,
        IServiceScopeFactory? scopeFactory = null,
        IScheduleDisplayNameService? scheduleDisplayNameService = null)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.dispatcher = dispatcher;
        this.alarmScheduleService = alarmScheduleService;
        this.generalSettingsService = generalSettingsService;
        this.BiblePublicationService = BiblePublicationService;
        this.melodyMusicService = melodyMusicService;
        this.urlRefreshService = urlRefreshService;
        this.urlConstructionService = urlConstructionService ?? throw new ArgumentNullException(nameof(urlConstructionService));
        this.languageContentService = languageContentService;
        this.scopeFactory = scopeFactory;
        this.scheduleDisplayNameService = scheduleDisplayNameService;
        biblePublicationTrackBuilder = new PlaylistBiblePublicationTrackBuilder(logger, mediaService, urlRefreshService, urlConstructionService, BiblePublicationService);
        musicTrackBuilder = new PlaylistMusicTrackBuilder(logger, mediaService, melodyMusicService, urlRefreshService, urlConstructionService);
        trackChangeDetector = new TrackChangeDetector(alarmScheduleService, cancellationTokenSource.Token);
        trackNavigator = new TrackNavigator(mediaService, BiblePublicationService, languageContentService, scopeFactory, logger);
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
        var nextTrackCode = await GetNextTrackCodeIfNeeded(trackMetadata);

        var updatedSchedule = await UpdateScheduleForPlayedTrack(
            trackMetadata,
            nextTrackCode);

        if (trackMetadata.PlayType == PlayType.Bible)
        {
            trackChangeDetector.SetLastKnownBibleTrack(
                (int)trackMetadata.ScheduleId,
                trackMetadata.SectionCode,
                trackMetadata.TrackCode);
        }

        if (trackChanged)
        {
            dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        }
    }

    private async Task<string?> GetNextTrackCodeIfNeeded(TrackMetadata trackMetadata)
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
        return next.Metadata.TrackCode;
    }

    private async Task<AlarmSchedule> UpdateScheduleForPlayedTrack(
        TrackMetadata trackMetadata,
        string? nextTrackCode)
    {
        return await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => PlaylistTrackUpdater.UpdateScheduleForPlayedTrackInternal(schedule, trackMetadata, nextTrackCode),
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
                Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(nextTrackInfo.NextTrack.Value.Value));
        }

        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    private async Task<NextTrackInfo> GetNextTrackInfoAsync(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            var nextTrackCode = await GetNextMusicTrackCodeAsync(trackMetadata);
            return new NextTrackInfo(nextTrackCode, null);
        }
        else
        {
            var nextTrack = await trackNavigator.GetNextBiblePublicationTrack(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                trackMetadata.SectionCode,
                trackMetadata.TrackCode);
            return new NextTrackInfo(null, nextTrack);
        }
    }

    private async Task<string?> GetNextMusicTrackCodeAsync(TrackMetadata trackMetadata)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);
        if (schedule?.Music != null && !schedule.Music.Repeat)
        {
            var next = await musicTrackBuilder.NextMusicUrlToPlay(schedule, true);
            return next.Metadata.TrackCode;
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
            PlaylistTrackUpdater.UpdateMusicTrackForFinished(schedule, nextTrackInfo.NextTrackCode);
        }
        else
        {
            PlaylistTrackUpdater.UpdateBiblePublicationTrackForFinished(schedule, trackMetadata, nextTrackInfo.NextTrack);
        }
    }
    private record NextTrackInfo(string? NextTrackCode, KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>? NextTrack);

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
        
        var trackCode = TrackCodeHelper.GetFromTrack(trackInfo.Track);
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = scheduleId,
            IsBibleContent = true,
            PublicationCode = biblePublicationSchedule.PublicationCode,
            LanguageCode = effectiveLanguageCode,
            SectionCode = trackInfo.SectionCode,
            TrackCode = trackCode,
            IsLastTrack = false
        };

        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            biblePublicationSchedule.PublicationCode,
            effectiveLanguageCode,
            trackInfo.SectionCode,
            trackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={effectiveLanguageCode}, section={trackInfo.SectionCode ?? "(none)"}, track={trackInfo.Track.TrackCode}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        // So alarm modal shows full track title for disc-style melody (e.g. iam): DisplayMetadataService needs DownloadCode/OriginalTrackCode.
        TryApplyDiscStyleDownloadCode(trackMetadata);

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
            var bible = schedule.BiblePublicationSchedule;
            logger.Debug("[PlaylistBuild] Schedule {ScheduleId} Bible from DB: PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}",
                scheduleId, bible.PublicationCode, bible.SectionCode ?? "(null)", bible.TrackCode);

            var biblePublicationTracks = await biblePublicationTrackBuilder.BuildBiblePublicationTracks(
                scheduleId,
                schedule,
                schedule.BiblePublicationSchedule,
                (lang, pub, sectionCode, track) => trackNavigator.GetNextBiblePublicationTrack(lang, pub, sectionCode, track));
            result.AddRange(biblePublicationTracks);
        }

        if (result.Count > 0 && result[0].Metadata is { } firstMeta)
        {
            logger.Information("[Playback] First play item: ScheduleId={ScheduleId}, PublicationCode={PublicationCode}, SectionCode={SectionCode}, TrackCode={TrackCode}, LookUpPath={LookUpPath}",
                firstMeta.ScheduleId, firstMeta.PublicationCode, firstMeta.SectionCode ?? "(null)", firstMeta.TrackCode, firstMeta.LookUpPath);
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

        var languageCode = schedule.BiblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
        var next = await trackNavigator.GetNextBiblePublicationTrack(
            languageCode,
            schedule.BiblePublicationSchedule.PublicationCode,
            schedule.BiblePublicationSchedule.SectionCode,
            schedule.BiblePublicationSchedule.TrackCode);

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToNextTrackAsync(scheduleId, next);
        trackChangeDetector.SetLastKnownBibleTrack(
            scheduleId,
            next.Key?.SectionCode,
            TrackCodeHelper.GetFromTrack(next.Value));
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task MoveToPreviousBiblePublicationTrack(int scheduleId)
    {
        var schedule = await scheduleUpdater.GetScheduleWithBiblePublicationAsync(scheduleId);
        if (schedule.BiblePublicationSchedule == null)
        {
            return;
        }

        var languageCode = schedule.BiblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
        var previous = await trackNavigator.GetPreviousBiblePublicationTrack(
            languageCode,
            schedule.BiblePublicationSchedule.PublicationCode,
            schedule.BiblePublicationSchedule.SectionCode,
            schedule.BiblePublicationSchedule.TrackCode);

        // For non-sectioned publications, Key (section) will be null
        if (previous.Value == null)
        {
            throw new InvalidOperationException("Previous track Value is null");
        }

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToPreviousTrackAsync(scheduleId, previous);
        trackChangeDetector.SetLastKnownBibleTrack(
            scheduleId,
            previous.Key?.SectionCode,
            TrackCodeHelper.GetFromTrack(previous.Value));
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetNextBiblePublicationTrack(string languageCode,
        string publicationCode, string? sectionCode, string trackCode)
    {
        return await trackNavigator.GetNextBiblePublicationTrack(languageCode, publicationCode, sectionCode, trackCode);
    }

    public async Task<KeyValuePair<BiblePublicationSection?, BiblePublicationTrack>> GetPreviousBiblePublicationTrack(string languageCode,
        string publicationCode, string? sectionCode, string trackCode)
    {
        return await trackNavigator.GetPreviousBiblePublicationTrack(languageCode, publicationCode, sectionCode, trackCode);
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

        var musicTrackKey = GetNextTrackKey(musicTracks, music.TrackCode, next);
        var musicTrack = musicTracks[musicTrackKey];

        return await CreateMusicPlayItem(schedule, music, musicTrack);
    }

    private static int GetNextTrackKey(SortedDictionary<int, MusicTrack> tracks, string? currentTrackCode, bool next)
    {
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException("No tracks available");
        }

        if (!int.TryParse(currentTrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var currentTrackNum))
        {
            return tracks.Keys.First();
        }

        // Keep current track if present; otherwise use first key.
        if (!next)
        {
            return tracks.ContainsKey(currentTrackNum) ? currentTrackNum : tracks.Keys.First();
        }

        // Advance to next available key (handles gaps). Wrap to first.
        var keys = tracks.Keys.ToList();
        var currentIndex = keys.IndexOf(currentTrackNum);
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

        var vocalTrackIndex = CalculateTrackIndex(vocalMusic.TrackCode, vocalTracks.Count, next);
        var vocalTrack = vocalTracks[vocalTrackIndex];

        return await CreateVocalMusicPlayItem(schedule, vocalMusic, vocalTrack);
    }

    private static int CalculateTrackIndex(string? currentTrackCode, int totalTracks, bool next)
    {
        if (!int.TryParse(currentTrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var currentTrackNum))
        {
            return next ? 1 : 1;
        }

        var trackIndex = next ? currentTrackNum % totalTracks + 1 : currentTrackNum;
        if (trackIndex < 1 || trackIndex > totalTracks)
        {
            throw new InvalidOperationException($"Invalid track index {trackIndex} for {totalTracks} tracks");
        }
        return trackIndex;
    }

    private async Task<PlayItem> CreateMusicPlayItem(AlarmSchedule schedule, AlarmMusic music, MusicTrack musicTrack)
    {
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = music.PublicationCode,
            TrackCode = musicTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DownloadCode = musicTrack.DownloadCode,
            OriginalTrackCode = musicTrack.OriginalTrackCode
        };

        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            music.PublicationCode,
            null, // Melody has no language
            musicTrack.DownloadCode, // Section/disc code (e.g. "iam-1")
            musicTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={music.PublicationCode}, section={musicTrack.DownloadCode}, track={musicTrack.Number}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(trackMetadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException($"Failed to get URL for music track {musicTrack.Number}");
        }

        return new PlayItem(trackMetadata, url);
    }

    private async Task<PlayItem> CreateVocalMusicPlayItem(AlarmSchedule schedule, AlarmMusic vocalMusic, MusicTrack vocalTrack)
    {
        var trackMetadata = new TrackMetadata
        {
            ScheduleId = schedule.Id,
            PublicationCode = vocalMusic.PublicationCode,
            LanguageCode = vocalMusic.LanguageCode ?? string.Empty,
            TrackCode = vocalTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DownloadCode = vocalTrack.DownloadCode,
            OriginalTrackCode = vocalTrack.OriginalTrackCode
        };

        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            vocalMusic.PublicationCode,
            vocalMusic.LanguageCode,
            null, // No section for vocal music
            vocalTrack.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={vocalMusic.PublicationCode}, lang={vocalMusic.LanguageCode}, track={vocalTrack.Number}. Only harvested tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

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
            schedule.Music.TrackCode = currentTrackMetadata.TrackCode;

            return await musicTrackBuilder.NextMusicUrlToPlay(schedule, next: true);
        }

        // Bible content
        var next = await trackNavigator.GetNextBiblePublicationTrack(
            currentTrackMetadata.LanguageCode,
            currentTrackMetadata.PublicationCode,
            currentTrackMetadata.SectionCode,
            currentTrackMetadata.TrackCode);

        if (next.Value == null)
        {
            throw new InvalidOperationException("Next track Value is null");
        }

        var nextTrackCode = TrackCodeHelper.GetFromTrack(next.Value);
        var nextSectionCode = next.Key?.SectionCode;

        // Check if section changed - if so, refresh schedule display names
        var sectionChanged = !string.Equals(currentTrackMetadata.SectionCode, nextSectionCode, StringComparison.OrdinalIgnoreCase);
        if (sectionChanged && currentTrackMetadata.ScheduleId > 0 && scheduleDisplayNameService != null)
        {
            await RefreshScheduleDisplayNamesAsync((int)currentTrackMetadata.ScheduleId, currentTrackMetadata.LanguageCode, currentTrackMetadata.PublicationCode, nextSectionCode);
        }

        var metadata = new TrackMetadata
        {
            ScheduleId = currentTrackMetadata.ScheduleId,
            IsBibleContent = true,
            LanguageCode = currentTrackMetadata.LanguageCode,
            PublicationCode = currentTrackMetadata.PublicationCode,
            SectionCode = nextSectionCode,
            TrackCode = nextTrackCode,
            IsLastTrack = false
        };
        TryApplyDiscStyleDownloadCode(metadata);

        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            metadata.PublicationCode,
            metadata.LanguageCode,
            metadata.SectionCode,
            metadata.TrackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={metadata.PublicationCode}, lang={metadata.LanguageCode}, section={metadata.SectionCode ?? "(none)"}, track={metadata.TrackCode}. Only harvested tracks can be played.");
        }
        metadata.LookUpPath = lookUpPath;

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

            schedule.Music.TrackCode = currentTrackMetadata.TrackCode;
            return await musicTrackBuilder.PreviousMusicUrlToPlay(schedule);
        }

        // Bible content
        var previous = await trackNavigator.GetPreviousBiblePublicationTrack(
            currentTrackMetadata.LanguageCode,
            currentTrackMetadata.PublicationCode,
            currentTrackMetadata.SectionCode,
            currentTrackMetadata.TrackCode);

        if (previous.Value == null)
        {
            throw new InvalidOperationException("Previous track Value is null");
        }

        var prevTrackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(previous.Value);
        var prevSectionCode = previous.Key?.SectionCode;

        // Check if section changed - if so, refresh schedule display names
        var sectionChanged = !string.Equals(currentTrackMetadata.SectionCode, prevSectionCode, StringComparison.OrdinalIgnoreCase);
        if (sectionChanged && currentTrackMetadata.ScheduleId > 0 && scheduleDisplayNameService != null)
        {
            await RefreshScheduleDisplayNamesAsync((int)currentTrackMetadata.ScheduleId, currentTrackMetadata.LanguageCode, currentTrackMetadata.PublicationCode, prevSectionCode);
        }

        var metadata = new TrackMetadata
        {
            ScheduleId = currentTrackMetadata.ScheduleId,
            IsBibleContent = true,
            LanguageCode = currentTrackMetadata.LanguageCode,
            PublicationCode = currentTrackMetadata.PublicationCode,
            SectionCode = prevSectionCode,
            TrackCode = prevTrackCode,
            IsLastTrack = false
        };
        TryApplyDiscStyleDownloadCode(metadata);

        var lookUpPath = await urlConstructionService.ConstructTrackLookUpPathAsync(
            metadata.PublicationCode,
            metadata.LanguageCode,
            metadata.SectionCode,
            metadata.TrackCode);
        if (string.IsNullOrEmpty(lookUpPath))
        {
            throw new InvalidOperationException(
                $"Track not found in media index: pub={metadata.PublicationCode}, lang={metadata.LanguageCode}, section={metadata.SectionCode ?? "(none)"}, track={metadata.TrackCode}. Only harvested tracks can be played.");
        }
        metadata.LookUpPath = lookUpPath;

        var url = await urlRefreshService.RefreshUrlAsync(metadata);
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException("Failed to refresh URL for previous play item");
        }

        return new PlayItem(metadata, url);
    }

    /// <summary>
    /// Refreshes schedule display names after navigating to a newly harvested section.
    /// Creates a temporary schedule entity with the new section code to populate display names.
    /// </summary>
    private async Task RefreshScheduleDisplayNamesAsync(int scheduleId, string languageCode, string publicationCode, string? sectionCode)
    {
        if (scheduleDisplayNameService == null || alarmScheduleService == null)
        {
            return;
        }

        try
        {
            // Get the schedule from database
            var schedule = await alarmScheduleService.GetScheduleByIdAsync(
                scheduleId,
                includeMusic: false,
                includeBiblePublication: true,
                cancellationTokenSource.Token);

            if (schedule?.BiblePublicationSchedule == null)
            {
                return;
            }

            // Create a temporary schedule entity with the new section code for display name population
            // We don't update the database schedule here - that happens when the track is played
            var tempSchedule = new AlarmSchedule
            {
                Id = schedule.Id,
                Name = schedule.Name,
                IsEnabled = schedule.IsEnabled,
                Hour = schedule.Hour,
                Minute = schedule.Minute,
                Second = schedule.Second,
                DaysOfWeek = schedule.DaysOfWeek,
                NotificationEnabled = schedule.NotificationEnabled,
                MusicEnabled = schedule.MusicEnabled,
                SnoozeMinutes = schedule.SnoozeMinutes,
                NumberOfTracksToPlay = schedule.NumberOfTracksToPlay,
                AlwaysPlayFromStart = schedule.AlwaysPlayFromStart,
                BiblePublicationSchedule = new BiblePublicationSchedule
                {
                    LanguageCode = schedule.BiblePublicationSchedule.LanguageCode ?? languageCode,
                    PublicationCode = schedule.BiblePublicationSchedule.PublicationCode ?? publicationCode,
                    SectionCode = sectionCode,
                    TrackCode = schedule.BiblePublicationSchedule.TrackCode
                }
            };

            // Create a temporary ScheduleStateItem to populate display names
            var scheduleStateItem = new ScheduleStateItem
            {
                Id = schedule.Id,
                BiblePublicationLanguageCode = tempSchedule.BiblePublicationSchedule.LanguageCode,
                BiblePublicationCode = tempSchedule.BiblePublicationSchedule.PublicationCode,
                BiblePublicationSectionCode = tempSchedule.BiblePublicationSchedule.SectionCode,
                BiblePublicationTrackCode = tempSchedule.BiblePublicationSchedule.TrackCode
            };

            // Populate display names from database (this will read the newly harvested section name)
            await scheduleDisplayNameService.PopulateDisplayNamesAsync(scheduleStateItem, tempSchedule);

            // Dispatch update to refresh schedule state with new display names (without saving to DB)
            dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(scheduleStateItem, false, false, shouldSave: false));

            logger.Debug("Refreshed schedule display names after section harvest: ScheduleId={ScheduleId}, SectionCode={SectionCode}, SectionName={SectionName}",
                scheduleId, sectionCode, scheduleStateItem.BiblePublicationSectionName);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to refresh schedule display names after section harvest: ScheduleId={ScheduleId}, SectionCode={SectionCode}",
                scheduleId, sectionCode);
        }
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
        if (int.TryParse(metadata.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedTrackNum))
        {
            metadata.OriginalTrackCode = parsedTrackNum;
        }
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
