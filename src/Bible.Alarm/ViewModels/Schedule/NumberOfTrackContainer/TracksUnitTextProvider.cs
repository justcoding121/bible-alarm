#nullable enable

namespace Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;

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

    public static string GetTrackLabelText(string? categoryName)
    {
        var (_, plural) = GetUnitTextTitleCase(categoryName);
        return $"{plural} to play each time";
    }

    public static string GetTracksLabelText(string? categoryName)
    {
        var (_, plural) = GetUnitTextLowerCase(categoryName);
        return $"Number of {plural} to play";
    }

    public static string GetSelectedTracksText(string? categoryName, int number)
    {
        var (singular, plural) = GetUnitTextTitleCase(categoryName);
        if (number == 0)
        {
            return plural;
        }

        var unit = number == 1 ? singular : plural;
        return $"{number} {unit}";
    }

    public static string GetModalHeaderText(string? categoryName)
    {
        var (_, plural) = GetUnitTextTitleCase(categoryName);
        return $"Select Number of {plural}";
    }

    public static string GetRestartLabelText(string? categoryName)
    {
        var (_, plural) = GetUnitTextLowerCase(categoryName);
        return $"Restart incomplete {plural} from the beginning";
    }
}
