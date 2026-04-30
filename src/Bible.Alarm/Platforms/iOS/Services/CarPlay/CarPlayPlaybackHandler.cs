#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.Services.CarPlay;

/// <summary>
/// Handles playback actions for CarPlay schedule items.
/// </summary>
public sealed class CarPlayPlaybackHandler
{
    private CarPlayPlaybackHandler()
    {
    }

    private static readonly ILogger logger = Log.ForContext<CarPlayPlaybackHandler>();
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Handles schedule item clicked from CarPlay list.
    /// The completion callback is called after playback preparation finishes (or times out)
    /// so that CarPlay shows a native loading spinner on the tapped item while preparing.
    /// </summary>
    public static void HandleScheduleItemClicked(int scheduleId, Action completion)
    {
        logger.Information("[CarPlay] Schedule {ScheduleId} clicked - starting playback", scheduleId);
        WeakReferenceMessenger.Default.Send(new RequestShowPlaybackModalMessage { TargetScheduleId = scheduleId });

        _ = Task.Run(async () =>
        {
            try
            {
                await StartPlaybackWithCompletionAsync(scheduleId, completion);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "[CarPlay] Error handling schedule click for schedule {ScheduleId}", scheduleId);
                CompleteOnMainThread(completion);
            }
        });
    }

    private static async Task StartPlaybackWithCompletionAsync(int scheduleId, Action completion)
    {
        var playbackService = ServiceProviderManager.GetService<ISchedulePlaybackService>();
        if (playbackService == null)
        {
            logger.Error("[CarPlay] ISchedulePlaybackService is null - cannot play schedule {ScheduleId}", scheduleId);
            CompleteOnMainThread(completion);
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(CompletionTimeout);
            var playTask = playbackService.PlayScheduleAsync(scheduleId);
            var completedTask = await Task.WhenAny(playTask, Task.Delay(CompletionTimeout, cts.Token));

            if (completedTask == playTask)
            {
                await playTask;
                logger.Information("[CarPlay] Started playback for schedule {ScheduleId}", scheduleId);
            }
            else
            {
                logger.Warning("[CarPlay] Playback preparation timed out for schedule {ScheduleId}, dismissing spinner", scheduleId);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[CarPlay] Error playing schedule {ScheduleId}", scheduleId);
        }
        finally
        {
            CompleteOnMainThread(completion);
        }
    }

    private static void CompleteOnMainThread(Action completion)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                completion();
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "[CarPlay] Error calling completion handler");
            }
        });
    }
}
