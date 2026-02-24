#nullable enable
using Bible.Alarm.Platforms.iOS.Services.CarPlay.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.CarPlay;

/// <summary>
/// When CarPlay is connected and no track is playing, dispatches RotateDefaultScheduleAction every 5 minutes
/// so the Now Playing screen cycles through each schedule to encourage the user to tap and play.
/// </summary>
public sealed class CarPlayDefaultScheduleRotationService(
    IState<PlaybackState> playbackState,
    Fluxor.IDispatcher dispatcher) : ICarPlayDefaultScheduleRotationService
{
    private static readonly ILogger logger = Log.ForContext<CarPlayDefaultScheduleRotationService>();
    private static readonly TimeSpan RotationInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private CancellationTokenSource? cts;
    private DateTime? lastRotationUtc;
    private readonly object gate = new();

    public void Start()
    {
        lock (gate)
        {
            if (cts != null)
            {
                logger.Debug("[CarPlay] Default schedule rotation already started");
                return;
            }

            cts = new CancellationTokenSource();
            lastRotationUtc = DateTime.UtcNow;
            _ = RunRotationLoopAsync(cts.Token);
            logger.Information("[CarPlay] Default schedule rotation started (every {Minutes} min when connected and not playing)", RotationInterval.TotalMinutes);
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (cts == null)
            {
                return;
            }

            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "[CarPlay] Error stopping default schedule rotation");
            }
            finally
            {
                cts = null;
                lastRotationUtc = null;
            }

            logger.Information("[CarPlay] Default schedule rotation stopped");
        }
    }

    private async Task RunRotationLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!CarPlaySceneDelegate.IsCarPlayConnected)
            {
                continue;
            }

            if (playbackState.Value.IsPreparingOrPlaying)
            {
                continue;
            }

            var now = DateTime.UtcNow;
            if (lastRotationUtc.HasValue && (now - lastRotationUtc.Value) < RotationInterval)
            {
                continue;
            }

            lastRotationUtc = now;
            logger.Debug("[CarPlay] Dispatching RotateDefaultScheduleAction for 5-minute rotation");
            dispatcher.Dispatch(new RotateDefaultScheduleAction());
        }
    }
}
