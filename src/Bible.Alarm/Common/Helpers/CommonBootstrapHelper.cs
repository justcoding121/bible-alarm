#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if ANDROID
using Bible.Alarm.Platforms.Android.Effects;
#endif

namespace Bible.Alarm.Common.Helpers;

public static class CommonBootstrapHelper
{
    private static readonly SemaphoreSlim @lock = new(1);
    private static volatile bool servicesVerified;

    public static async Task VerifyServices(bool initializeUi = false)
    {
        Log.Logger.Information("VerifyServices called with initializeUI={InitializeUI}, _servicesVerified={ServicesVerified}",
            initializeUi, servicesVerified);

        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (servicesVerified)
            {
                Log.Logger.Information("Services already verified, skipping database operations");
            }
            else
            {
                Log.Logger.Information("Starting database and IO operations");
                // Run database and IO operations off UI thread
                await Task.Run(async () =>
                {
                    var task1 = VerifyMediaLookUpService();
                    var task2 = InitializeDatabase();
                    var task3 = InitializeFluxorStore();
                    var task4 = CopySilentMp3ToStorage();

                    await Task.WhenAll(task1, task2, task3, task4);

                    // After database and Fluxor store are initialized, load schedules into state
                    // This ensures schedules are available for both Android Auto services and main UI
                    await InitializeSchedules();
                });
                servicesVerified = true;
                Log.Logger.Information("Database and IO operations completed");
            }
        });

        // Send InitializedMessage after lock is released
        // NavigateToHomeAsync handles duplicate navigation attempts internally
        // CRITICAL: Send InitializedMessage even if services were already verified
        // This handles the case where Android Auto completed bootstrap first (isForeground=false)
        // and the UI needs to navigate away from BootstrapPage
        if (initializeUi)
        {
            Log.Logger.Information("Sending InitializedMessage to trigger navigation (services verified: {ServicesVerified})", servicesVerified);

            // If services were already verified (bootstrap completed by Android Auto), add a small delay
            // to ensure MessageHandlingService.RegisterMessageHandlers() has been called in App.xaml.cs
            if (servicesVerified)
            {
                // Small delay to ensure message handlers are registered before sending message
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(100); // 100ms delay to ensure handlers are registered
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                WeakReferenceMessenger.Default.Send(new InitializedMessage());
                                Log.Logger.Information("InitializedMessage sent (delayed for handler registration)");
                            }
                            catch (Exception ex)
                            {
                                Log.Logger.Error(ex, "Error sending InitializedMessage (delayed)");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error in delayed InitializedMessage task");
                    }
                });
            }
            else
            {
                // Services just verified, send message immediately
                try
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            WeakReferenceMessenger.Default.Send(new InitializedMessage());
                            Log.Logger.Information("InitializedMessage sent");
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Error(ex, "Error sending InitializedMessage");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Error invoking MainThread for InitializedMessage");
                }
            }
        }
        else
        {
            Log.Logger.Information("initializeUI=false, not sending InitializedMessage");
        }
    }

    private static async Task VerifyMediaLookUpService()
    {
        var service = ServiceProviderManager.GetService<IMediaIndexService>();
        await service.Verify();
    }

    private static async Task InitializeDatabase()
    {
        // Create a scope for the DbContext since it's registered as scoped
        // This ensures proper lifetime management and prevents disposal issues
        var scopeFactory = ServiceProviderManager.GetService<IServiceScopeFactory>();
        await using var scope = scopeFactory.CreateAsyncScope();

        // Migrate Schedule database (always safe - app owns this DB)
        var scheduleDb = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();
        await scheduleDb.Database.MigrateAsync();

        // Migrate Media database if it exists and is from a previous app version
        // Note: App is packaged with latest media index database, so this primarily
        // handles users upgrading from previous app versions
        var mediaMigrationService = ServiceProviderManager.GetService<IMediaMigrationService>();
        await mediaMigrationService.MigrateIfNeededAsync();
    }

    private static async Task InitializeFluxorStore()
    {
        Log.Logger.Information("Initializing Fluxor store");

        // Get the store from service provider
        var store = ServiceProviderManager.GetService<IStore>();
        if (store == null)
        {
            Log.Logger.Warning("IStore service not found - Fluxor store initialization skipped");
            return;
        }

        // Initialize store asynchronously
        await store.InitializeAsync();

        // Set the static store reference
        ReduxContainer.Store = store;

#if ANDROID
        // Android Auto can start the process without constructing the MAUI App UI (App.xaml.cs),
        // so register MediaSessionEffect message handlers here to ensure progress updates flow to MediaSession.
        // (Metadata/status/navigation are handled via Fluxor effects, but position comes from MVVM messages.)
        try
        {
            var mediaSessionEffect = ServiceProviderManager.GetService<MediaSessionEffect>();
            mediaSessionEffect?.RegisterMessageHandlers();
            Log.Logger.Debug("MediaSessionEffect message handlers registered (bootstrap)");
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to register MediaSessionEffect message handlers (bootstrap)");
        }
#endif

        Log.Logger.Information("Fluxor store initialized successfully");
    }

    private static async Task InitializeSchedules()
    {
        Log.Logger.Information("Initializing schedules in state");

        try
        {
            var services = GetRequiredServices();
            if (services == null)
            {
                return;
            }

            await SeedAndMigrateSchedules(services);
            var alarmSchedules = await LoadSchedulesFromDatabase(services.AlarmScheduleService);
            var languagesDict = await LoadLanguagesDictionary(services.BibleTranslationService);
            var initialSchedules = await PopulateScheduleStateItems(
                alarmSchedules,
                services,
                languagesDict);

            await DispatchInitializeAction(services.Dispatcher, initialSchedules);

#if ANDROID
            // Dispatch SetCarPlayScreenAction to fetch and set default schedule metadata for Android Auto
            // This will trigger the effect to fetch metadata and update MediaSession
            services.Dispatcher.Dispatch(new SetCarPlayScreenAction());
            Log.Logger.Debug("SetCarPlayScreenAction dispatched after bootstrap completion");
#endif
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error initializing schedules in bootstrap");
        }
    }

    private record BootstrapServices(
        IDatabaseSeedService DatabaseSeedService,
        IScheduleMigrationService ScheduleMigrationService,
        IAlarmScheduleService AlarmScheduleService,
        IDispatcher Dispatcher,
        IBibleTranslationService BibleTranslationService,
        IBibleBookService BibleBookService,
        IMapper Mapper,
        IMediaService MediaService,
        IMelodyMusicService MelodyMusicService);

    private static BootstrapServices? GetRequiredServices()
    {
        var databaseSeedService = ServiceProviderManager.GetService<IDatabaseSeedService>();
        var scheduleMigrationService = ServiceProviderManager.GetService<IScheduleMigrationService>();
        var alarmScheduleService = ServiceProviderManager.GetService<IAlarmScheduleService>();
        var dispatcher = ServiceProviderManager.GetService<IDispatcher>();
        var bibleTranslationService = ServiceProviderManager.GetService<IBibleTranslationService>();
        var bibleBookService = ServiceProviderManager.GetService<IBibleBookService>();
        var mapper = ServiceProviderManager.GetService<IMapper>();
        var mediaService = ServiceProviderManager.GetService<IMediaService>();
        var melodyMusicService = ServiceProviderManager.GetService<IMelodyMusicService>();

        if (databaseSeedService == null || scheduleMigrationService == null ||
            alarmScheduleService == null || dispatcher == null)
        {
            Log.Logger.Warning("Required services not available for schedule initialization - skipping");
            return null;
        }

        return new BootstrapServices(
            databaseSeedService,
            scheduleMigrationService,
            alarmScheduleService,
            dispatcher,
            bibleTranslationService!,
            bibleBookService!,
            mapper!,
            mediaService!,
            melodyMusicService!);
    }

    private static async Task SeedAndMigrateSchedules(BootstrapServices services)
    {
        await services.DatabaseSeedService.SeedDefaultAlarmAsync();
        await services.ScheduleMigrationService.MigrateBibleGatewaySchedulesAsync();
    }

    private static async Task<List<AlarmSchedule>> LoadSchedulesFromDatabase(IAlarmScheduleService alarmScheduleService)
    {
        var alarmSchedules = await alarmScheduleService.GetAllSchedulesAsync(
            includeMusic: true,
            includeBibleReading: true);

        Log.Logger.Information("Loaded {Count} schedules from database during bootstrap", alarmSchedules.Count);
        return alarmSchedules;
    }

    private static async Task<Dictionary<string, Language>?> LoadLanguagesDictionary(IBibleTranslationService? bibleTranslationService)
    {
        if (bibleTranslationService == null)
        {
            return null;
        }

        try
        {
            var languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
            Log.Logger.Information("Loaded {Count} languages for translation name lookup", languagesDict.Count);
            return languagesDict;
        }
        catch (Exception langEx)
        {
            Log.Logger.Warning(langEx, "Error loading languages - translation names will not be populated");
            return null;
        }
    }

    private static async Task<ObservableHashSet<ScheduleStateItem>> PopulateScheduleStateItems(
        List<AlarmSchedule> alarmSchedules,
        BootstrapServices services,
        Dictionary<string, Language>? languagesDict)
    {
        var initialSchedules = new ObservableHashSet<ScheduleStateItem>();

        foreach (var schedule in alarmSchedules)
        {
            var scheduleStateItem = services.Mapper.Map<ScheduleStateItem>(schedule);

            await PopulateBibleReadingDisplayNames(
                schedule,
                scheduleStateItem,
                services,
                languagesDict);

            await PopulateMusicDisplayNames(
                schedule,
                scheduleStateItem,
                services);

            await PopulateDefaultMusicIfNeeded(
                schedule,
                scheduleStateItem,
                services);

            initialSchedules.Add(scheduleStateItem);
        }

        return initialSchedules;
    }

    private static async Task PopulateBibleReadingDisplayNames(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BootstrapServices services,
        Dictionary<string, Language>? languagesDict)
    {
        if (schedule.BibleReadingSchedule == null)
        {
            return;
        }

        var bibleReading = schedule.BibleReadingSchedule;
        SetBibleReadingLanguageName(schedule, scheduleStateItem, bibleReading, languagesDict);
        await SetBibleReadingPublicationName(schedule, scheduleStateItem, bibleReading, services.BibleTranslationService);
        await SetBibleReadingBookName(schedule, scheduleStateItem, bibleReading, services.BibleBookService);
    }

    private static void SetBibleReadingLanguageName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BibleReadingSchedule bibleReading,
        Dictionary<string, Language>? languagesDict)
    {
        if (languagesDict == null)
        {
            return;
        }

        var languageCode = bibleReading.LanguageCode;
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return;
        }

        if (languagesDict.TryGetValue(languageCode, out var language))
        {
            scheduleStateItem.BibleReadingLanguageName = language.Name;
            Log.Logger.Debug("Set BibleReadingLanguageName '{BibleReadingLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                language.Name, schedule.Id, languageCode);
        }
        else
        {
            scheduleStateItem.BibleReadingLanguageName = languageCode;
            Log.Logger.Debug("Language not found for LanguageCode '{LanguageCode}', using code as BibleReadingLanguageName for schedule {ScheduleId}",
                languageCode, schedule.Id);
        }
    }

    private static async Task SetBibleReadingPublicationName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BibleReadingSchedule bibleReading,
        IBibleTranslationService? bibleTranslationService)
    {
        if (bibleTranslationService == null ||
            string.IsNullOrWhiteSpace(bibleReading.LanguageCode) ||
            string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
        {
            return;
        }

        try
        {
            var translation = await bibleTranslationService.GetByLanguageAndCodeWithBooksAsync(
                bibleReading.LanguageCode,
                bibleReading.PublicationCode);

            if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
            {
                scheduleStateItem.BibleReadingPublicationName = translation.Name;
                Log.Logger.Debug("Set BibleReadingPublicationName '{BibleReadingPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    translation.Name, schedule.Id, bibleReading.PublicationCode);
            }
        }
        catch (Exception pubEx)
        {
            Log.Logger.Warning(pubEx, "Error loading publication name for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                schedule.Id, bibleReading.PublicationCode);
        }
    }

    private static async Task SetBibleReadingBookName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BibleReadingSchedule bibleReading,
        IBibleBookService? bibleBookService)
    {
        if (bibleBookService == null || bibleReading.BookNumber <= 0)
        {
            return;
        }

        try
        {
            var bookName = await bibleBookService.GetBookNameAsync(
                bibleReading.LanguageCode,
                bibleReading.PublicationCode,
                bibleReading.BookNumber);

            if (!string.IsNullOrWhiteSpace(bookName))
            {
                scheduleStateItem.BibleReadingBookName = bookName;
                Log.Logger.Debug("Set BibleReadingBookName '{BibleReadingBookName}' for schedule {ScheduleId} (BookNumber: {BookNumber})",
                    bookName, schedule.Id, bibleReading.BookNumber);
            }
        }
        catch (Exception bookEx)
        {
            Log.Logger.Warning(bookEx, "Error loading book name for schedule {ScheduleId} (BookNumber: {BookNumber})",
                schedule.Id, bibleReading.BookNumber);
        }
    }

    private static async Task PopulateMusicDisplayNames(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BootstrapServices services)
    {
        if (schedule.Music == null)
        {
            return;
        }

        var music = schedule.Music;
        await SetMusicLanguageName(schedule, scheduleStateItem, music, services.MediaService);
        await SetMusicPublicationName(schedule, scheduleStateItem, music, services.MediaService);
        await SetMusicTrackName(schedule, scheduleStateItem, music, services.MediaService);
    }

    private static async Task SetMusicLanguageName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        AlarmMusic music,
        IMediaService? mediaService)
    {
        if (music.MusicType != Shared.Models.Enums.MusicType.Vocals ||
            string.IsNullOrWhiteSpace(music.LanguageCode) ||
            mediaService == null)
        {
            return;
        }

        try
        {
            var vocalLanguagesDict = await mediaService.GetVocalMusicLanguages();
            if (vocalLanguagesDict.TryGetValue(music.LanguageCode, out var vocalLanguage))
            {
                scheduleStateItem.MusicLanguageName = vocalLanguage.Name;
                Log.Logger.Debug("Set MusicLanguageName '{MusicLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                    vocalLanguage.Name, schedule.Id, music.LanguageCode);
            }
            else
            {
                scheduleStateItem.MusicLanguageName = music.LanguageCode;
                Log.Logger.Debug("Language not found for LanguageCode '{LanguageCode}', using code as MusicLanguageName for schedule {ScheduleId}",
                    music.LanguageCode, schedule.Id);
            }
        }
        catch (Exception langEx)
        {
            Log.Logger.Warning(langEx, "Error loading music language name for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                schedule.Id, music.LanguageCode);
        }
    }

    private static async Task SetMusicPublicationName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        AlarmMusic music,
        IMediaService? mediaService)
    {
        if (music.MusicType != Shared.Models.Enums.MusicType.Vocals ||
            string.IsNullOrWhiteSpace(music.LanguageCode) ||
            string.IsNullOrWhiteSpace(music.PublicationCode) ||
            mediaService == null)
        {
            return;
        }

        try
        {
            var releases = await mediaService.GetVocalMusicReleases(music.LanguageCode);
            if (releases.TryGetValue(music.PublicationCode, out var release))
            {
                scheduleStateItem.MusicPublicationName = release.Name;
                Log.Logger.Debug("Set MusicPublicationName '{MusicPublicationName}' for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                    release.Name, schedule.Id, music.PublicationCode);
            }
        }
        catch (Exception pubEx)
        {
            Log.Logger.Warning(pubEx, "Error loading music publication name for schedule {ScheduleId} (PublicationCode: {PublicationCode})",
                schedule.Id, music.PublicationCode);
        }
    }

    private static async Task SetMusicTrackName(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        AlarmMusic music,
        IMediaService? mediaService)
    {
        if (music.TrackNumber <= 0 || mediaService == null)
        {
            return;
        }

        try
        {
            string? trackName = await GetTrackName(music, mediaService);
            if (!string.IsNullOrWhiteSpace(trackName))
            {
                scheduleStateItem.MusicTrackName = trackName;
                Log.Logger.Debug("Set MusicTrackName '{MusicTrackName}' for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                    trackName, schedule.Id, music.TrackNumber);
            }
        }
        catch (Exception trackEx)
        {
            Log.Logger.Warning(trackEx, "Error loading music track name for schedule {ScheduleId} (TrackNumber: {TrackNumber})",
                schedule.Id, music.TrackNumber);
        }
    }

    private static async Task<string?> GetTrackName(AlarmMusic music, IMediaService mediaService)
    {
        if (music.MusicType == Shared.Models.Enums.MusicType.Melodies &&
            !string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            var tracks = await mediaService.GetMelodyMusicTracks(music.PublicationCode);
            if (tracks.TryGetValue(music.TrackNumber, out var track))
            {
                return $"Melody Number(s) {track.Title}";
            }
        }
        else if (music.MusicType == Shared.Models.Enums.MusicType.Vocals &&
                 !string.IsNullOrWhiteSpace(music.LanguageCode) &&
                 !string.IsNullOrWhiteSpace(music.PublicationCode))
        {
            var tracks = await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode);
            if (tracks.TryGetValue(music.TrackNumber, out var track))
            {
                return track.Title;
            }
        }

        return null;
    }

    private static async Task PopulateDefaultMusicIfNeeded(
        AlarmSchedule schedule,
        ScheduleStateItem scheduleStateItem,
        BootstrapServices services)
    {
        if (scheduleStateItem.MusicType.HasValue &&
            scheduleStateItem.MusicTrackNumber.HasValue &&
            scheduleStateItem.MusicTrackNumber.Value > 0)
        {
            return;
        }

        try
        {
            const string defaultPublicationCode = "iam";

            if (services.MelodyMusicService == null)
            {
                return;
            }

            var melodyMusic = await services.MelodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);

            if (melodyMusic?.Tracks == null || melodyMusic.Tracks.Count == 0)
            {
                Log.Logger.Warning("Melody music '{PublicationCode}' not found or has no tracks - cannot populate default music for schedule {ScheduleId}",
                    defaultPublicationCode, schedule.Id);
                return;
            }

            var randomTrack = melodyMusic.Tracks[Random.Shared.Next(melodyMusic.Tracks.Count)];

            scheduleStateItem.MusicType = Shared.Models.Enums.MusicType.Melodies;
            scheduleStateItem.MusicPublicationCode = defaultPublicationCode;
            scheduleStateItem.MusicLanguageCode = null;
            scheduleStateItem.MusicTrackNumber = randomTrack.Number;
            scheduleStateItem.MusicRepeat = false;
            scheduleStateItem.MusicTrackName = $"Melody Number(s) {randomTrack.Title}";

            Log.Logger.Information("Populated default music properties for schedule {ScheduleId}. MusicType=Melodies, PublicationCode={PublicationCode}, TrackNumber={TrackNumber}",
                schedule.Id, defaultPublicationCode, randomTrack.Number);
        }
        catch (Exception defaultMusicEx)
        {
            Log.Logger.Warning(defaultMusicEx, "Error populating default music properties for schedule {ScheduleId}",
                schedule.Id);
        }
    }

    private static async Task DispatchInitializeAction(IDispatcher dispatcher, ObservableHashSet<ScheduleStateItem> initialSchedules)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    dispatcher.Dispatch(new InitializeAction(initialSchedules));
                    Log.Logger.Information("Dispatched InitializeAction with {Count} schedules", initialSchedules.Count);
                }
                catch (Exception dispatchEx)
                {
                    Log.Logger.Error(dispatchEx, "Error dispatching InitializeAction");
                    throw;
                }
            });
        }
        catch (Exception mainThreadEx)
        {
            Log.Logger.Error(mainThreadEx, "Error invoking MainThread for InitializeAction dispatch");
            throw;
        }
    }

    /// <summary>
    /// Copies silent.mp3 from embedded resources to storage directory (same as schedule database).
    /// This ensures the file is available for Android Auto dummy tracks.
    /// Only runs on Android platform. Fast exits if file already exists.
    /// </summary>
    private static async Task CopySilentMp3ToStorage()
    {
#if ANDROID
        try
        {
            const string ResourceFileName = "silent.mp3";
            var storageService = ServiceProviderManager.GetService<IStorageService>();

            // Copy to StorageRoot (same directory as schedule database) instead of CacheRoot
            // because cache can get deleted by the system
            var storageDir = storageService.StorageRoot;
            var filePath = Path.Combine(storageDir, ResourceFileName);

            // Fast exit: Check if file already exists synchronously first
            if (File.Exists(filePath))
            {
                Log.Logger.Debug("Silent MP3 already exists in storage: {FilePath}", filePath);
                return;
            }

            // Copy from embedded resource to storage directory
            await storageService.CopyResourceFile(ResourceFileName, storageDir, ResourceFileName);
            Log.Logger.Information("Silent MP3 copied to storage: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Failed to copy silent MP3 to storage - will attempt to copy on-demand");
        }
#else
        // Only needed on Android for Android Auto dummy tracks
        await Task.CompletedTask;
#endif
    }

}
