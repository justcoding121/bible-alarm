#nullable enable
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Playback;
using Serilog;
#if WINDOWS
using Bible.Alarm.Platforms.Windows.Helpers;
#endif

namespace Bible.Alarm.Services.UI;

public sealed class AppLifecycleService(ILogger logger, IServiceProvider serviceProvider, IReviewPromptService reviewPromptService) : IAppLifecycleService, IDisposable
{
    private readonly IServiceProvider serviceProvider = serviceProvider;

#if WINDOWS
    private WindowsPeriodicBackgroundTasks? periodicBackgroundTasks;
#endif

    public void OnStart()
    {
        App.IsInForeground = true;
        _ = RecordAppOpenSafeAsync("OnStart");

        // NOTE: Do NOT call InitializePlatformBootstrap here
        // WindowSetupService.CreateWindow() already handled bootstrap and sent InitializedMessage
        // This method is called AFTER CreateWindow(), so bootstrap is already complete

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);

#if WINDOWS
                // Start periodic background tasks (similar to Android JobScheduler)
                // This will run scheduler and media index update immediately on start, then continue periodically
                periodicBackgroundTasks ??= new WindowsPeriodicBackgroundTasks(logger, serviceProvider);
                periodicBackgroundTasks.Start();
#endif
            }
            catch (Exception e)
            {
                logger.Error(e, AppConstants.Logging.AppLifecycleDiagnosticsLog.ErrorInOnStartTask);
            }
        });
    }

    public static void OnSleep() => App.IsInForeground = false;

    public void OnResume()
    {
        App.IsInForeground = true;
        _ = RecordAppOpenSafeAsync("OnResume");

        try
        {
            var playbackModalService = serviceProvider.GetService<IPlaybackModalService>();
            playbackModalService?.ShowMiniBarIfPlaybackActiveOnResume();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.AppLifecycleDiagnosticsLog.ErrorShowingMiniBarPreviewOnResume);
        }

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000);

                ReconcilePlaybackState();

                try
                {
                    var playbackModalService = serviceProvider.GetService<IPlaybackModalService>();
                    if (playbackModalService != null)
                    {
                        await playbackModalService.ShowPlaybackModalIfNeededOnResumeAsync();
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, AppConstants.Logging.AppLifecycleDiagnosticsLog.ErrorShowingPlaybackModalOnResume);
                }

#if WINDOWS
                // Ensure periodic background tasks are running
                // This will run scheduler and media index update immediately on resume, then continue periodically
                periodicBackgroundTasks ??= new WindowsPeriodicBackgroundTasks(logger, serviceProvider);
                periodicBackgroundTasks.Start();
#endif
            }
            catch (Exception e)
            {
                logger.Error(e, AppConstants.Logging.AppLifecycleDiagnosticsLog.ErrorInOnResumeTask);
            }
        });
    }

    /// <summary>
    /// Detects when Fluxor thinks playback is active but the underlying player is dead
    /// (e.g. OS killed the foreground service, or an exception prevented PlaybackStoppedAction
    /// from being dispatched). Dispatches PlaybackStoppedAction to clean up the zombie state
    /// so the mini bar / playback modal are dismissed.
    /// </summary>
    private void ReconcilePlaybackState()
    {
        try
        {
            var playbackState = serviceProvider.GetService<Fluxor.IState<PlaybackState>>();
            if (playbackState?.Value?.IsPreparingOrPlaying != true)
            {
                return;
            }

            var audioPlayer = serviceProvider.GetService<IAudioPlayer>();
            if (audioPlayer == null)
            {
                return;
            }

            if (audioPlayer.IsActuallyPlayingOrPaused)
            {
                return;
            }

            var status = audioPlayer.Status;
            if (status is Services.Media.Models.PlayStatus.Loading)
            {
                return;
            }

            logger.Warning(
                AppConstants.Logging.AppLifecycleDiagnosticsLog.ReconcilePlaybackStateFluxorActivePlayerInactiveDispatchStopped,
                status);

            var dispatcher = serviceProvider.GetService<Fluxor.IDispatcher>();
            dispatcher?.Dispatch(new PlaybackStoppedAction());
#if ANDROID || IOS
            dispatcher?.Dispatch(new SetCarPlayScreenAction());
#endif
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.AppLifecycleDiagnosticsLog.ErrorReconcilingPlaybackStateOnResume);
        }
    }

    public void Dispose()
    {
#if WINDOWS
        periodicBackgroundTasks?.Dispose();
        periodicBackgroundTasks = null;
#endif
    }

    private async Task RecordAppOpenSafeAsync(string source)
    {
        try
        {
            await reviewPromptService.RecordAppOpenAsync();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.AppLifecycleDiagnosticsLog.ErrorRecordingAppOpenFromSource, source);
        }
    }
}

