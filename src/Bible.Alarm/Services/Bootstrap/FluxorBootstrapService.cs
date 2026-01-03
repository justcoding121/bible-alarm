#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Stores;
using Fluxor;
using Serilog;
using Bible.Alarm.Common;

#if ANDROID
using Bible.Alarm.Platforms.Android.Effects;
#endif

namespace Bible.Alarm.Services.Bootstrap;

/// <summary>
/// Service for initializing Fluxor store during bootstrap.
/// </summary>
public class FluxorBootstrapService : IFluxorBootstrapService
{
    private readonly IStore? store;

    public FluxorBootstrapService(IStore? store)
    {
        this.store = store;
    }

    public async Task InitializeAsync()
    {
#if DEBUG
        var fluxorStartTime = System.Diagnostics.Stopwatch.GetTimestamp();
        Log.Logger.Information("[BOOTSTRAP] Fluxor store initialization starting");
#endif

        if (store == null)
        {
            Log.Logger.Warning("IStore service not found - Fluxor store initialization skipped");
            return;
        }

        // Initialize store asynchronously
        await store.InitializeAsync();

        // Set the static store reference
        ReduxContainer.Store = store;
        
        // Log registered effects for debugging
        try
        {
            // Try to verify ScheduleEffects is registered
            var scheduleEffects = ServiceProviderManager.GetService<Bible.Alarm.Stores.Effects.ScheduleEffects>();
            if (scheduleEffects != null)
            {
                Log.Logger.Debug("Fluxor: ScheduleEffects is registered in DI container");
            }
            else
            {
                Log.Logger.Warning("Fluxor: ScheduleEffects is NOT registered in DI container!");
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "Could not verify ScheduleEffects registration");
        }

#if DEBUG
        var fluxorElapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - fluxorStartTime) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Log.Logger.Information("[BOOTSTRAP] Fluxor store initialization completed in {ElapsedMs:F2}ms", fluxorElapsed);
#endif

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
}

