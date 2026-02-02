#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper class for sorting Bible publications by priority.
/// Priority order: nwt (2013 NWT) first, then bi12 (1984 NWT), then all others sorted by name.
/// </summary>
public static class PublicationSortHelper
{
    /// <summary>
    /// Gets the sort priority for a publication code.
    /// nwt (2013 NWT) = 0, bi12 (1984 NWT) = 1, others = 2
    /// </summary>
    /// <param name="code">The publication code (case-insensitive)</param>
    /// <returns>Priority value (lower = higher priority)</returns>
    public static int GetPublicationSortPriority(string code)
    {
        return PublicationCodeHelper.GetPublicationSortPriority(code);
    }

    /// <summary>
    /// Sorts a collection of publications by priority (nwt first, then bi12, then others by name).
    /// </summary>
    /// <typeparam name="T">Type that has a Code property (e.g., BiblePublication, PublicationListViewItemModel)</typeparam>
    /// <param name="publications">Collection of publications to sort</param>
    /// <param name="getCode">Function to extract the publication code from each item</param>
    /// <param name="getName">Optional function to extract the name for secondary sorting (defaults to code if not provided)</param>
    /// <returns>Sorted collection</returns>
    public static IEnumerable<T> SortByPriority<T>(
        IEnumerable<T> publications,
        Func<T, string> getCode,
        Func<T, string>? getName = null)
    {
        if (publications == null)
            return Enumerable.Empty<T>();

        return publications
            .OrderBy(p => getCode(p), PublicationCodeHelper.PublicationCodeComparer)
            .ThenBy(p => getName != null ? getName(p) : getCode(p))
            .ToList();
    }

    /// <summary>
    /// Sorts a dictionary of publications by priority (nwt first, then bi12, then others by name).
    /// </summary>
    /// <typeparam name="T">Type of publication value</typeparam>
    /// <param name="publications">Dictionary of publications to sort</param>
    /// <param name="getName">Optional function to extract the name for secondary sorting (defaults to key if not provided)</param>
    /// <returns>Sorted list of key-value pairs</returns>
    public static List<KeyValuePair<string, T>> SortByPriority<T>(
        Dictionary<string, T> publications,
        Func<T, string>? getName = null)
    {
        if (publications == null || publications.Count == 0)
            return new List<KeyValuePair<string, T>>();

        return publications
            .OrderBy(kvp => kvp.Key, PublicationCodeHelper.PublicationCodeComparer)
            .ThenBy(kvp => getName != null ? getName(kvp.Value) : kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Gets the priority publication codes in order.
    /// </summary>
    /// <returns>Array of priority publication codes</returns>
    public static string[] GetPriorityPublicationCodes()
    {
        return PublicationCodeHelper.GetPriorityPublicationCodes();
    }
}
