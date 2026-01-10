#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Storage.Interfaces;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Helper class for bootstrap operations.
/// Delegates to IBootstrapOrchestrator for the main bootstrap flow.
/// Provides static methods for cache refresh operations used by ScheduleEffects.
/// </summary>
public static class CommonBootstrapHelper
{
    public static async Task VerifyServices(bool initializeUi = false)
    {
        var orchestrator = ServiceProviderManager.GetService<IBootstrapOrchestrator>();
        if (orchestrator == null)
        {
            Log.Logger.Error("IBootstrapOrchestrator not found - bootstrap cannot proceed");
            return;
        }

        await orchestrator.VerifyServicesAsync(initializeUi);
    }

    /// <summary>
    /// Record containing services required for bootstrap and cache operations.
    /// Exposed for use by ScheduleEffects to refresh cache after mutations.
    /// </summary>
    public record BootstrapServices(
        IDatabaseSeedService DatabaseSeedService,
        IScheduleMigrationService ScheduleMigrationService,
        IAlarmScheduleService AlarmScheduleService,
        IDispatcher Dispatcher,
        IBiblePublicationService? BiblePublicationService,
        IBibleBookService? BibleBookService,
        IMapper Mapper,
        IMediaService? MediaService,
        IMelodyMusicService? MelodyMusicService);

    /// <summary>
    /// Gets required services for cache refresh operations.
    /// Exposed for use by ScheduleEffects to refresh cache after mutations.
    /// </summary>
    public static BootstrapServices? GetRequiredServicesForCache()
    {
        return GetRequiredServicesInternal();
    }

    /// <summary>
    /// Loads languages dictionary for cache refresh operations.
    /// Exposed for use by ScheduleEffects to refresh cache after mutations.
    /// </summary>
    public static async Task<Dictionary<string, Language>?> LoadLanguagesDictionaryForCache(IBiblePublicationService? BiblePublicationService)
    {
        var scheduleBootstrapService = ServiceProviderManager.GetService<IScheduleBootstrapService>();
        if (scheduleBootstrapService == null)
        {
            Log.Logger.Warning("IScheduleBootstrapService not found - cannot load languages dictionary");
            return null;
        }

        return await scheduleBootstrapService.LoadLanguagesDictionaryAsync();
    }

    /// <summary>
    /// Loads schedules list for cache refresh operations.
    /// Exposed for use by ScheduleEffects to refresh cache after mutations.
    /// </summary>
    public static async Task<List<ScheduleStateItem>> LoadSchedulesListAsyncForCache(
        BootstrapServices services,
        Dictionary<string, Language>? languagesDict)
    {
        var scheduleBootstrapService = ServiceProviderManager.GetService<IScheduleBootstrapService>();
        if (scheduleBootstrapService == null)
        {
            Log.Logger.Warning("IScheduleBootstrapService not found - cannot load schedules list");
            return new List<ScheduleStateItem>();
        }

        return await scheduleBootstrapService.LoadSchedulesListAsync(languagesDict);
    }

    private static BootstrapServices? GetRequiredServicesInternal()
    {
        var databaseSeedService = ServiceProviderManager.GetService<IDatabaseSeedService>();
        var scheduleMigrationService = ServiceProviderManager.GetService<IScheduleMigrationService>();
        var alarmScheduleService = ServiceProviderManager.GetService<IAlarmScheduleService>();
        var dispatcher = ServiceProviderManager.GetService<IDispatcher>();
        var BiblePublicationService = ServiceProviderManager.GetService<IBiblePublicationService>();
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
            BiblePublicationService,
            bibleBookService,
            mapper,
            mediaService,
            melodyMusicService);
    }


}
