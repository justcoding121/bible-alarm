#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Android.Effects;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Media;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// Ports <c>MediaSessionCallback</c> play, pause, next, previous, seek, and play-from-id behavior
/// onto the proxy player. Work runs off the main thread, matching the legacy callback.
/// </summary>
internal static class SessionCommandRouter
{
    private static readonly ILogger logger = Log.ForContext(typeof(SessionCommandRouter));

    public static void Play()
    {
        logger.Information("SessionCommandRouter.Play");
        var playbackState = ServiceProviderManager.GetService<IState<PlaybackState>>();
        if (playbackState?.Value?.IsPreparingOrPlaying is true)
        {
            Run(async () =>
            {
                WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                await Task.CompletedTask;
            });
            return;
        }

        MediaSessionEffect.SetRestartingPlayback(true);
        Run(async () =>
        {
            try
            {
                var playbackService = Require<IPlaybackService>();
                var scheduleId = SessionStatePlayer.Instance.CurrentScheduleId;
                WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = scheduleId });
                if (scheduleId is > 0)
                {
                    await playbackService.PrepareAndPlayAsync(scheduleId.Value, isAlarm: false);
                    return;
                }

                var firstId = Require<IDefaultScheduleService>().GetFirstScheduleId();
                if (firstId is > 0)
                {
                    await playbackService.PrepareAndPlayAsync(firstId.Value, isAlarm: false);
                    return;
                }

                WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
            }
            catch (Exception)
            {
                MediaSessionEffect.SetRestartingPlayback(false);
                throw;
            }
        });
    }

    public static void Pause()
    {
        Run(async () =>
        {
            WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
            await Task.CompletedTask;
        });
    }

    public static void Next() => Send(new NextButtonPressedMessage());

    public static void Previous() => Send(new PreviousButtonPressedMessage());

    public static void SeekForward() => Send(new SeekForwardButtonPressedMessage());

    public static void SeekBackward() => Send(new SeekBackwardButtonPressedMessage());

    public static void SeekTo(long positionMs)
    {
        Run(async () =>
        {
            await Require<IPlaybackService>().SeekToAsync(TimeSpan.FromMilliseconds(positionMs));
        });
    }

    public static void PlayFromMediaId(string? mediaId)
    {
        var parsed = MediaSessionMediaId.ParseScheduleId(mediaId);
        var playbackState = ServiceProviderManager.GetService<IState<PlaybackState>>();
        if (playbackState?.Value?.IsPreparingOrPlaying is true
            && playbackState.Value.CurrentScheduleId == parsed)
        {
            WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = parsed });
            Run(async () =>
            {
                WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                await Task.CompletedTask;
            });
            return;
        }

        MediaSessionEffect.SetRestartingPlayback(true);
        SessionStatePlayer.Instance.ShowBuffering();
        Run(async () =>
        {
            try
            {
                var scheduleId = ResolveScheduleId(parsed);
                if (scheduleId <= 0)
                {
                    return;
                }

                WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = scheduleId });
                await Require<IPlaybackService>().PrepareAndPlayAsync(scheduleId, isAlarm: false);
            }
            catch (Exception)
            {
                MediaSessionEffect.SetRestartingPlayback(false);
                throw;
            }
        });
    }

    private static int ResolveScheduleId(int scheduleId)
    {
        var schedules = Require<IDefaultScheduleService>();
        if (scheduleId > 0 && schedules.ValidateScheduleIdExists(scheduleId))
        {
            return scheduleId;
        }

        return schedules.GetFirstScheduleId() ?? 0;
    }

    private static void Send<TMessage>(TMessage message)
        where TMessage : class
    {
        Run(async () =>
        {
            WeakReferenceMessenger.Default.Send(message);
            await Task.CompletedTask;
        });
    }

    private static void Run(Func<Task> operation)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (!BootstrapHelper.IsBootstrapCompleted())
                {
                    SessionStatePlayer.Instance.ShowBuffering();
                }

                try
                {
                    await MauiProgram.WaitForBootstrapAsync();
                }
                catch (TimeoutException timeoutEx)
                {
                    logger.Warning(timeoutEx, "Bootstrap wait timed out in SessionCommandRouter");
                }

                await operation();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Session command failed");
            }
        });
    }

    private static T Require<T>() where T : notnull
        => ServiceProviderManager.GetService<T>() ?? throw new InvalidOperationException($"{typeof(T).Name} is not available.");
}
