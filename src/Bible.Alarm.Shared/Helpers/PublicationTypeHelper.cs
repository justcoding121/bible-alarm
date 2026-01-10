#nullable enable

using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper for determining content structure based on publication code.
/// Drama publications have a flat Track structure, while Bible publications have Book → Chapter structure.
/// </summary>
public static class PublicationTypeHelper
{
    private static readonly HashSet<string> DramaPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dramas",
        "DramaticBibleReadings"
    };

    /// <summary>
    /// Returns true if the publication has a Book → Chapter structure (traditional Bible).
    /// Returns false for dramas which have a flat Track structure.
    /// </summary>
    public static bool HasBookStructure(string? publicationCode)
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
    /// Gets the appropriate label for chapter/track selection based on publication type.
    /// </summary>
    public static string GetChapterLabel(string? publicationCode)
    {
        return HasBookStructure(publicationCode) ? "Chapter" : "Part";
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
            _ => "Bible Reading"
        };
    }
}
