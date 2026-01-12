#nullable enable

using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper for determining content structure based on publication code.
/// Drama publications have a flat Track structure, while Bible publications have Section → Track structure.
/// </summary>
public static class PublicationTypeHelper
{
    private static readonly HashSet<string> DramaPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dramas",
        "DramaticBibleReadings",
        "gnj" // Good News According to Jesus (video)
    };

    /// <summary>
    /// Returns true if the publication has a Section → Track structure (traditional Bible).
    /// Returns false for dramas which have a flat Track structure.
    /// </summary>
    public static bool HasSectionStructure(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return true; // Default to Bible structure
        }

        return !DramaPublicationCodes.Contains(publicationCode);
    }

    /// <summary>
    /// Returns true if the publication is a drama (Audio Bible Dramas or Dramatic Bible Readings).
    /// </summary>
    public static bool IsDrama(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return false;
        }

        return DramaPublicationCodes.Contains(publicationCode);
    }

    /// <summary>
    /// Gets the appropriate label for track/track selection based on publication type.
    /// </summary>
    public static string GetTrackLabel(string? publicationCode)
    {
        return HasSectionStructure(publicationCode) ? "Track" : "Part";
    }

    /// <summary>
    /// Gets a user-friendly display name for the content type.
    /// </summary>
    public static string GetContentTypeDisplayName(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return "Bible Reading";
        }

        return publicationCode.ToLowerInvariant() switch
        {
            "dramas" => "Audio Bible Dramas",
            "dramaticbiblereadings" => "Dramatic Bible Readings",
            "gnj" => "Good News According to Jesus",
            _ => "Bible Reading"
        };
    }
}
