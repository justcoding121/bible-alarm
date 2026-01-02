#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using System.Windows.Input;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.ScheduleListItemHelpers;

/// <summary>
/// Handles command creation and execution for ScheduleListItemViewModel.
/// </summary>
public sealed class ScheduleListItemCommandHandler(
    ILogger logger,
    ISchedulePlaybackService playbackService,
    IPlaylistService playlistService,
    IState<ApplicationState> applicationState,
    IDispatcher dispatcher)
{
    /// <summary>
    /// Creates the play command.
    /// </summary>
    public ICommand CreatePlayCommand(AlarmSchedule? schedule, Action? onPlayStarted)
    {
        return new AsyncRelayCommand(async () =>
        {
            if (schedule?.Id > 0)
            {
                // Notify HomeViewModel to show overlay immediately
                onPlayStarted?.Invoke();
                // Wait 50ms to ensure overlay is visible before starting playback
                await Task.Delay(50);
                await playbackService.PlayScheduleAsync(schedule.Id);
            }
        });
    }

    /// <summary>
    /// Creates the previous command.
    /// </summary>
    public ICommand CreatePreviousCommand(AlarmSchedule? schedule)
    {
        return new AsyncRelayCommand(async () =>
        {
            if (schedule?.Id > 0 && await playbackService.CanMoveChapterAsync(schedule.Id))
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToPreviousBibleChapter(schedule.Id);
                });
                // Don't refresh here - OnApplicationStateChanged will handle it when state updates
                // This prevents showing stale data before the state is updated
            }
        });
    }

    /// <summary>
    /// Creates the next command.
    /// </summary>
    public ICommand CreateNextCommand(AlarmSchedule? schedule)
    {
        return new AsyncRelayCommand(async () =>
        {
            if (schedule?.Id > 0 && await playbackService.CanMoveChapterAsync(schedule.Id))
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToNextBibleChapter(schedule.Id);
                });
                // Don't refresh here - OnApplicationStateChanged will handle it when state updates
                // This prevents showing stale data before the state is updated
            }
        });
    }

    /// <summary>
    /// Creates the delete command.
    /// </summary>
    public ICommand CreateDeleteCommand(AlarmSchedule? schedule)
    {
        return new AsyncRelayCommand(async () =>
        {
            if (schedule == null || schedule.Id <= 0)
            {
                return;
            }

            // Check if this is the last schedule - prevent deletion if it is
            var scheduleCount = applicationState.Value.Schedules?.Count ?? 0;
            if (scheduleCount <= 1)
            {
                logger.Warning("Cannot delete schedule {ScheduleId} - it is the last schedule", schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Cannot delete last schedule"));
                return;
            }

            // Dispatch DeleteScheduleAction (following Fluxor best practices)
            // The Effect will handle the actual DB deletion and dispatch success/failure actions
            logger.Information("ScheduleListItem: Dispatching DeleteScheduleAction for ScheduleId={ScheduleId}", schedule.Id);
            dispatcher.Dispatch(new DeleteScheduleAction(schedule.Id));
        });
    }
}
