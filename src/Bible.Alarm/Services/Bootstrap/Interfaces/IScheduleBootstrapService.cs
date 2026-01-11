#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Services.Bootstrap.Interfaces;

/// <summary>
/// Service for loading schedules and populating state during bootstrap.
/// </summary>
public interface IScheduleBootstrapService
{
    /// <summary>
    /// Seeds default schedule if database is empty and migrates legacy schedules.
    /// </summary>
    /// <returns>True if a schedule was seeded, false otherwise.</returns>
    Task<bool> SeedAndMigrateAsync();

    /// <summary>
    /// Loads schedules from database or cache and populates Fluxor state.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Loads languages dictionary for translation name lookup.
    /// </summary>
    Task<Dictionary<string, Language>?> LoadLanguagesDictionaryAsync();

    /// <summary>
    /// Loads schedules list from database and populates state items.
    /// Used for cache refresh operations.
    /// </summary>
    Task<List<ScheduleStateItem>> LoadSchedulesListAsync(Dictionary<string, Language>? languagesDict);
}

