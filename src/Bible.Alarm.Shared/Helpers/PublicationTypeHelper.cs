#nullable enable

using System;
using System.Collections.Generic;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper for determining content structure based on publication code.
/// There are three main cataloging types:
/// 1. Bible (sectioned): Section → Track structure using booknum parameter
/// 2. Drama (Mediator API): Flat tracks using Mediator API for discovery, then GETPUBMEDIALINKS with section codes
/// 3. Flat (Music/Video): Flat tracks using GETPUBMEDIALINKS directly (MP3 for Music, MP4 for Video)
/// </summary>
public static class PublicationTypeHelper
{
    private static readonly HashSet<string> DramaPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        AppConstants.Media.BiblePublicationCategoryDramas,
        AppConstants.Media.BiblePublicationCodeDramaticBibleReadings,
        AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes,
        AppConstants.Media.BiblePublicationCodeVODMoviesModernDay,
        AppConstants.Media.BiblePublicationCodeVODMoviesAnimated,
        AppConstants.Media.BiblePublicationCodeVODMoviesExtras
    };

    private static readonly HashSet<string> VideoPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        AppConstants.Media.BiblePublicationCodeDramasGoodNews, // Good News According to Jesus (video, Mediator API)
        AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes, // Bible Times (video, Mediator API)
        AppConstants.Media.BiblePublicationCodeVODMoviesModernDay, // Modern-Day (video, Mediator API)
        AppConstants.Media.BiblePublicationCodeVODMoviesAnimated, // Animated (video, Mediator API)
        AppConstants.Media.BiblePublicationCodeVODMoviesExtras // Extras (video, Mediator API)
    };

    /// <summary>
    /// Series category flat video publications (GETPUBMEDIALINKS, MP4). Treated as Flat + IsVideo.
    /// </summary>
    private static readonly HashSet<string> SeriesVideoPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        AppConstants.Media.SeriesPublicationCodeThv // Apply Yourself to Reading and Teaching—Videos
    };

    /// <summary>
    /// Returns true if the publication has a Section → Track structure.
    /// Bible (books 1-66), Music "iam" (Kingdom Melodies discs), and Magazines (issue-based) have sections.
    /// Returns false for dramas, videos, and other music which have a flat Track structure.
    /// </summary>
    public static bool HasSectionStructure(string? publicationCode)
    {
        // Default empty to Bible structure; magazines and Kingdom Melodies ("iam") use sections.
        if (string.IsNullOrEmpty(publicationCode)
            || MagazineHelper.IsMagazinePublicationCode(publicationCode)
            || publicationCode.Equals(AppConstants.Media.MelodyMusicPublicationCodeIam, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Flat catalogs: Mediator flat tracks, article series, books/yearbooks/brochures, series video, vocal/melody music.
        if (JwSourceHelper.AllMediatorPublicationCodes.Contains(publicationCode)
            || JwSourceHelper.ArticleSeriesPublicationCodes.Contains(publicationCode)
            || JwSourceHelper.BooksPublicationCodes.Contains(publicationCode)
            || JwSourceHelper.YearbooksPublicationCodes.Contains(publicationCode)
            || JwSourceHelper.BrochuresAndBookletsPublicationCodes.Contains(publicationCode)
            || SeriesVideoPublicationCodes.Contains(publicationCode))
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
    /// Canonical database publication codes for the two MP3 audio dramas (<c>dramas</c> vs Dramatic Bible Readings);
    /// all other discovery codes pass through unchanged.
    /// </summary>
    public static string GetCanonicalPublicationCodeForDatabase(string publicationCodeFromDiscovery)
    {
        var lowerCode = publicationCodeFromDiscovery.ToLowerInvariant();
        if (!IsDrama(lowerCode))
        {
            return publicationCodeFromDiscovery;
        }

        return lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
            ? AppConstants.Media.BiblePublicationCategoryDramas
            : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings;
    }

    /// <summary>
    /// Mediator drama publications that are audio-only (MP3). Excluded from IsVideo so we request MP3, not MP4.
    /// </summary>
    private static readonly HashSet<string> AudioDramaPublicationCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        AppConstants.Media.BiblePublicationCategoryDramas,
        AppConstants.Media.BiblePublicationCodeDramaticBibleReadings
    };

    /// <summary>
    /// Returns true if the publication is a video (e.g., "The Good News According to Jesus").
    /// Dramas and DramaticBibleReadings are audio (MP3); all other mediator-based publications are video (MP4).
    /// </summary>
    public static bool IsVideo(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode)
            || MagazineHelper.IsMagazinePublicationCode(publicationCode))
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
        return HasSectionStructure(publicationCode) ? AppConstants.Media.PublicationUiTrackSingular : AppConstants.Media.PublicationUiPartSingular;
    }

    /// <summary>
    /// Determines the catalog type for a given publication code.
    /// </summary>
    public static CatalogType GetCatalogType(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return CatalogType.Sectioned; // Default to Bible structure
        }

        // Magazine publications use issue-based sectioned fetching
        if (MagazineHelper.IsMagazinePublicationCode(publicationCode))
        {
            return CatalogType.IssueSectioned;
        }

        // Publications excluded from Mediator use GETPUBMEDIALINKS only (MediatorValidationExclusionCodes is currently empty)
        if (JwSourceHelper.MediatorValidationExclusionCodes.Contains(publicationCode))
        {
            return CatalogType.Flat;
        }

        // All Mediator-based publications use Mediator API
        if (JwSourceHelper.AllMediatorPublicationCodes.Contains(publicationCode))
        {
            return CatalogType.MediatorSectioned;
        }

        // Bible and iam (Kingdom Melodies) have sections
        if (HasSectionStructure(publicationCode))
        {
            return CatalogType.Sectioned;
        }

        // Music and Video are flat
        return CatalogType.Flat;
    }
}
