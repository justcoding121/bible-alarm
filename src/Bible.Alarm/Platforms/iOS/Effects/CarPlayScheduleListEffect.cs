#nullable enable
using Bible.Alarm.Platforms.iOS.Services.CarPlay;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Serilog;
using BiblePublicationTrackSelectedAction = Bible.Alarm.Stores.Actions.BiblePublications.TrackSelectedAction;
using FluxorDispatcher = Fluxor.IDispatcher;
using MusicTrackSelectedAction = Bible.Alarm.Stores.Actions.Music.TrackSelectedAction;

namespace Bible.Alarm.Platforms.iOS.Effects;

/// <summary>
/// Fluxor effect that refreshes the CarPlay schedule list when schedules change.
/// Listens for schedule load (InitializeAction), create, update, delete, and track selection actions.
/// </summary>
public class CarPlayScheduleListEffect
{
    private static readonly ILogger logger = Log.ForContext<CarPlayScheduleListEffect>();

    /// <summary>
    /// Handles InitializeAction - when bootstrap loads schedules into state.
    /// Fixes "No schedules" when CarPlay connects before bootstrap completes.
    /// </summary>
    [EffectMethod]
    public Task HandleInitialize(InitializeAction action, FluxorDispatcher dispatcher)
    {
        if (action.ScheduleList.Count > 0)
        {
            RefreshCarPlayScheduleList("Initialize");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles schedule creation success - refresh CarPlay list.
    /// </summary>
    [EffectMethod]
    public Task HandleCreateScheduleSuccess(CreateScheduleSuccessAction action, FluxorDispatcher dispatcher)
    {
        RefreshCarPlayScheduleList("CreateScheduleSuccess");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles schedule update success - refresh CarPlay list.
    /// </summary>
    [EffectMethod]
    public Task HandleUpdateScheduleSuccess(UpdateScheduleSuccessAction action, FluxorDispatcher dispatcher)
    {
        RefreshCarPlayScheduleList("UpdateScheduleSuccess");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles schedule delete success - refresh CarPlay list.
    /// </summary>
    [EffectMethod]
    public Task HandleRemoveScheduleSuccess(RemoveScheduleSuccessAction action, FluxorDispatcher dispatcher)
    {
        RefreshCarPlayScheduleList("RemoveScheduleSuccess");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles Bible publication track selection - refresh CarPlay list.
    /// </summary>
    [EffectMethod]
    public Task HandleBiblePublicationTrackSelected(BiblePublicationTrackSelectedAction action, FluxorDispatcher dispatcher)
    {
        RefreshCarPlayScheduleList("BiblePublicationTrackSelected");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles music track selection - refresh CarPlay list.
    /// </summary>
    [EffectMethod]
    public Task HandleMusicTrackSelected(MusicTrackSelectedAction action, FluxorDispatcher dispatcher)
    {
        RefreshCarPlayScheduleList("MusicTrackSelected");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles LastPlayedAtUtc update - refresh CarPlay list so recently played schedules appear first.
    /// </summary>
    [EffectMethod]
    public Task HandleUpdateScheduleLastPlayed(UpdateScheduleLastPlayedAction action, FluxorDispatcher dispatcher)
    {
        RefreshCarPlayScheduleList("UpdateScheduleLastPlayed");
        return Task.CompletedTask;
    }

    private void RefreshCarPlayScheduleList(string triggerAction)
    {
        if (!CarPlaySceneDelegate.IsCarPlayConnected)
        {
            logger.Debug("[CarPlay] Not connected, skipping schedule list refresh for {Action}", triggerAction);
            return;
        }

        try
        {
            logger.Information("[CarPlay] Refreshing schedule list due to {Action}", triggerAction);
            CarPlaySceneDelegate.Current?.RefreshScheduleList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[CarPlay] Error refreshing schedule list");
        }
    }
}