#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Loads per-language phrases that indicate audio-description track titles (from embedded JSON).
/// Use to filter out AD tracks by title when markers-based detection is not available.
/// </summary>
public static class AudioDescriptionTitlePhrases
{
    private const string ResourceName = "Bible.Alarm.Shared.Resources.AudioDescriptionTitlePhrases.json";

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> PhrasesByLanguage = new(LoadPhrases);

    /// <summary>
    /// Returns true if the title contains any of the known audio-description phrases for the given language.
    /// </summary>
    public static bool ContainsAudioDescriptionPhrase(string? languageCode, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var phrases = GetPhrasesForLanguage(languageCode);

        foreach (var phrase in phrases)
        {
            if (phrase.Length == 0)
            {
                continue;
            }

            if (title.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the list of phrases for the given language. Falls back to English (E) if the language has no entries.
    /// </summary>
    public static IReadOnlyList<string> GetPhrasesForLanguage(string? languageCode)
    {
        var dict = PhrasesByLanguage.Value;
        if (!string.IsNullOrWhiteSpace(languageCode) && dict.TryGetValue(languageCode.Trim(), out var list))
        {
            return list;
        }

        if (dict.TryGetValue("E", out var english))
        {
            return english;
        }

        return Array.Empty<string>();
    }

    private static Dictionary<string, IReadOnlyList<string>> LoadPhrases()
    {
        var assembly = typeof(AudioDescriptionTitlePhrases).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(stream);
        if (raw == null)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in raw)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || kvp.Value == null)
            {
                continue;
            }

            var list = new List<string>();
            foreach (var s in kvp.Value.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                list.Add(s.Trim());
            }

            result[kvp.Key.Trim()] = list;
        }

        return result;
    }
}
