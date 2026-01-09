#nullable enable

namespace Bible.Alarm.Services.UI;

/// <summary>
/// Simple context to pass schedule ID during navigation.
/// Set before navigation, read by ScheduleStateManager, cleared after use.
/// This avoids Fluxor state race conditions with ResetScheduleStateAction.
/// </summary>
public static class ScheduleNavigationContext
{
    /// <summary>
    /// The schedule ID to load from database. Null means create new schedule.
    /// </summary>
    public static int? ScheduleIdToLoad { get; set; }
    
    /// <summary>
    /// The IsEnabled value for the schedule being loaded.
    /// </summary>
    public static bool IsEnabledToLoad { get; set; }
    
    /// <summary>
    /// Clears the navigation context after use.
    /// </summary>
    public static void Clear()
    {
        ScheduleIdToLoad = null;
        IsEnabledToLoad = false;
    }
}
