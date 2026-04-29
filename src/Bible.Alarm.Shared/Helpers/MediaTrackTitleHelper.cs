#nullable enable

using System.Net;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Decodes track/publication titles from JSON (HTML entities and non-breaking spaces).
/// </summary>
public static class MediaTrackTitleHelper
{
    public const string UnknownTitle = "Unknown";

    public static string DecodeHtmlTitle(string? rawTitle) =>
        rawTitle != null ? WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ') : UnknownTitle;

    /// <summary>Same decode as <see cref="DecodeHtmlTitle"/> but returns null when <paramref name="rawTitle"/> is null (for optional API names).</summary>
    public static string? DecodeHtmlTitleNullable(string? rawTitle) =>
        rawTitle is null ? null : WebUtility.HtmlDecode(rawTitle).Replace('\u00A0', ' ');
}
