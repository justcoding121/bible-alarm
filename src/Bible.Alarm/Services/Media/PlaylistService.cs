#nullable enable
using System.Linq;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Services.Media.PlaylistInternal;
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
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
namespace Bible.Alarm.Services.Media;

public sealed class PlaylistService : IPlaylistService
{
    private readonly ILogger logger;
    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> applicationState;
    private readonly IAlarmScheduleService alarmScheduleService;
    private readonly IGeneralSettingsService generalSettingsService;
    private readonly IBiblePublicationService BiblePublicationService;
    private readonly IMelodyMusicService melodyMusicService;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;
    private PlaylistScheduleManager? _scheduleManager;
    private PlaylistScheduleManager scheduleManager => _scheduleManager ??= new PlaylistScheduleManager(
        alarmScheduleService,
        generalSettingsService,
        cancellationTokenSource.Token);
    private readonly PlaylistBiblePublicationTrackBuilder biblePublicationTrackBuilder;
    private readonly PlaylistMusicTrackBuilder musicTrackBuilder;

    // Helper classes
    private readonly TrackChangeDetector trackChangeDetector;
    private readonly TrackNavigator trackNavigator;
    private readonly ScheduleUpdater scheduleUpdater;
    private readonly PlaylistScheduleDisplayRefresher? scheduleDisplayRefresher;
    private readonly PlaylistBiblePlayItemBuilder biblePlayItemBuilder;

    private readonly IUrlConstructionService urlConstructionService;

    public PlaylistService(
        ILogger logger,
        IMediaService mediaService,
        IDispatcher dispatcher,
        IState<ApplicationState> applicationState,
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
        this.dispatcher = dispatcher;
        this.applicationState = applicationState;
        this.alarmScheduleService = alarmScheduleService;
        this.generalSettingsService = generalSettingsService;
        this.BiblePublicationService = BiblePublicationService;
        this.melodyMusicService = melodyMusicService;
        this.urlConstructionService = urlConstructionService ?? throw new ArgumentNullException(nameof(urlConstructionService));
        biblePublicationTrackBuilder = new PlaylistBiblePublicationTrackBuilder(logger, mediaService, urlRefreshService, urlConstructionService, BiblePublicationService);
        musicTrackBuilder = new PlaylistMusicTrackBuilder(logger, mediaService, melodyMusicService, urlRefreshService, urlConstructionService);
        trackChangeDetector = new TrackChangeDetector(alarmScheduleService, cancellationTokenSource.Token);
        trackNavigator = new TrackNavigator(mediaService, BiblePublicationService, logger, languageContentService, scopeFactory);
        scheduleUpdater = new ScheduleUpdater(alarmScheduleService, cancellationTokenSource.Token);
        scheduleDisplayRefresher = scheduleDisplayNameService != null && alarmScheduleService != null
            ? new PlaylistScheduleDisplayRefresher(alarmScheduleService, scheduleDisplayNameService, applicationState, dispatcher, logger)
            : null;
        biblePlayItemBuilder = new PlaylistBiblePlayItemBuilder(urlConstructionService, urlRefreshService);
    }

    public async Task<int> GetRelevantScheduleToPlay()
    {
        return await scheduleManager.GetRelevantScheduleToPlay();
    }

    public async Task SaveLastPlayed(int currentScheduleId)
    {
        await scheduleManager.SaveLastPlayed(currentScheduleId);
    }

    public async Task MarkTrackAsPlayed(TrackMetadata trackMetadata)
    {
        var trackChanged = await trackChangeDetector.CheckIfTrackChanged(trackMetadata);
        var (nextTrackCode, nextSectionCode) = await GetNextTrackCodeAndSectionIfNeededAsync(trackMetadata);

        var updatedSchedule = await UpdateScheduleForPlayedTrack(
            trackMetadata,
            nextTrackCode,
            nextSectionCode);

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

    private async Task<(string? TrackCode, string? SectionCode)> GetNextTrackCodeAndSectionIfNeededAsync(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType != PlayType.Music)
        {
            return (null, null);
        }

        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);

        if (schedule?.Music == null || schedule.Music.Repeat)
        {
            return (null, null);
        }

