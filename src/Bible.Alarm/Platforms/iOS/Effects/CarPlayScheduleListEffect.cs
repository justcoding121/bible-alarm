#nullable enable
using Bible.Alarm.Platforms.iOS.Services.CarPlay;
using Bible.Alarm.Stores.Actions.Schedule;
using Fluxor;
using Serilog;
using FluxorDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Platforms.iOS.Effects;

/// <summary>
/// Fluxor effect that refreshes the CarPlay schedule list when schedules change.
/// Listens for schedule create, update, and delete success actions.
/// </summary>
public class CarPlayScheduleListEffect
{
    private static readonly ILogger logger = Log.ForContext<CarPlayScheduleListEffect>();

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
