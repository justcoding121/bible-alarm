#nullable enable

using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects.Services;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Handles AddScheduleAction effect logic.
/// </summary>
public class ScheduleAddHandler
{
    private readonly IMapper mapper;
    private readonly ScheduleDisplayNamePopulator displayNamePopulator;

    public ScheduleAddHandler(IMapper mapper, ScheduleDisplayNamePopulator displayNamePopulator)
    {
        this.mapper = mapper;
        this.displayNamePopulator = displayNamePopulator;
    }

    public async Task HandleAsync(AddScheduleAction action, IDispatcher dispatcher)
    {
        try
        {
            Log.Information("ScheduleEffects: HandleAddSchedule - ScheduleId: {ScheduleId}, Name: {Name}",
                action.Schedule?.Id, action.Schedule?.Name);

            if (action.Schedule == null)
            {
                Log.Warning("ScheduleEffects: HandleAddSchedule - Schedule is null, skipping");
                return;
            }

            // Transform DB entity to State DTO (following Fluxor best practices)
            var scheduleStateItem = mapper.Map<ScheduleStateItem>(action.Schedule);

            // Populate BiblePublicationLanguageName, BiblePublicationPublicationName, and BiblePublicationSectionName if BiblePublicationSchedule exists
            await displayNamePopulator.PopulateTranslationNameAsync(scheduleStateItem, action.Schedule);
            await displayNamePopulator.PopulatePublicationNameAsync(scheduleStateItem, action.Schedule);
            await displayNamePopulator.PopulateSectionNameAsync(scheduleStateItem, action.Schedule);

            // Populate music display properties if Music exists
            await displayNamePopulator.PopulateMusicLanguageNameAsync(scheduleStateItem, action.Schedule);
            await displayNamePopulator.PopulateMusicPublicationNameAsync(scheduleStateItem, action.Schedule);
            await displayNamePopulator.PopulateMusicTrackNameAsync(scheduleStateItem, action.Schedule);

            // Dispatch success action with DTO (reducer will handle this)
            dispatcher.Dispatch(new AddScheduleSuccessAction(scheduleStateItem));

            Log.Information("ScheduleEffects: HandleAddSchedule - Dispatched AddScheduleSuccessAction for ScheduleId: {ScheduleId}",
                scheduleStateItem.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ScheduleEffects: Error in HandleAddSchedule");
        }
    }
}

