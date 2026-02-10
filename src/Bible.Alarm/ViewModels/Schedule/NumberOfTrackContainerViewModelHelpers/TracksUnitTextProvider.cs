#nullable enable

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainerViewModelHelpers;

/// <summary>
/// Provides category-based unit text for tracks (Chapter/Episode/Track).
/// </summary>
public static class TracksUnitTextProvider
{
    public enum TracksUnit
    {
        Chapter,
        Episode,
        Track
    }

    public static TracksUnit GetTracksUnit(string? categoryName)
    {
        if (string.Equals(categoryName, "Music", StringComparison.OrdinalIgnoreCase))
        {
            return TracksUnit.Track;
        }

        if (string.Equals(categoryName, "Dramas", StringComparison.OrdinalIgnoreCase))
        {
            return TracksUnit.Episode;
        }

        return TracksUnit.Chapter;
    }

    public static (string Singular, string Plural) GetUnitTextTitleCase(string? categoryName)
    {
        return GetTracksUnit(categoryName) switch
        {
            TracksUnit.Track => ("Track", "Tracks"),
            TracksUnit.Episode => ("Episode", "Episodes"),
            _ => ("Chapter", "Chapters")
        };
    }

    public static (string Singular, string Plural) GetUnitTextLowerCase(string? categoryName)
    {
        return GetTracksUnit(categoryName) switch
        {
            TracksUnit.Track => ("track", "tracks"),
            TracksUnit.Episode => ("episode", "episodes"),
            _ => ("chapter", "chapters")
        };
    }
}
