namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Helper for formatting publication codes for display in the UI.
/// </summary>
public static class PublicationDisplayHelper
{
    /// <summary>
    /// Gets the short display name for a publication code.
    /// Format: "nwt" -> "NWT 2013", "bi12" -> "NWT 1984", others -> uppercase code
    /// </summary>
    /// <param name="publicationCode">The publication code (e.g., "nwt", "bi12")</param>
    /// <returns>The formatted display name, or uppercase code if no mapping exists</returns>
    public static string GetDisplayName(string? publicationCode)
    {
        if (string.IsNullOrWhiteSpace(publicationCode))
        {
            return string.Empty;
        }

        var code = publicationCode.ToLowerInvariant();
        return code switch
        {
            "nwt" => "NWT 2013",
            "bi12" => "NWT 1984",
            _ => publicationCode.ToUpperInvariant()
        };
    }
}

