#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Builds Windows scheduled/live toast notification IDs with the platform 16-character ceiling.
/// </summary>
public static class WindowsToastNotificationUniqueIdComposer
{
    public static string Compose(int scheduleId, DateTimeOffset fireDate)
    {
        var dateHash = Sha256Base36CompactHasher.To11CharacterToken(fireDate);
        var uniqueId = $"{scheduleId}_{dateHash}";

        if (uniqueId.Length > 16)
        {
            var maxHashLength = 16 - scheduleId.ToString(System.Globalization.CultureInfo.InvariantCulture).Length - 1;
            dateHash = dateHash[..Math.Min(maxHashLength, dateHash.Length)];
            uniqueId = $"{scheduleId}_{dateHash}";
        }

        return uniqueId;
    }
}
