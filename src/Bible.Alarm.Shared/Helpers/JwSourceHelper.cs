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
        "gnj"
    };

    /// <summary>
    /// Drama category codes used for seeding.
    /// </summary>
    public static HashSet<string> DramaCategoryCodes => new(StringComparer.OrdinalIgnoreCase)
    {
        "Dramas",
        "DramaticBibleReadings"
    };

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
                ["Dramas"] = dramaCodes
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