        schedule.Music.TrackCode = trackMetadata.TrackCode;
        if (!string.IsNullOrWhiteSpace(trackMetadata.DownloadCode))
        {
            schedule.Music.SectionCode = trackMetadata.DownloadCode;
        }
        var next = await musicTrackBuilder.NextMusicUrlToPlay(schedule, true);
        var nextSectionCode = !string.IsNullOrWhiteSpace(next.Metadata.DownloadCode) ? next.Metadata.DownloadCode : null;
        return (next.Metadata.TrackCode, nextSectionCode);
    }

    /// <summary>
    /// Bible schedule language code: use DB when set; when DB has null (e.g. no-language pub iam), use the value
    /// already in state so we do not overwrite it. Language is only updated when the user clicks Save on the schedule page.
    /// </summary>
    private string GetPreservedLanguageForNoLanguageBibleSchedule(int scheduleId, string? dbLanguageCode)
    {
        if (!string.IsNullOrWhiteSpace(dbLanguageCode))
            return dbLanguageCode;
        var state = applicationState.Value;
        if (state.CurrentSchedule?.Id == scheduleId && !string.IsNullOrWhiteSpace(state.CurrentSchedule.BiblePublicationLanguageCode))
            return state.CurrentSchedule.BiblePublicationLanguageCode;
        var fromList = state.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        return !string.IsNullOrWhiteSpace(fromList?.BiblePublicationLanguageCode)
            ? fromList.BiblePublicationLanguageCode
            : AppConstants.Media.DefaultLanguageCode;
    }

    /// <summary>
    /// Music schedule language code: use DB when set; when DB has null (e.g. no-language pub), use the value
    /// already in state so we do not overwrite it. Language is only updated when the user clicks Save on the schedule page.
    /// </summary>
    private string GetPreservedLanguageForNoLanguageMusicSchedule(int scheduleId, string? dbLanguageCode)
    {
        if (!string.IsNullOrWhiteSpace(dbLanguageCode))
            return dbLanguageCode;
        var state = applicationState.Value;
        if (state.CurrentSchedule?.Id == scheduleId && !string.IsNullOrWhiteSpace(state.CurrentSchedule.MusicLanguageCode))
            return state.CurrentSchedule.MusicLanguageCode;
        var fromList = state.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        return !string.IsNullOrWhiteSpace(fromList?.MusicLanguageCode)
            ? fromList.MusicLanguageCode
            : AppConstants.Media.DefaultLanguageCode;
    }

    private async Task<AlarmSchedule> UpdateScheduleForPlayedTrack(
        TrackMetadata trackMetadata,
        string? nextTrackCode,
        string? nextSectionCode = null)
    {
        var scheduleToUse = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);
        var effectiveMetadata = trackMetadata;
        if (scheduleToUse?.BiblePublicationSchedule != null
            && trackMetadata.PlayType == PlayType.Bible
            && await BiblePublicationService.IsNoLanguagePublicationAsync(scheduleToUse.BiblePublicationSchedule.PublicationCode))
        {
            var preservedLang = GetPreservedLanguageForNoLanguageBibleSchedule((int)trackMetadata.ScheduleId, scheduleToUse.BiblePublicationSchedule.LanguageCode);
            effectiveMetadata = CloneTrackMetadataWithLanguage(trackMetadata, preservedLang);
        }
        else if (scheduleToUse?.Music != null
            && trackMetadata.PlayType == PlayType.Music
            && await BiblePublicationService.IsNoLanguagePublicationAsync(scheduleToUse.Music.PublicationCode))
        {
            var preservedLang = GetPreservedLanguageForNoLanguageMusicSchedule((int)trackMetadata.ScheduleId, scheduleToUse.Music.LanguageCode);
            effectiveMetadata = CloneTrackMetadataWithLanguage(trackMetadata, preservedLang);
        }

        return await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => PlaylistTrackUpdater.UpdateScheduleForPlayedTrackInternal(schedule, effectiveMetadata, nextTrackCode, nextSectionCode),
            cancellationTokenSource.Token);
    }
    public async Task MarkTrackAsFinished(TrackMetadata trackMetadata)
    {
        // Get next track/track before updating
        var nextTrackInfo = await GetNextTrackInfoAsync(trackMetadata);

        var scheduleToUse = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);
        var effectiveMetadata = trackMetadata;
        if (scheduleToUse?.BiblePublicationSchedule != null
            && trackMetadata.PlayType == PlayType.Bible
            && await BiblePublicationService.IsNoLanguagePublicationAsync(scheduleToUse.BiblePublicationSchedule.PublicationCode))
        {
            var preservedLang = GetPreservedLanguageForNoLanguageBibleSchedule((int)trackMetadata.ScheduleId, scheduleToUse.BiblePublicationSchedule.LanguageCode);
            effectiveMetadata = CloneTrackMetadataWithLanguage(trackMetadata, preservedLang);
        }
        else if (scheduleToUse?.Music != null
            && trackMetadata.PlayType == PlayType.Music
            && await BiblePublicationService.IsNoLanguagePublicationAsync(scheduleToUse.Music.PublicationCode))
        {
            var preservedLang = GetPreservedLanguageForNoLanguageMusicSchedule((int)trackMetadata.ScheduleId, scheduleToUse.Music.LanguageCode);
            effectiveMetadata = CloneTrackMetadataWithLanguage(trackMetadata, preservedLang);
        }

        // Update schedule using service
        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule => UpdateScheduleForFinishedTrackInternal(schedule, effectiveMetadata, nextTrackInfo),
            cancellationTokenSource.Token);

        if (trackMetadata.PlayType == PlayType.Bible && nextTrackInfo.NextTrack != null)
        {
            trackChangeDetector.SetLastKnownBibleTrack(
                (int)trackMetadata.ScheduleId,
                nextTrackInfo.NextTrack.Section?.SectionCode,
                Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(nextTrackInfo.NextTrack.Track));
        }

        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task PersistSchedulePointerToFinishedTrackAsync(TrackMetadata trackMetadata)
    {
        if (trackMetadata.ScheduleId <= 0)
        {
            return;
        }

        var scheduleToUse = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);
        var effectiveMetadata = trackMetadata;
        if (scheduleToUse?.BiblePublicationSchedule != null
            && trackMetadata.PlayType == PlayType.Bible
            && await BiblePublicationService.IsNoLanguagePublicationAsync(scheduleToUse.BiblePublicationSchedule.PublicationCode))
        {
            var preservedLang = GetPreservedLanguageForNoLanguageBibleSchedule((int)trackMetadata.ScheduleId, scheduleToUse.BiblePublicationSchedule.LanguageCode);
            effectiveMetadata = CloneTrackMetadataWithLanguage(trackMetadata, preservedLang);
        }
        else if (scheduleToUse?.Music != null
            && trackMetadata.PlayType == PlayType.Music
            && await BiblePublicationService.IsNoLanguagePublicationAsync(scheduleToUse.Music.PublicationCode))
        {
            var preservedLang = GetPreservedLanguageForNoLanguageMusicSchedule((int)trackMetadata.ScheduleId, scheduleToUse.Music.LanguageCode);
            effectiveMetadata = CloneTrackMetadataWithLanguage(trackMetadata, preservedLang);
        }

        var updatedSchedule = await alarmScheduleService.UpdateScheduleByIdAsync(
            (int)trackMetadata.ScheduleId,
            schedule =>
            {
                if (trackMetadata.PlayType == PlayType.Music && schedule.Music != null)
                {
                    schedule.Music.TrackCode = effectiveMetadata.TrackCode;
                    if (!string.IsNullOrWhiteSpace(effectiveMetadata.DownloadCode))
                    {
                        schedule.Music.SectionCode = effectiveMetadata.DownloadCode;
                    }
                }
                else if (trackMetadata.PlayType == PlayType.Bible && schedule.BiblePublicationSchedule != null)
                {
                    PlaylistTrackUpdater.UpdateBiblePublicationTrack(schedule, effectiveMetadata);
                }
            },
            cancellationTokenSource.Token);

        if (trackMetadata.PlayType == PlayType.Bible)
        {
            trackChangeDetector.SetLastKnownBibleTrack(
                (int)trackMetadata.ScheduleId,
                effectiveMetadata.SectionCode,
                effectiveMetadata.TrackCode);
        }

        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
        logger.Information(
            "Persisted schedule pointer to finished track after failed indefinite transition: ScheduleId={ScheduleId}, PlayType={PlayType}",
            trackMetadata.ScheduleId,
            trackMetadata.PlayType);
    }

    private async Task<NextTrackInfo> GetNextTrackInfoAsync(TrackMetadata trackMetadata)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            var (nextTrackCode, nextSectionCode) = await GetNextMusicTrackCodeAndSectionAsync(trackMetadata);
            return new NextTrackInfo(nextTrackCode, null, nextSectionCode);
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

    private async Task<(string? TrackCode, string? SectionCode)> GetNextMusicTrackCodeAndSectionAsync(TrackMetadata trackMetadata)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            (int)trackMetadata.ScheduleId, true, false, cancellationTokenSource.Token);
        if (schedule?.Music != null && !schedule.Music.Repeat)
        {
            if (!string.IsNullOrWhiteSpace(trackMetadata.DownloadCode))
            {
                schedule.Music.SectionCode = trackMetadata.DownloadCode;
            }
            var next = await musicTrackBuilder.NextMusicUrlToPlay(schedule, true);
            var nextSectionCode = !string.IsNullOrWhiteSpace(next.Metadata.DownloadCode) ? next.Metadata.DownloadCode : null;
            return (next.Metadata.TrackCode, nextSectionCode);
        }
        return (null, null);
    }

    private static TrackMetadata CloneTrackMetadataWithLanguage(TrackMetadata source, string languageCode)
    {
        var copy = new TrackMetadata
        {
            ScheduleId = source.ScheduleId,
            NotificationTime = source.NotificationTime,
            IsBibleContent = source.IsBibleContent,
            LanguageCode = languageCode,
            PublicationCode = source.PublicationCode,
            NaturalKey = source.NaturalKey,
            DownloadCode = source.DownloadCode,
            OriginalTrackCode = source.OriginalTrackCode,
            SectionCode = source.SectionCode,
            TrackCode = source.TrackCode,
            FinishedDuration = source.FinishedDuration,
            IsLastTrack = source.IsLastTrack,
        };
        return copy;
    }

    private static void UpdateScheduleForFinishedTrackInternal(
        AlarmSchedule schedule,
        TrackMetadata trackMetadata,
        NextTrackInfo nextTrackInfo)
    {
        if (trackMetadata.PlayType == PlayType.Music)
        {
            PlaylistTrackUpdater.UpdateMusicTrackForFinished(schedule, nextTrackInfo.NextTrackCode, nextTrackInfo.NextSectionCode);
        }
        else
        {
            PlaylistTrackUpdater.UpdateBiblePublicationTrackForFinished(schedule, trackMetadata, nextTrackInfo.NextTrack);
        }
    }

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

        return await BuildBiblePublicationPlayItemAsync(scheduleId, biblePublicationSchedule);
    }

    public async Task<PlayItem?> NextBiblePublicationTrack(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, true, true, cancellationTokenSource.Token) ?? throw new ArgumentException($"Invalid schedule Id {scheduleId}");

        if (schedule.BiblePublicationSchedule == null)
        {
            return null;
        }

        return await BuildBiblePublicationPlayItemAsync(scheduleId, schedule.BiblePublicationSchedule);
    }

    private async Task<PlayItem> BuildBiblePublicationPlayItemAsync(
        int scheduleId,
        BiblePublicationSchedule biblePublicationSchedule)
    {
        var trackInfo = await biblePublicationTrackBuilder.GetInitialTrackInfo(biblePublicationSchedule);

        var isNoLanguagePublication = await BiblePublicationService.IsNoLanguagePublicationAsync(biblePublicationSchedule.PublicationCode);
        string effectiveLanguageCode;
        if (isNoLanguagePublication)
        {
            effectiveLanguageCode = AppConstants.Media.DefaultLanguageCode;
        }
        else
        {
            effectiveLanguageCode = biblePublicationSchedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
        }

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
                $"Track not found in media index: pub={biblePublicationSchedule.PublicationCode}, lang={effectiveLanguageCode}, section={trackInfo.SectionCode ?? "(none)"}, track={trackInfo.Track.TrackCode}. Only cataloged tracks can be played.");
        }
        trackMetadata.LookUpPath = lookUpPath;

        PlaylistMetadataHelper.TryApplyDiscStyleDownloadCode(trackMetadata);

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
                async (lang, pub, sectionCode, track) => await trackNavigator.GetNextBiblePublicationTrack(lang, pub, sectionCode, track));
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
            next.Section?.SectionCode,
            TrackCodeHelper.GetFromTrack(next.Track));
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

        var updatedSchedule = await scheduleUpdater.UpdateScheduleToPreviousTrackAsync(scheduleId, previous);
        trackChangeDetector.SetLastKnownBibleTrack(
            scheduleId,
            previous.Section?.SectionCode,
            TrackCodeHelper.GetFromTrack(previous.Track));
        dispatcher.Dispatch(new UpdateScheduleAction(updatedSchedule));
    }

    public async Task<TrackNavigationResult> GetNextBiblePublicationTrack(string languageCode,
        string publicationCode, string? sectionCode, string trackCode)
    {
        return await trackNavigator.GetNextBiblePublicationTrack(languageCode, publicationCode, sectionCode, trackCode);
    }

    public async Task<TrackNavigationResult> GetPreviousBiblePublicationTrack(string languageCode,
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

    public async Task<TimeSpan> GetScheduleFinishedDurationAsync(int scheduleId)
    {
        var schedule = await alarmScheduleService.GetScheduleByIdAsync(
            scheduleId, false, true, cancellationTokenSource.Token);

        return schedule?.BiblePublicationSchedule?.FinishedDuration ?? TimeSpan.Zero;
    }

    public async Task<PlayItem> GetNextPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null)
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
            if (!string.IsNullOrWhiteSpace(currentTrackMetadata.DownloadCode))
            {
                schedule.Music.SectionCode = currentTrackMetadata.DownloadCode;
            }

            return await musicTrackBuilder.NextMusicUrlToPlay(schedule, next: true);
        }

        // Bible content
        var next = await trackNavigator.GetNextBiblePublicationTrack(
            currentTrackMetadata.LanguageCode,
            currentTrackMetadata.PublicationCode,
            currentTrackMetadata.SectionCode,
            currentTrackMetadata.TrackCode,
            sectionFetchProgress);

        var nextTrackCode = TrackCodeHelper.GetFromTrack(next.Track);
        var nextSectionCode = next.Section?.SectionCode;
        var nextPublicationCode = next.PublicationCode;

        var sectionChanged = !string.Equals(currentTrackMetadata.SectionCode, nextSectionCode, StringComparison.OrdinalIgnoreCase);
        if (sectionChanged && currentTrackMetadata.ScheduleId > 0 && scheduleDisplayRefresher != null)
        {
            await scheduleDisplayRefresher.RefreshAsync((int)currentTrackMetadata.ScheduleId, currentTrackMetadata.LanguageCode, nextPublicationCode, nextSectionCode, cancellationTokenSource.Token);
        }

        return await biblePlayItemBuilder.BuildPlayItemAsync(
            currentTrackMetadata.ScheduleId,
            currentTrackMetadata.LanguageCode,
            nextPublicationCode,
            nextSectionCode,
            nextTrackCode);
    }

    public async Task<PlayItem> GetPreviousPlayItemAsync(TrackMetadata currentTrackMetadata, IFetchProgress? sectionFetchProgress = null)
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
            if (!string.IsNullOrWhiteSpace(currentTrackMetadata.DownloadCode))
            {
                schedule.Music.SectionCode = currentTrackMetadata.DownloadCode;
            }
            return await musicTrackBuilder.PreviousMusicUrlToPlay(schedule);
        }

        // Bible content
        var previous = await trackNavigator.GetPreviousBiblePublicationTrack(
            currentTrackMetadata.LanguageCode,
            currentTrackMetadata.PublicationCode,
            currentTrackMetadata.SectionCode,
            currentTrackMetadata.TrackCode,
            sectionFetchProgress);

        var prevTrackCode = TrackCodeHelper.GetFromTrack(previous.Track);
        var prevSectionCode = previous.Section?.SectionCode;
        var prevPublicationCode = previous.PublicationCode;

        var sectionChanged = !string.Equals(currentTrackMetadata.SectionCode, prevSectionCode, StringComparison.OrdinalIgnoreCase);
        if (sectionChanged && currentTrackMetadata.ScheduleId > 0 && scheduleDisplayRefresher != null)
        {
            await scheduleDisplayRefresher.RefreshAsync((int)currentTrackMetadata.ScheduleId, currentTrackMetadata.LanguageCode, prevPublicationCode, prevSectionCode, cancellationTokenSource.Token);
        }

        return await biblePlayItemBuilder.BuildPlayItemAsync(
            currentTrackMetadata.ScheduleId,
            currentTrackMetadata.LanguageCode,
            prevPublicationCode,
            prevSectionCode,
            prevTrackCode);
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
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            logger.Warning(ex, AppConstants.Logging.DisposableLifetimeLog.ErrorDuringCancellationTokenSourceDisposal);
        }

        // Note: DbContext instances are now created via IServiceScopeFactory and disposed by the scope
        // mediaService (MediaService), IServiceScopeFactory, and IDispatcher are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
