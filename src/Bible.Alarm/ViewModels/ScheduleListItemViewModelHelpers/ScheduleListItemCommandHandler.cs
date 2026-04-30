#nullable enable
using System.Windows.Input;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

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
            if (schedule == null || schedule.Id <= 0)
            {
                return;
            }

            // Notify HomeViewModel to show overlay immediately
            onPlayStarted?.Invoke();
            // Wait 50ms to ensure overlay is visible before starting playback
            await Task.Delay(50);
            await playbackService.PlayScheduleAsync(schedule.Id);
        });
    }

    /// <summary>
    /// Creates the previous command.
    /// </summary>
    public ICommand CreatePreviousCommand(AlarmSchedule? schedule)
    {
        return new AsyncRelayCommand(async () =>
        {
            if (schedule == null || schedule.Id <= 0)
            {
                logger.Warning(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.PreviousCommandScheduleNullOrInvalidId);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Schedule not found"));
                return;
            }

            // Check if schedule has Bible reading configured
            if (schedule.BiblePublicationSchedule == null)
            {
                logger.Debug(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.PreviousCommandNoBibleReadingConfigured, schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Please configure Bible reading for this schedule"));
                return;
            }

            var canMove = await playbackService.CanMoveTrackAsync(schedule.Id);
            if (!canMove)
            {
                logger.Debug(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.PreviousCommandCannotMoveTrackInProgress, schedule.Id);
                return;
            }

            logger.Information(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.PreviousCommandMovingToPreviousTrack, schedule.Id);
            
            // Show progress bar to indicate background activity
            WeakReferenceMessenger.Default.Send(new ShowProgressBarMessage());
            
            try
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToPreviousBiblePublicationTrack(schedule.Id);
                });
                logger.Information(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.PreviousCommandSuccessfullyMoved, schedule.Id);
                // Don't refresh here - OnApplicationStateChanged will handle it when state updates
                // This prevents showing stale data before the state is updated
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.PreviousCommandErrorMoving, schedule.Id);
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
            if (schedule == null || schedule.Id <= 0)
            {
                logger.Warning(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.NextCommandScheduleNullOrInvalidId);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Schedule not found"));
                return;
            }

            // Check if schedule has Bible reading configured
            if (schedule.BiblePublicationSchedule == null)
            {
                logger.Debug(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.NextCommandNoBibleReadingConfigured, schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Please configure Bible reading for this schedule"));
                return;
            }

            var canMove = await playbackService.CanMoveTrackAsync(schedule.Id);
            if (!canMove)
            {
                logger.Debug(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.NextCommandCannotMoveTrackInProgress, schedule.Id);
                return;
            }

            logger.Information(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.NextCommandMovingToNextTrack, schedule.Id);
            
            // Show progress bar to indicate background activity
            WeakReferenceMessenger.Default.Send(new ShowProgressBarMessage());
            
            try
            {
                // Run database operations off UI thread
                await Task.Run(async () =>
                {
                    await playlistService.MoveToNextBiblePublicationTrack(schedule.Id);
                });
                logger.Information(AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.NextCommandSuccessfullyMoved, schedule.Id);
                // Don't refresh here - OnApplicationStateChanged will handle it when state updates
                // This prevents showing stale data before the state is updated
            }
            catch (Exception ex)
            {
                logger.Error(ex, AppConstants.Logging.ScheduleListItemCommandHandlerDiagnosticsLog.NextCommandErrorMoving, schedule.Id);
                WeakReferenceMessenger.Default.Send(new ShowToastMessage("Error moving to next track"));
            }
        });
    }

}
