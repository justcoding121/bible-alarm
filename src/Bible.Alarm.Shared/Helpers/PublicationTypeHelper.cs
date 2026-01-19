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
        "DramaticBibleReadings"
    };

    private static readonly HashSet<string> VideoPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gnj" // Good News According to Jesus (video)
    };

    /// <summary>
    /// Returns true if the publication has a Section → Track structure.
    /// Only Bible (books 1-66) and Music "iam" (Kingdom Melodies discs) have sections.
    /// Returns false for dramas, videos, and other music which have a flat Track structure.
    /// </summary>
    public static bool HasSectionStructure(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return true; // Default to Bible structure
        }

        // Only Bible and "iam" (Kingdom Melodies) have sections
        if (publicationCode.Equals("iam", StringComparison.OrdinalIgnoreCase))
        {
            return true; // Kingdom Melodies uses discs (sections)
        }

        // Dramas and videos have flat tracks
        if (DramaPublicationCodes.Contains(publicationCode) || VideoPublicationCodes.Contains(publicationCode))
        {
            return false;
        }

        // Check if it's a music publication (vocal or melody, but not "iam" which we already handled)
        var isMusic = JwSourceHelper.VocalMusicPublicationCodes.Contains(publicationCode) ||
                      JwSourceHelper.MelodyMusicPublicationCodes.Contains(publicationCode);
        
        if (isMusic)
        {
            return false; // Music publications (except "iam") have flat tracks
        }

        // Default to Bible structure (has sections)
        return true;
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
    /// Returns true if the publication is a video (e.g., "The Good News According to Jesus").
    /// Videos are non-sectioned like dramas but use GETPUBMEDIALINKS API, not Mediator API.
    /// </summary>
    public static bool IsVideo(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return false;
        }

        return VideoPublicationCodes.Contains(publicationCode);
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
