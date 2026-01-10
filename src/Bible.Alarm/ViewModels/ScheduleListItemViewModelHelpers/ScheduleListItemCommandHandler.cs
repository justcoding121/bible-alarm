#nullable enable
using Bible;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using System.Windows.Input;
using IDispatcher = Fluxor.IDispatcher;
using IToastService = Bible.Alarm.Services.UI.Interfaces.IToastService;

namespace Bible.Alarm.ViewModels.ScheduleListItemViewModelHelpers;

/// <summary>
/// Handles command creation and execution for ScheduleListItemViewModel.
/// </summary>
public sealed class ScheduleListItemCommandHandler(
    ILogger logger,
    ISchedulePlaybackService playbackService,
    IPlaylistService playlistService)
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
            if (schedule?.Id <= 0)
            {
                logger.Warning("PreviousCommand: Schedule is null or has invalid ID");
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Schedule not found"));
                return;
            }

            if (schedule == null)
            {
                return;
            }

            // Check if schedule has Bible reading configured
            if (schedule.BibleReadingSchedule == null)
            {
                logger.Debug("PreviousCommand: Schedule {ScheduleId} does not have Bible reading configured", schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Please configure Bible reading for this schedule"));
                return;
            }

            var canMove = await playbackService.CanMoveTrackAsync(schedule.Id);
            if (!canMove)
            {
                logger.Debug("PreviousCommand: Cannot move track for schedule {ScheduleId} - schedule may be in progress", schedule.Id);
                return;
            }

            logger.Information("PreviousCommand: Moving to previous track for schedule {ScheduleId}", schedule.Id);
            try
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToPreviousBibleTrack(schedule.Id);
                });
                logger.Information("PreviousCommand: Successfully moved to previous track for schedule {ScheduleId}", schedule.Id);
                // Don't refresh here - OnApplicationStateChanged will handle it when state updates
                // This prevents showing stale data before the state is updated
            }
            catch (Exception ex)
            {
                logger.Error(ex, "PreviousCommand: Error moving to previous track for schedule {ScheduleId}", schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Error moving to previous track"));
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
            if (schedule?.Id <= 0)
            {
                logger.Warning("NextCommand: Schedule is null or has invalid ID");
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Schedule not found"));
                return;
            }

            if (schedule == null)
            {
                return;
            }

            // Check if schedule has Bible reading configured
            if (schedule.BibleReadingSchedule == null)
            {
                logger.Debug("NextCommand: Schedule {ScheduleId} does not have Bible reading configured", schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Please configure Bible reading for this schedule"));
                return;
            }

            var canMove = await playbackService.CanMoveTrackAsync(schedule.Id);
            if (!canMove)
            {
                logger.Debug("NextCommand: Cannot move track for schedule {ScheduleId} - schedule may be in progress", schedule.Id);
                return;
            }

            logger.Information("NextCommand: Moving to next track for schedule {ScheduleId}", schedule.Id);
            try
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToNextBibleTrack(schedule.Id);
                });
                logger.Information("NextCommand: Successfully moved to next track for schedule {ScheduleId}", schedule.Id);
                // Don't refresh here - OnApplicationStateChanged will handle it when state updates
                // This prevents showing stale data before the state is updated
            }
            catch (Exception ex)
            {
                logger.Error(ex, "NextCommand: Error moving to next track for schedule {ScheduleId}", schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Error moving to next track"));
            }
        });
    }

}
