#nullable enable

namespace Bible.Alarm.Services.Media.BrowseHelpers;

/// <summary>
/// Picks the schedule list a car or phone browse tree should show.
/// Fluxor wins once bootstrap has filled it. The database list is the cold-start fallback.
/// </summary>
public static class ScheduleBrowseSelector
{
    public static IReadOnlyList<T> Select<T>(IReadOnlyList<T> fromState, IReadOnlyList<T> fromDatabase)
    {
        if (fromState.Count > 0)
        {
            return fromState;
        }

        return fromDatabase;
    }
}
