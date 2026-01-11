#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Bootstrap.Interfaces;
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

    public BootstrapOrchestrator(
        IDatabaseBootstrapService databaseBootstrapService,
        IFluxorBootstrapService fluxorBootstrapService,
        IResourceBootstrapService resourceBootstrapService,
        IScheduleBootstrapService scheduleBootstrapService,
        IPlatformBootstrapService platformBootstrapService)
    {
        this.databaseBootstrapService = databaseBootstrapService;
        this.fluxorBootstrapService = fluxorBootstrapService;
        this.resourceBootstrapService = resourceBootstrapService;
        this.scheduleBootstrapService = scheduleBootstrapService;
        this.platformBootstrapService = platformBootstrapService;
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

                try
                {
                    // Run database and IO operations off UI thread
                    await Task.Run(async () =>
                    {
#if DEBUG
                        var verifyMediaStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
#endif
                        // Run all bootstrap tasks in parallel
                        var task1 = databaseBootstrapService.InitializeAsync();
                        var task2 = fluxorBootstrapService.InitializeAsync();
                        var task3 = resourceBootstrapService.CopyResourcesAsync();

                        await Task.WhenAll(task1, task2, task3);
#if DEBUG
                        var verifyMediaElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - verifyMediaStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                        Log.Logger.Information("[BOOTSTRAP] Media index verification/copy completed in {ElapsedMs:F2}ms", verifyMediaElapsed);
#endif

                        // Send InitializedMessage early (after database/Fluxor are ready) to show UI with loading state
                        // Use fire-and-forget to not block bootstrap - Home page creation runs in parallel with schedule loading
                        // This ensures the progress bar animation gets UI thread time to run smoothly
                        if (shouldSendEarlyNav)
                        {
#if DEBUG
                            Log.Logger.Information("[BOOTSTRAP] Sending InitializedMessage early (fire-and-forget, before schedule population)");
#endif
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    WeakReferenceMessenger.Default.Send(new InitializedMessage());
#if DEBUG
                                    Log.Logger.Information("[BOOTSTRAP] Early InitializedMessage sent - Navigation triggered");
#endif
                                }
                                catch (Exception ex)
                                {
                                    Log.Logger.Error(ex, "Error sending early InitializedMessage");
                                }
                            });
                        }

                        // Load schedules in parallel with Home page creation
                        // HomeViewModel will read state.Value on construction - if schedules are already loaded, it shows them immediately
                        // If schedules aren't loaded yet, HomeViewModel shows progress bar and updates when InitializeAction arrives
                        await scheduleBootstrapService.InitializeAsync();

                        // Initialize platform-specific services (notification channels, background jobs)
                        // This runs after core bootstrap to ensure platform setup regardless of entry point
                        await platformBootstrapService.InitializeAsync();
                    });

                    Log.Logger.Information("[BOOTSTRAP] Bootstrap tasks completed successfully");
                }
                catch (Exception ex)
                {
                    // Log the error but don't re-throw - allow app to continue
                    // Bootstrap failure is recoverable in most cases
                    Log.Logger.Error(ex, "[BOOTSTRAP] Error during bootstrap initialization - continuing anyway");
                }
                finally
                {
                    // CRITICAL: Always mark bootstrap as completed, even on failure
                    // This prevents infinite polling loops in BootstrapReadyManager
                    servicesVerified = true;

                    // Set bootstrap completion flag so IsBootstrapCompleted() works correctly
                    // This ensures the flag is set regardless of success or failure
                    BootstrapHelper.MarkBootstrapCompleted();

#if DEBUG
                    var dbOpsElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - dbOpsStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    Log.Logger.Information("[BOOTSTRAP] Database and IO operations completed in {ElapsedMs:F2}ms", dbOpsElapsed);
#endif
                }
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

