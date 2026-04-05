#nullable enable
using Bible.Alarm.Platforms.Android.Services.AndroidAuto.Interfaces;
using Bible.Alarm.Platforms.Android.Services.Media;
using Bible.Alarm.Platforms.Android.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto;

/// <summary>
/// When Android Auto is connected and no track is playing, dispatches RotateDefaultScheduleAction every 5 minutes
/// so the Now Playing screen cycles through each schedule to encourage the user to tap and play.
/// Also serves as a secondary disconnect detector: if the CarConnection content provider reports
/// disconnected while the MediaBrowser bind flag is still stale, cleans up and stops.
/// </summary>
public sealed class AndroidAutoDefaultScheduleRotationService(
    IState<PlaybackState> playbackState,
    Fluxor.IDispatcher dispatcher,
    IMediaSessionManager mediaSessionManager) : IAndroidAutoDefaultScheduleRotationService
{
    private static readonly ILogger logger = Log.ForContext<AndroidAutoDefaultScheduleRotationService>();
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
                logger.Debug("Android Auto default schedule rotation already started");
                return;
            }

            cts = new CancellationTokenSource();
            lastRotationUtc = DateTime.UtcNow;
            _ = RunRotationLoopAsync(cts.Token);
            logger.Information("Android Auto default schedule rotation started (every {Minutes} min when car connected and not playing)", RotationInterval.TotalMinutes);
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
                logger.Warning(ex, "Error stopping Android Auto default schedule rotation");
            }
            finally
            {
                cts = null;
                lastRotationUtc = null;
            }

            logger.Information("Android Auto default schedule rotation stopped");
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

            if (!CarConnectionHelper.IsCarConnected())
            {
                // The CarConnection content provider says the car is physically disconnected.
                // If the MediaBrowser bind flag still says connected, OnUnbind was never called
                // (common with wireless Android Auto on some cars like Genesis GV70).
                // Clean up the stale state to remove the orphaned foreground notification
                // and deactivate the MediaSession (prevents Android 13+ system notification).
                if (ForegroundServiceCoordinator.IsAndroidAutoConnected)
                {
                    logger.Warning(
                        "Car physically disconnected (CarConnection provider) but MediaBrowser bind flag still true — cleaning up stale Android Auto state");
                    ForegroundServiceCoordinator.OnAndroidAutoDisconnected();
                    mediaSessionManager.SetActive(false);
                    break;
                }

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
            logger.Debug("Dispatching RotateDefaultScheduleAction for 5-minute rotation");
            dispatcher.Dispatch(new RotateDefaultScheduleAction());
        }
    }
}
