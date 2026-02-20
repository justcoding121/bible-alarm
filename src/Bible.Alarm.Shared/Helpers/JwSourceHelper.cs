#nullable enable
using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper class for JW.org source-related utilities.
/// Centralized location for all publication codes used for seeding.
/// Publication names are extracted directly from API responses instead of using hardcoded mappings.
/// </summary>
public static class JwSourceHelper
{
    /// <summary>
    /// Bible publication codes used for seeding.
    /// </summary>
    public static HashSet<string> BiblePublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "nwt",
        "bi12"
    };

    /// <summary>
    /// Vocal music publication codes used for seeding.
    /// </summary>
    public static HashSet<string> VocalMusicPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "osg",
        "sjjc",
        "sjji",
        "snv",
        "pksjj"
    };

    /// <summary>
    /// Melody music publication codes used for seeding.
    /// </summary>
    public static HashSet<string> MelodyMusicPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "iam"
    };

    /// <summary>
    /// Video publication codes used for seeding.
    /// </summary>
    public static HashSet<string> VideoPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "gnj",
        "VODMoviesBibleTimes",
        "VODMoviesModernDay",
        "VODMoviesAnimated",
        "VODMoviesExtras"
    };

    /// <summary>
    /// Drama category codes used for seeding (exact casing for API/DB).
    /// </summary>
    public static HashSet<string> DramaCategoryCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "Dramas",
        "DramaticBibleReadings",
        "VODMoviesBibleTimes",
        "VODMoviesModernDay",
        "VODMoviesAnimated",
        "VODMoviesExtras"
    };

    /// <summary>
    /// Canonical drama publication codes in exact casing for DB and Mediator API.
    /// Used to resolve normalized (lowercase) code to the form required by the API and database.
    /// </summary>
    private static readonly string[] CanonicalDramaPublicationCodes =
    {
        "Dramas",
        "DramaticBibleReadings",
        "VODMoviesBibleTimes",
        "VODMoviesModernDay",
        "VODMoviesAnimated",
        "VODMoviesExtras"
    };

    /// <summary>
    /// Returns the canonical (exact-case) publication code for a drama, or null if not a drama.
    /// Use for DB queries and Mediator API category key.
    /// </summary>
    public static string? GetCanonicalDramaPublicationCode(string? normalizedPublicationCode)
    {
        if (string.IsNullOrEmpty(normalizedPublicationCode))
        {
            return null;
        }

        var normalized = normalizedPublicationCode.ToLowerInvariant();
        foreach (var code in CanonicalDramaPublicationCodes)
        {
            if (code.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        return null;
    }

    /// <summary>
    /// Publication codes for categories that have no harvesters yet. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> ArticleSeriesPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Books category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> BooksPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Broadcasting category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> BroadcastingPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Brochures and Booklets category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> BrochuresAndBookletsPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Children category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> ChildrenPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Family category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> FamilyPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Interviews and Experiences category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> InterviewsAndExperiencesPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Meetings and Ministry category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> MeetingsAndMinistryPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Programs and Events category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> ProgramsAndEventsPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Series category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> SeriesPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Publication codes for Teenagers category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> TeenagersPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Centralized mapping of category names to their publication codes.
    /// </summary>
    public static Dictionary<string, HashSet<string>> CategoryToPublicationCodes
    {
        get
        {
            var musicCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in VocalMusicPublicationCodes)
            {
                musicCodes.Add(code);
            }
            foreach (var code in MelodyMusicPublicationCodes)
            {
                musicCodes.Add(code);
            }

            var dramaCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in DramaCategoryCodes)
            {
                dramaCodes.Add(code);
            }
            foreach (var code in VideoPublicationCodes)
            {
                dramaCodes.Add(code);
            }

            return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bible"] = BiblePublicationCodes,
                ["Music"] = musicCodes,
                ["Dramas"] = dramaCodes,
                ["Article Series"] = ArticleSeriesPublicationCodes,
                ["Books"] = BooksPublicationCodes,
                ["Broadcasting"] = BroadcastingPublicationCodes,
                ["Brochures and Booklets"] = BrochuresAndBookletsPublicationCodes,
                ["Children"] = ChildrenPublicationCodes,
                ["Family"] = FamilyPublicationCodes,
                ["Interviews and Experiences"] = InterviewsAndExperiencesPublicationCodes,
                ["Meetings and Ministry"] = MeetingsAndMinistryPublicationCodes,
                ["Programs and Events"] = ProgramsAndEventsPublicationCodes,
                ["Series"] = SeriesPublicationCodes,
                ["Teenagers"] = TeenagersPublicationCodes
            };
        }
    }

    /// <summary>
    /// Gets the category name for a given publication code.
    /// Returns null if the publication code is not recognized.
    /// </summary>
    public static string? GetCategoryName(string publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode))
        {
            return null;
        }

        var normalizedCode = publicationCode.ToLowerInvariant();
        
        foreach (var (categoryName, codes) in CategoryToPublicationCodes)
        {
            if (codes.Contains(normalizedCode) || codes.Contains(publicationCode))
            {
                return categoryName;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all publication codes for a given category name.
    /// Returns an empty set if the category is not found.
    /// </summary>
    public static HashSet<string> GetPublicationCodesForCategory(string categoryName)
    {
        if (string.IsNullOrEmpty(categoryName))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return CategoryToPublicationCodes.TryGetValue(categoryName, out var codes)
            ? codes
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    // Publication names are extracted from API responses:
    // - Bible: parentPubName field
    // - Music: pubName field
    // - Drama: category.name field
    // - Video: pubName field
}
