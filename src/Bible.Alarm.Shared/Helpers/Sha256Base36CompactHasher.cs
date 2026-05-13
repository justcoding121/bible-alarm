#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Compact SHA256-derived base36 tokens for Windows toast notification IDs (non-cryptographic uniqueness).
/// </summary>
public static class Sha256Base36CompactHasher
{
    /// <summary>Returns exactly 11 characters for any input instant.</summary>
    public static string To11CharacterToken(DateTimeOffset dateTime)
    {
        var dateStr = dateTime.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var bytes = Encoding.UTF8.GetBytes(dateStr);
        var hashBytes = SHA256.HashData(bytes);

        const string base36Chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        var hash = new StringBuilder();

        ulong value = 0;
        for (var i = 0; i < 6 && i < hashBytes.Length; i++)
        {
            value = (value << 8) | hashBytes[i];
        }

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

        return hash.ToString().PadLeft(11, '0')[..11];
    }
}
