#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Services.Database.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap;

/// <summary>
/// Orchestrates the overall bootstrap process, coordinating all bootstrap services.
/// </summary>
public class BootstrapOrchestrator : IBootstrapOrchestrator
{
    private static readonly SemaphoreSlim @lock = new(1);
    private static volatile bool servicesVerified;

    private readonly IDatabaseBootstrapService databaseBootstrapService;
    private readonly IFluxorBootstrapService fluxorBootstrapService;
    private readonly IResourceBootstrapService resourceBootstrapService;
    private readonly IScheduleBootstrapService scheduleBootstrapService;
    private readonly IPlatformBootstrapService platformBootstrapService;
    private readonly IDatabaseSeedService databaseSeedService;
    private readonly ILanguageNameService languageNameService;
    private readonly ICategoryNameService categoryNameService;

    public BootstrapOrchestrator(
        IDatabaseBootstrapService databaseBootstrapService,
        IFluxorBootstrapService fluxorBootstrapService,
        IResourceBootstrapService resourceBootstrapService,
        IScheduleBootstrapService scheduleBootstrapService,
        IPlatformBootstrapService platformBootstrapService,
        IDatabaseSeedService databaseSeedService,
        ILanguageNameService languageNameService,
        ICategoryNameService categoryNameService)
    {
        this.databaseBootstrapService = databaseBootstrapService;
        this.fluxorBootstrapService = fluxorBootstrapService;
        this.resourceBootstrapService = resourceBootstrapService;
        this.scheduleBootstrapService = scheduleBootstrapService;
        this.platformBootstrapService = platformBootstrapService;
        this.databaseSeedService = databaseSeedService;
        this.languageNameService = languageNameService;
        this.categoryNameService = categoryNameService;
    }

    public async Task VerifyServicesAsync(bool initializeUi = false)
    {
        Log.Logger.Information("VerifyServices called with initializeUI={InitializeUI}, _servicesVerified={ServicesVerified}",
            initializeUi, servicesVerified);

        // Track if we need to send early navigation after database/Fluxor are ready
        var shouldSendEarlyNav = false;

        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (servicesVerified)
            {
                Log.Logger.Information("Services already verified, skipping database operations");
                // Even if services are verified, we still need to ensure schedules are loaded
                // This handles the case where background bootstrap completed but schedules weren't loaded yet
                if (!initializeUi)
                {
                    // Background service - schedules should already be loaded
                    return;
                }
                // Foreground UI - ensure schedules are loaded even if services were verified by background
                try
                {
                    Log.Logger.Information("[BOOTSTRAP] Services verified but ensuring schedules are loaded for UI");
                    await scheduleBootstrapService.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "[BOOTSTRAP] Error loading schedules after services verified");
                }
                return;
            }
            else
            {
#if DEBUG
                var dbOpsStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
                Log.Logger.Information("[BOOTSTRAP] Starting database and IO operations");
#endif
                // Track if we should send early navigation (only for UI initialization)
                shouldSendEarlyNav = initializeUi;

                // Run database and IO operations off UI thread
                await Task.Run(async () =>
                {
#if DEBUG
                    var verifyMediaStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                    // Run all bootstrap tasks in parallel, including HTTP assembly warmup
                    // Assembly warmup preloads HTTP/download assemblies to prevent UI freezes
                    // when DefaultCarScreenEffect downloads track metadata after bootstrap
                    var task1 = databaseBootstrapService.InitializeAsync();
                    var task2 = fluxorBootstrapService.InitializeAsync();
                    var task3 = resourceBootstrapService.CopyResourcesAsync();
                    var task4 = AssemblyWarmupHelper.WarmupHttpAssembliesAsync();

                    await Task.WhenAll(task1, task2, task3, task4);
#if DEBUG
                    var verifyMediaElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - verifyMediaStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    Log.Logger.Information("[BOOTSTRAP] Media index verification/copy completed in {ElapsedMs:F2}ms", verifyMediaElapsed);
#endif

                    // After both database and resource bootstrap complete, run non-English fetch only on version change
                    // (when media index was replaced this run), not on every bootstrap
                    if (resourceBootstrapService.WasMediaIndexReplacedThisRun())
                    {
                        await resourceBootstrapService.MigrateNonEnglishMediaDataAsync();
                    }

                    // Warm in-memory caches for category and language display names (current app language "E")
                    // so UI lookups avoid DB hits. Future app languages can be warmed similarly.
                    try
                    {
                        var defaultLang = AppConstants.Media.DefaultLanguageCode;
                        await languageNameService.WarmCacheForDisplayLanguageAsync(defaultLang);
                        await categoryNameService.WarmCacheForDisplayLanguageAsync(defaultLang);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Warning(ex, "[BOOTSTRAP] Failed to warm display-name caches, lookups will use DB");
                    }

                    // Send InitializedMessage early (after database/Fluxor are ready) to show UI with loading state
                    // This improves perceived performance - user sees the home page while schedules are being populated
                    if (shouldSendEarlyNav)
                    {
#if DEBUG
                        var earlyNavStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
                        Log.Logger.Information("[BOOTSTRAP] Sending InitializedMessage early (before schedule population)");
#endif
                        try
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    WeakReferenceMessenger.Default.Send(new InitializedMessage());
#if DEBUG
                                    var earlyNavElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - earlyNavStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                                    Log.Logger.Information("[BOOTSTRAP] Early InitializedMessage sent - Navigation triggered in {ElapsedMs:F2}ms", earlyNavElapsed);
#endif
                                }
                                catch (Exception ex)
                                {
                                    Log.Logger.Error(ex, "Error sending early InitializedMessage");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Error(ex, "Error invoking MainThread for early InitializedMessage");
                        }
                    }

                    // After database and Fluxor store are initialized, load schedules into state
                    // This ensures schedules are available for both Android Auto services and main UI
                    // UI is already showing (via early InitializedMessage), so user sees loading state
                    await scheduleBootstrapService.InitializeAsync();

                    // Initialize platform-specific services (notification channels, background jobs)
                    // This runs after core bootstrap to ensure platform setup regardless of entry point
                    await platformBootstrapService.InitializeAsync();
                });
                servicesVerified = true;
                
