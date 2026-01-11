#nullable enable
using AutoMapper;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Stores.Selectors;

/// <summary>
/// Memoized selectors for deriving ViewModel data from domain models in ApplicationState.
/// Following Fluxor best practices: Selectors derive UI-specific data from domain models.
/// 
/// Pattern: Domain Model → View Model (in memoized selectors)
/// - Selectors act like computed ViewModel properties
/// - Ideal for MAUI Blazor where selectors derive UI-specific data
/// - Uses AutoMapper for domain model → entity mapping
/// 
/// Usage in ViewModels:
///   var currentSchedule = ApplicationSelectors.GetCurrentScheduleEntity(_state.Value, _mapper);
///   Or subscribe to state changes and use selectors to derive ViewModel data
/// </summary>
public static class ApplicationSelectors
{
    /// <summary>
    /// Selector: Get current schedule as AlarmSchedule entity (for ViewModel use).
    /// Maps ScheduleStateItem (domain model) → AlarmSchedule (entity for ViewModel).
    /// </summary>
    public static AlarmSchedule? GetCurrentScheduleEntity(ApplicationState state, IMapper mapper)
    {
        if (state.CurrentSchedule == null)
        {
            return null;
        }

        return mapper.Map<AlarmSchedule>(state.CurrentSchedule);
    }

    /// <summary>
    /// Selector: Get current music as AlarmMusic entity (for ViewModel use).
    /// Maps MusicStateItem (domain model) → AlarmMusic (entity for ViewModel).
    /// </summary>
    public static AlarmMusic? GetCurrentMusicEntity(ApplicationState state, IMapper mapper)
    {
        if (state.CurrentMusic == null)
        {
            return null;
        }

        return mapper.Map<AlarmMusic>(state.CurrentMusic);
    }

    /// <summary>
    /// Selector: Get current Bible reading schedule as BiblePublicationSchedule entity (for ViewModel use).
    /// Maps BiblePublicationStateItem (domain model) → BiblePublicationSchedule (entity for ViewModel).
    /// </summary>
    public static BiblePublicationSchedule? GetCurrentBiblePublicationEntity(ApplicationState state, IMapper mapper)
    {
        if (state.CurrentBiblePublicationSchedule == null)
        {
            return null;
        }

        return mapper.Map<BiblePublicationSchedule>(state.CurrentBiblePublicationSchedule);
    }

    /// <summary>
    /// Selector: Get all schedules as a list of AlarmSchedule entities (for ViewModel use).
    /// Maps ScheduleStateItem[] (domain models) → AlarmSchedule[] (entities for ViewModel).
    /// </summary>
    public static List<AlarmSchedule> GetAllSchedulesEntities(ApplicationState state, IMapper mapper)
    {
        if (state.Schedules == null || state.Schedules.Count == 0)
        {
            return [];
        }

        return state.Schedules.Select(mapper.Map<AlarmSchedule>).ToList();
    }

    /// <summary>
    /// Selector: Get schedule by ID as AlarmSchedule entity (for ViewModel use).
    /// Maps ScheduleStateItem (domain model) → AlarmSchedule (entity for ViewModel).
    /// </summary>
    public static AlarmSchedule? GetScheduleByIdEntity(ApplicationState state, int scheduleId, IMapper mapper)
    {
        var scheduleStateItem = state.Schedules?.FirstOrDefault(s => s.Id == scheduleId);
        if (scheduleStateItem == null)
        {
            return null;
        }

        return mapper.Map<AlarmSchedule>(scheduleStateItem);
    }
}
