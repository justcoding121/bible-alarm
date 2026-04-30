#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Services.Media.PlaylistServiceHelpers;

/// <summary>
/// Helper for applying disc-style download code to track metadata (e.g. iam melody releases).
/// </summary>
public static class PlaylistMetadataHelper
{
    public static void TryApplyDiscStyleDownloadCode(TrackMetadata metadata)
    {
        var sectionCode = SectionCodeHelper.Normalize(metadata.SectionCode);
        if (string.IsNullOrWhiteSpace(sectionCode))
        {
            return;
        }

        if (!sectionCode.Contains('-'))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.PublicationCode) ||
            !sectionCode.StartsWith(metadata.PublicationCode + "-", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parts = sectionCode.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return;
        }

        var suffix = parts[^1];
        if (string.IsNullOrEmpty(suffix) || !suffix.All(char.IsDigit) || suffix.All(c => c == '0'))
        {
            return;
        }

        metadata.DownloadCode = sectionCode;
        if (int.TryParse(metadata.TrackCode, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedTrackNum))
        {
            metadata.OriginalTrackCode = parsedTrackNum;
        }
    }
}
