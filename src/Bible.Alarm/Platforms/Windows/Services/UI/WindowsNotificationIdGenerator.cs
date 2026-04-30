#nullable enable

using System.Security.Cryptography;
using System.Text;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

internal static class WindowsNotificationIdGenerator
{
    internal static string BuildUniqueId(int scheduleId, DateTimeOffset fireDate)
    {
        // Create unique ID for this occurrence: Windows has a 16-character limit for notification IDs
        // Use format: "{scheduleId}_{hash}" where hash is a short representation of the date/time
        // ScheduleId can be up to 4 digits (9999), so we have ~12 chars for the hash
        var dateHash = GetShortDateHash(fireDate);
        var uniqueId = $"{scheduleId}_{dateHash}";

        // Ensure ID doesn't exceed 16 characters (Windows limit)
        if (uniqueId.Length > 16)
        {
            // If scheduleId is too long, truncate the hash
            var maxHashLength = 16 - scheduleId.ToString().Length - 1; // -1 for underscore
            dateHash = dateHash[..Math.Min(maxHashLength, dateHash.Length)];
            uniqueId = $"{scheduleId}_{dateHash}";
        }

        return uniqueId;
    }

    /// <summary>
    /// Generates a short hash (11 characters) from a DateTimeOffset for use in notification IDs.
    /// Windows notification IDs have a 16-character limit, so we need a compact representation.
    /// Format: ScheduleId (up to 4 digits) + "_" (1 char) + DateHash (11 chars) = 16 chars max
    /// Uses SHA256 (first 6 bytes) for uniqueness; not used in a cryptographic context.
    /// </summary>
    private static string GetShortDateHash(DateTimeOffset dateTime)
    {
        var dateStr = dateTime.ToString("yyyyMMddHHmmss");
        var bytes = Encoding.UTF8.GetBytes(dateStr);
        var hashBytes = SHA256.HashData(bytes);

        // Convert hash to base36 (0-9, a-z) for a compact 11-character representation
        const string base36Chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        var hash = new StringBuilder();

        // Use first 6 bytes of hash (48 bits) to generate 11 base36 characters
        // 36^11 is much larger than 2^48, so we have good distribution
        ulong value = 0;
        for (int i = 0; i < 6 && i < hashBytes.Length; i++)
        {
            value = (value << 8) | hashBytes[i];
        }

        // Convert to base36
        if (value == 0)
        {
            hash.Append('0');
        }
        else
        {
            while (value > 0 && hash.Length < 11)
            {
                hash.Insert(0, base36Chars[(int)(value % 36)]);
                value /= 36;
            }
        }

        // Pad to exactly 11 characters for consistency
        return hash.ToString().PadLeft(11, '0')[..11];
    }
}

