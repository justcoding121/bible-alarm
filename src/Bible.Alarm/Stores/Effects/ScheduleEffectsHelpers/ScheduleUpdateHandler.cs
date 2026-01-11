#nullable enable

using AutoMapper;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Handles UpdateScheduleAction effect logic.
/// </summary>
public class ScheduleUpdateHandler
{
    private readonly IMapper mapper;
    private readonly ScheduleDisplayNamePopulator displayNamePopulator;
    private readonly ScheduleCacheManager cacheManager;

    public ScheduleUpdateHandler(IMapper mapper, ScheduleDisplayNamePopulator displayNamePopulator, ScheduleCacheManager cacheManager)
    {
        this.mapper = mapper;
        this.displayNamePopulator = displayNamePopulator;
        this.cacheManager = cacheManager;
    }

    public async Task HandleAsync(UpdateScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information("ScheduleEffects: HandleUpdateSchedule - ScheduleId: {ScheduleId}, Name: {Name}",
                action.Schedule?.Id, action.Schedule?.Name);

            if (action.Schedule == null)
            {
                Log.Warning("ScheduleEffects: HandleUpdateSchedule - Schedule is null, skipping");
                return;
            }

            // Clear cache BEFORE processing (schedule was already saved to DB in PlaylistService)
            // This prevents stale cache if process crashes after save but before refresh
            cacheManager.InvalidateScheduleCache();

            // Transform DB entity to State DTO
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(action.Schedule);

            // Populate BiblePublicationLanguageName, BiblePublicationName, and BiblePublicationSectionName if missing
            // (Note: This is for UpdateScheduleAction which doesn't go through the optimistic reducer)
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationLanguageName))
            {
                await displayNamePopulator.PopulateTranslationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationName))
            {
                await displayNamePopulator.PopulatePublicationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationSectionName))
            {
                await displayNamePopulator.PopulateSectionNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.BiblePublicationTrackTitle))
            {
                await displayNamePopulator.PopulateTrackTitleAsync(scheduleStateItem, action.Schedule);
            }

            // Populate music display properties if missing
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicLanguageName))
            {
                await displayNamePopulator.PopulateMusicLanguageNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicPublicationName))
            {
                await displayNamePopulator.PopulateMusicPublicationNameAsync(scheduleStateItem, action.Schedule);
            }
            if (string.IsNullOrWhiteSpace(scheduleStateItem.MusicTrackName))
            {
                await displayNamePopulator.PopulateMusicTrackNameAsync(scheduleStateItem, action.Schedule);
            }

            // Dispatch success action with DTO (reducer will handle this)
            dispatcher.Dispatch(new UpdateScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleUpdateSchedule - Dispatched UpdateScheduleSuccessAction for ScheduleId: {ScheduleId}",
                scheduleStateItem.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleUpdateSchedule");
        }
    }
}

