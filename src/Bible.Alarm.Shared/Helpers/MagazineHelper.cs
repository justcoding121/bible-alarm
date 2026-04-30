#nullable enable

using System;
using System.Collections.Generic;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper for magazine publications (Watchtower, Awake!).
/// Handles section code parsing, issue code generation, and section name building.
/// </summary>
public static class MagazineHelper
{
    public const int MagazineStartYear = 2008;
    private const int WatchtowerFormatChangeYear = 2016;

    public static int MagazineEndYear => DateTime.UtcNow.Year + 1;

    public static bool IsMagazinePublicationCode(string? publicationCode)
    {
        return IsWatchtowerMagazineCode(publicationCode) || IsAwakeMagazineCode(publicationCode);
    }

    public static bool IsWatchtowerMagazineCode(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode) || publicationCode.Length < 5)
            return false;

        if (!publicationCode.StartsWith("w", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(publicationCode.AsSpan(1), out var year)
               && year >= MagazineStartYear
               && year <= MagazineEndYear;
    }

    public static bool IsAwakeMagazineCode(string? publicationCode)
    {
        if (string.IsNullOrEmpty(publicationCode) || publicationCode.Length < 5)
            return false;

        if (!publicationCode.StartsWith("g", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(publicationCode.AsSpan(1), out var year)
               && year >= MagazineStartYear
               && year <= MagazineEndYear;
    }

    /// <summary>
    /// Extracts the year from a magazine publication code (e.g. "w2008" → 2008).
    /// </summary>
    public static int GetYear(string publicationCode)
    {
        return int.Parse(publicationCode.AsSpan(1));
    }

    /// <summary>
    /// Parses a section code like "20080101-wp" into (ApiPubCode: "wp", IssueCode: "20080101").
    /// Splits on the last '-'.
    /// </summary>
    public static (string ApiPubCode, string IssueCode) ParseSectionCode(string sectionCode)
    {
        var lastDash = sectionCode.LastIndexOf('-');
        if (lastDash < 0)
            throw new ArgumentException($"Invalid magazine section code (no dash): {sectionCode}", nameof(sectionCode));

        var issueCode = sectionCode[..lastDash];
        var apiPubCode = sectionCode[(lastDash + 1)..];
        return (apiPubCode, issueCode);
    }

    /// <summary>
    /// Builds a section code from API pub code and issue code: "20080101" + "wp" → "20080101-wp".
    /// </summary>
    public static string BuildSectionCode(string apiPubCode, string issueCode)
    {
        return $"{issueCode}-{apiPubCode}";
    }

    /// <summary>
    /// Builds a section name by combining pubName and formattedDate from the API response.
    /// Both values are HTML-decoded and non-breaking spaces are replaced.
    /// </summary>
    public static string BuildSectionName(string? pubName, string? formattedDate)
    {
        var decodedPubName = MediaTrackTitleHelper.DecodeHtmlTitleNullable(pubName) ?? "";
        var decodedDate = MediaTrackTitleHelper.DecodeHtmlTitleNullable(formattedDate) ?? "";

        if (string.IsNullOrEmpty(decodedDate))
            return decodedPubName;
        if (string.IsNullOrEmpty(decodedPubName))
            return decodedDate;

        return $"{decodedPubName} — {decodedDate}";
    }

    /// <summary>
    /// Returns all possible (apiPubCode, issueCode) pairs for a given year and magazine type.
    /// For Watchtower before 2016: yyyyMM01 (public/wp) and yyyyMM15 (study/w) for each month.
    /// For Watchtower from 2016: yyyyMM for both w and wp.
    /// For Awake: yyyyMM for g.
    /// </summary>
    public static List<(string ApiPubCode, string IssueCode)> GetPossibleIssues(int year, bool isWatchtower)
    {
        var issues = new List<(string, string)>();

        if (isWatchtower)
        {
            if (year < WatchtowerFormatChangeYear)
            {
                for (int month = 1; month <= 12; month++)
                {
                    var mm = month.ToString("D2");
                    issues.Add(("wp", $"{year}{mm}01"));
                    issues.Add(("w", $"{year}{mm}15"));
                }
            }
            else
            {
                for (int month = 1; month <= 12; month++)
                {
                    var mm = month.ToString("D2");
                    issues.Add(("w", $"{year}{mm}"));
                    issues.Add(("wp", $"{year}{mm}"));
                }
            }
        }
        else
        {
            for (int month = 1; month <= 12; month++)
            {
                var mm = month.ToString("D2");
                issues.Add(("g", $"{year}{mm}"));
            }
        }

        return issues;
    }

    /// <summary>
    /// Returns the DB publication code for a given API pub code and year.
    /// Watchtower (w, wp) → "w{year}", Awake (g) → "g{year}".
    /// </summary>
    public static string GetPublicationCode(string apiPubCode, int year)
    {
        var prefix = apiPubCode.Equals("g", StringComparison.OrdinalIgnoreCase) ? "g" : "w";
        return $"{prefix}{year}";
    }

    /// <summary>
    /// Extracts the year from an issue code (e.g. "20080101" → 2008, "201601" → 2016).
    /// </summary>
    public static int GetYearFromIssueCode(string issueCode)
    {
        return int.Parse(issueCode.AsSpan(0, 4));
    }
}
