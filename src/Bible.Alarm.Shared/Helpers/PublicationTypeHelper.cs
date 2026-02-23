#nullable enable

using System;
using System.Collections.Generic;
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper for determining content structure based on publication code.
/// There are three main harvesting types:
/// 1. Bible (sectioned): Section → Track structure using booknum parameter
/// 2. Drama (Mediator API): Flat tracks using Mediator API for discovery, then GETPUBMEDIALINKS with section codes
/// 3. Flat (Music/Video): Flat tracks using GETPUBMEDIALINKS directly (MP3 for Music, MP4 for Video)
/// </summary>
public static class PublicationTypeHelper
{
    private static readonly HashSet<string> DramaPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dramas",
        "DramaticBibleReadings",
        "VODMoviesBibleTimes",
        "VODMoviesModernDay",
        "VODMoviesAnimated",
        "VODMoviesExtras"
    };

    private static readonly HashSet<string> VideoPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gnj", // Good News According to Jesus (video)
        "VODMoviesBibleTimes", // Bible Times (video, Mediator API)
        "VODMoviesModernDay", // Modern-Day (video, Mediator API)
        "VODMoviesAnimated", // Animated (video, Mediator API)
        "VODMoviesExtras" // Extras (video, Mediator API)
    };

    /// <summary>
    /// Series category flat video publications (GETPUBMEDIALINKS, MP4). Treated as Flat + IsVideo.
    /// </summary>
    private static readonly HashSet<string> SeriesVideoPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "thv" // Apply Yourself to Reading and Teaching—Videos
    };

    /// <summary>
    /// Series category publications that use Mediator API (MediatorSectioned, video). Treated as MediatorSectioned + IsVideo.
    /// </summary>
    private static readonly HashSet<string> SeriesMediatorPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "VODLFFVideosAD", // Enjoy Life Forever!—Videos
        "SeriesDigForTreasures" // Dig for Treasures in God's Word
    };

    /// <summary>
    /// Children category publications that use Mediator API (MediatorSectioned, video).
    /// </summary>
    private static readonly HashSet<string> ChildrenMediatorPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SeriesBJFLessons" // Bible Stories for Little Ones
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

        // All Mediator-based publications have flat tracks
        if (JwSourceHelper.AllMediatorPublicationCodes.Contains(publicationCode))
        {
            return false;
        }

        // Article Series (e.g. mrt = More Topics) are flat audio
        if (JwSourceHelper.ArticleSeriesPublicationCodes.Contains(publicationCode))
        {
            return false;
        }

        // Books, Yearbooks, Brochures and Booklets are flat audio (GETPUBMEDIALINKS, no booknum)
        if (JwSourceHelper.BooksPublicationCodes.Contains(publicationCode) ||
            JwSourceHelper.YearbooksPublicationCodes.Contains(publicationCode) ||
            JwSourceHelper.BrochuresAndBookletsPublicationCodes.Contains(publicationCode))
        {
            return false;
        }

        // Series flat video (thv)
        if (SeriesVideoPublicationCodes.Contains(publicationCode))
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
    /// Mediator drama publications that are audio-only (MP3). Excluded from IsVideo so we request MP3, not MP4.
    /// </summary>
    private static readonly HashSet<string> AudioDramaPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dramas",
        "DramaticBibleReadings"
    };

    /// <summary>
    /// Returns true if the publication is a video (e.g., "The Good News According to Jesus").
    /// Dramas and DramaticBibleReadings are audio (MP3); all other mediator-based publications are video (MP4).
    /// </summary>
    public static bool IsVideo(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return false;
        }

        if (VideoPublicationCodes.Contains(publicationCode) || SeriesVideoPublicationCodes.Contains(publicationCode))
        {
            return true;
        }

        if (AudioDramaPublicationCodes.Contains(publicationCode))
        {
            return false;
        }

        return JwSourceHelper.AllMediatorPublicationCodes.Contains(publicationCode);
    }

    /// <summary>
    /// Gets the appropriate label for track/track selection based on publication type.
    /// </summary>
    public static string GetTrackLabel(string? publicationCode)
    {
        return HasSectionStructure(publicationCode) ? "Track" : "Part";
    }

    /// <summary>
    /// Determines the harvest type for a given publication code.
    /// </summary>
    public static HarvestType GetHarvestType(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return HarvestType.Sectioned; // Default to Bible structure
        }

        // Flat-video-only publications (e.g. gnj) have no Mediator category; use GETPUBMEDIALINKS only
        if (JwSourceHelper.MediatorValidationExclusionCodes.Contains(publicationCode))
        {
            return HarvestType.Flat;
        }

        // All Mediator-based publications use Mediator API
        if (JwSourceHelper.AllMediatorPublicationCodes.Contains(publicationCode))
        {
            return HarvestType.MediatorSectioned;
        }

        // Bible and iam (Kingdom Melodies) have sections
        if (HasSectionStructure(publicationCode))
        {
            return HarvestType.Sectioned;
        }

        // Music and Video are flat
        return HarvestType.Flat;
    }
}
