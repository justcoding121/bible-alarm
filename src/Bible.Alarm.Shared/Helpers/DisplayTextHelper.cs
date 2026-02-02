#nullable enable
using System.Text;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Normalizes UI display text to a single line:
/// - trims leading/trailing whitespace
/// - replaces newlines/tabs/other whitespace with single spaces
/// - collapses repeated whitespace
/// </summary>
public static class DisplayTextHelper
{
    public static string NormalizeSingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Normalize common non-breaking space to a regular space.
        var input = value.Replace('\u00A0', ' ');

        var sb = new StringBuilder(input.Length);
        var inWhitespace = false;

        foreach (var ch in input)
        {
            // Treat any whitespace/control char as whitespace for UI display.
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                if (!inWhitespace)
                {
                    sb.Append(' ');
                    inWhitespace = true;
                }
                continue;
            }

            sb.Append(ch);
            inWhitespace = false;
        }

        return sb.ToString().Trim();
    }
}