                // Set bootstrap completion flag so IsBootstrapCompleted() works correctly
                // This ensures the flag is set regardless of which bootstrap path is taken
                BootstrapHelper.MarkBootstrapCompleted();
                
#if DEBUG
                var dbOpsElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dbOpsStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Log.Logger.Information("[BOOTSTRAP] Database and IO operations completed in {ElapsedMs:F2}ms", dbOpsElapsed);
#endif
            }
        });

        // Send InitializedMessage after lock is released (only if not already sent early)
        // NavigateToHomeAsync handles duplicate navigation attempts internally
        // CRITICAL: Send InitializedMessage even if services were already verified
        // This handles the case where Android Auto completed bootstrap first (isForeground=false)
        // and the UI needs to navigate to Home
        if (initializeUi && shouldSendEarlyNav)
        {
            // Services just verified, and we already sent early InitializedMessage above
            // So we don't need to send it again here
            Log.Logger.Debug("InitializedMessage already sent early, skipping duplicate send");
        }
        else if (initializeUi && servicesVerified)
        {
            // Services were already verified (bootstrap completed by Android Auto or previous call)
            // Since bootstrap is complete, handlers should already be registered, so send immediately
#if DEBUG
            var navStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
            Log.Logger.Information("[BOOTSTRAP] Sending InitializedMessage immediately (services verified: {ServicesVerified}, bootstrap complete)", servicesVerified);
#endif
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        WeakReferenceMessenger.Default.Send(new InitializedMessage());
#if DEBUG
                        var navElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - navStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                        Log.Logger.Information("[BOOTSTRAP] InitializedMessage sent immediately - Navigation triggered in {ElapsedMs:F2}ms", navElapsed);
#endif
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error sending InitializedMessage (immediate)");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error invoking MainThread for immediate InitializedMessage");
            }
        }
        else
        {
            Log.Logger.Information("initializeUI=false, not sending InitializedMessage");
        }
    }
}

