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
    /// Canonical drama and Mediator API publication/category codes in exact casing for DB and Mediator API.
    /// Used to resolve normalized (lowercase) code to the form required by the API and database.
    /// </summary>
    private static readonly string[] CanonicalDramaPublicationCodes =
    {
        "Dramas",
        "DramaticBibleReadings",
        "VODMoviesBibleTimes",
        "VODMoviesModernDay",
        "VODMoviesAnimated",
        "VODMoviesExtras",
        "VODLFFVideosAD", // Enjoy Life Forever!—Videos (Series, Mediator API)
        "SeriesDigForTreasures", // Dig for Treasures in God's Word (Series, Mediator API)
        "SeriesBJFLessons" // Bible Stories for Little Ones (Children, Mediator API)
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
    /// Publication codes for Children category.
    /// </summary>
    public static HashSet<string> ChildrenPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "SeriesBJFLessons" // Bible Stories for Little Ones (Mediator API)
    };

    /// <summary>
    /// Children category publications that use Mediator API for discovery (MediatorSectioned).
    /// </summary>
    public static HashSet<string> ChildrenMediatorPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "SeriesBJFLessons"
    };

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
    /// Publication codes for Series category (flat video and/or Mediator-sectioned video).
    /// </summary>
    public static HashSet<string> SeriesPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "thv", // Apply Yourself to Reading and Teaching—Videos (Flat)
        "VODLFFVideosAD", // Enjoy Life Forever!—Videos (Mediator API)
        "SeriesDigForTreasures" // Dig for Treasures in God's Word (Mediator API)
    };

    /// <summary>
    /// Series publications that use Mediator API for discovery (MediatorSectioned). Used by DramaHarvester.
    /// </summary>
    public static HashSet<string> SeriesMediatorPublicationCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "VODLFFVideosAD",
        "SeriesDigForTreasures"
    };

    /// <summary>
    /// Publication codes for Teenagers category. Empty until JW API codes are added.
    /// </summary>
    public static HashSet<string> TeenagersPublicationCodes => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All language-bound publication codes that should be harvested for English (E) in the harvester.
    /// Matches the cursor rules table: Bible, Music (vocal only), Dramas, Children, Series.
    /// Excludes melody (iam) which has no language. Used by EnglishSeeder to ensure every listed publication gets E.
    /// </summary>
    public static HashSet<string> AllPublicationCodesForEnglishSeeding
    {
        get
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in BiblePublicationCodes) set.Add(code);
            foreach (var code in VocalMusicPublicationCodes) set.Add(code);
            foreach (var code in DramaCategoryCodes) set.Add(code);
            foreach (var code in VideoPublicationCodes) set.Add(code);
            foreach (var code in ChildrenPublicationCodes) set.Add(code);
            foreach (var code in SeriesPublicationCodes) set.Add(code);
            return set;
        }
    }

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
    /// Gets the category name for a given publication code (for display/legacy).
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
    /// Gets the CategoryCode (DB) for a given publication code, e.g. "Bible", "Music", "Dramas".
    /// Returns null if the publication code is not recognized.
    /// </summary>
    public static string? GetCategoryCode(string publicationCode)
    {
        var name = GetCategoryName(publicationCode);
        if (name == null)
        {
            return null;
        }
        return CategoryNameToCode(name);
    }

    private static string CategoryNameToCode(string categoryName)
    {
        return categoryName.Replace(" and ", "And").Replace(" ", "");
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
