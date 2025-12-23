#nullable enable

using AutoMapper;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
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
            // Get services needed for schedule initialization
            var databaseSeedService = ServiceProviderManager.GetService<IDatabaseSeedService>();
            var scheduleMigrationService = ServiceProviderManager.GetService<IScheduleMigrationService>();
            var alarmScheduleService = ServiceProviderManager.GetService<IAlarmScheduleService>();
            var dispatcher = ServiceProviderManager.GetService<IDispatcher>();

            if (databaseSeedService == null || scheduleMigrationService == null ||
                alarmScheduleService == null || dispatcher == null)
            {
                Log.Logger.Warning("Required services not available for schedule initialization - skipping");
                return;
            }

            // Run seed and migration first (same as HomeViewModel)
            await databaseSeedService.SeedDefaultAlarmAsync();
            await scheduleMigrationService.MigrateBibleGatewaySchedulesAsync();

            // Load all schedules from database
            var alarmSchedules = await alarmScheduleService.GetAllSchedulesAsync(
                includeMusic: true,
                includeBibleReading: true);

            Log.Logger.Information("Loaded {Count} schedules from database during bootstrap", alarmSchedules.Count);

            // Load language dictionary for translation names
            Dictionary<string, Language>? languagesDict = null;
            var bibleTranslationService = ServiceProviderManager.GetService<IBibleTranslationService>();
            try
            {
                languagesDict = await bibleTranslationService.GetDistinctLanguagesAsync();
                Log.Logger.Information("Loaded {Count} languages for translation name lookup", languagesDict.Count);
            }
            catch (Exception langEx)
            {
                Log.Logger.Warning(langEx, "Error loading languages - translation names will not be populated");
            }

            // Get BibleBookService for book name lookup
            var bibleBookService = ServiceProviderManager.GetService<IBibleBookService>();

            // Get AutoMapper instance
            var mapper = ServiceProviderManager.GetService<IMapper>();

            // Create ObservableHashSet of ScheduleStateItem for state using AutoMapper.
            // IMPORTANT: Populate ALL display name fields here during bootstrap so that existing schedules
            // can be loaded from state without accessing the database. The only time we access the media
            // database in ScheduleViewModel is when creating a NEW schedule.
            // Include: BibleReadingLanguageName, BibleReadingPublicationName, BibleReadingBookName,
            // MusicLanguageName, MusicPublicationName, MusicTrackName
            // 
            // NOTE: This uses EF Core .Include() for efficient joins when loading schedules, but then
            // makes per-schedule queries for display names (one query per schedule for translation, book,
            // music language, publication, track). This is acceptable for bootstrap as it only runs once
            // at app startup. For better efficiency, we could batch these queries, but the current approach
            // is simpler and the performance impact is minimal since bootstrap runs once.
            var initialSchedules = new ObservableHashSet<ScheduleStateItem>();
            foreach (var schedule in alarmSchedules)
            {
                // Map AlarmSchedule to ScheduleStateItem using AutoMapper
                var scheduleStateItem = mapper.Map<ScheduleStateItem>(schedule);

                // Set BibleReadingLanguageName and BibleReadingBookName from services
                if (schedule.BibleReadingSchedule != null)
                {
                    var bibleReading = schedule.BibleReadingSchedule;

                    // Set BibleReadingLanguageName from language dictionary
                    if (languagesDict != null)
                    {
                        var languageCode = bibleReading.LanguageCode;
                        if (!string.IsNullOrWhiteSpace(languageCode) &&
                            languagesDict.TryGetValue(languageCode, out var language))
                        {
                            scheduleStateItem.BibleReadingLanguageName = language.Name;
                            Log.Logger.Debug("Set BibleReadingLanguageName '{BibleReadingLanguageName}' for schedule {ScheduleId} (LanguageCode: {LanguageCode})",
                                language.Name, schedule.Id, languageCode);
                        }
                        else
                        {
                            // Fallback to language code if language not found
                            scheduleStateItem.BibleReadingLanguageName = languageCode;
                            Log.Logger.Debug("Language not found for LanguageCode '{LanguageCode}', using code as BibleReadingLanguageName for schedule {ScheduleId}",
                                languageCode, schedule.Id);
                        }
                    }

                    // Set BibleReadingPublicationName from BibleTranslationService
                    if (bibleTranslationService != null &&
                        !string.IsNullOrWhiteSpace(bibleReading.LanguageCode) &&
                        !string.IsNullOrWhiteSpace(bibleReading.PublicationCode))
                    {
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

                    // Set BibleReadingBookName from BibleBookService
                    if (bibleBookService != null && bibleReading.BookNumber > 0)
                    {
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
                }

                // Set music display properties from services
                var mediaService = ServiceProviderManager.GetService<IMediaService>();
                var melodyMusicService = ServiceProviderManager.GetService<IMelodyMusicService>();
                
                if (schedule.Music != null)
                {
                    var music = schedule.Music;

                    // Set MusicLanguageName for vocals
                    if (music.MusicType == Shared.Models.Enums.MusicType.Vocals &&
                        !string.IsNullOrWhiteSpace(music.LanguageCode) &&
                        mediaService != null)
                    {
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

                    // Set MusicPublicationName for vocals
                    if (music.MusicType == Shared.Models.Enums.MusicType.Vocals &&
                        !string.IsNullOrWhiteSpace(music.LanguageCode) &&
                        !string.IsNullOrWhiteSpace(music.PublicationCode) &&
                        mediaService != null)
                    {
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

                    // Set MusicTrackName
                    if (music.TrackNumber > 0 && mediaService != null)
                    {
                        try
                        {
                            string? trackName = null;
                            if (music.MusicType == Shared.Models.Enums.MusicType.Melodies &&
                                !string.IsNullOrWhiteSpace(music.PublicationCode))
                            {
                                var tracks = await mediaService.GetMelodyMusicTracks(music.PublicationCode);
                                if (tracks.TryGetValue(music.TrackNumber, out var track))
                                {
                                    // Format melody track title with prefix to match track modal display
                                    trackName = $"Melody Number(s) {track.Title}";
                                }
                            }
                            else if (music.MusicType == Shared.Models.Enums.MusicType.Vocals &&
                                     !string.IsNullOrWhiteSpace(music.LanguageCode) &&
                                     !string.IsNullOrWhiteSpace(music.PublicationCode))
                            {
                                var tracks = await mediaService.GetVocalMusicTracks(music.LanguageCode, music.PublicationCode);
                                if (tracks.TryGetValue(music.TrackNumber, out var track))
                                {
                                    trackName = track.Title;
                                }
                            }

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
                }
                
                // Populate default music properties if missing (even when music is disabled)
                // This ensures music properties are available when music is re-enabled
                if (!scheduleStateItem.MusicType.HasValue || 
                    !scheduleStateItem.MusicTrackNumber.HasValue || 
                    scheduleStateItem.MusicTrackNumber.Value <= 0)
                {
                    try
                    {
                        // Use same default music as sample schedule: Melodies with "iam" publication
                        const string defaultPublicationCode = "iam";
                        
                        if (melodyMusicService != null && mediaService != null)
                        {
                            // Get melody music with tracks
                            var melodyMusic = await melodyMusicService.GetByCodeWithTracksAsync(defaultPublicationCode);
                            
                            if (melodyMusic != null && melodyMusic.Tracks != null && melodyMusic.Tracks.Count > 0)
                            {
                                // Select a random track (same as sample schedule)
                                var randomTrack = melodyMusic.Tracks[Random.Shared.Next(melodyMusic.Tracks.Count)];
                                
                                // Populate music properties
                                scheduleStateItem.MusicType = Shared.Models.Enums.MusicType.Melodies;
                                scheduleStateItem.MusicPublicationCode = defaultPublicationCode;
                                scheduleStateItem.MusicLanguageCode = null; // Melodies don't have language code
                                scheduleStateItem.MusicTrackNumber = randomTrack.Number;
                                scheduleStateItem.MusicRepeat = false;
                                
                                // Set track name
                                scheduleStateItem.MusicTrackName = $"Melody Number(s) {randomTrack.Title}";
                                
                                Log.Logger.Information("Populated default music properties for schedule {ScheduleId}. MusicType=Melodies, PublicationCode={PublicationCode}, TrackNumber={TrackNumber}",
                                    schedule.Id, defaultPublicationCode, randomTrack.Number);
                            }
                            else
                            {
                                Log.Logger.Warning("Melody music '{PublicationCode}' not found or has no tracks - cannot populate default music for schedule {ScheduleId}",
                                    defaultPublicationCode, schedule.Id);
                            }
                        }
                    }
                    catch (Exception defaultMusicEx)
                    {
                        Log.Logger.Warning(defaultMusicEx, "Error populating default music properties for schedule {ScheduleId}",
                            schedule.Id);
                    }
                }

                initialSchedules.Add(scheduleStateItem);
            }

            // Dispatch InitializeAction to populate state
            // This must be done on main thread since Fluxor dispatcher may require UI context
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
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error initializing schedules in bootstrap");
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
