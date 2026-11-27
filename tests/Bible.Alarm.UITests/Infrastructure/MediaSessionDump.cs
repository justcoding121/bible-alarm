#nullable enable

using System.Text.RegularExpressions;

namespace Bible.Alarm.UITests.Infrastructure;

public sealed record MediaSessionSnapshot(int SessionCount, int? PlaybackState, string? Title);

/// <summary>
/// Reads <c>dumpsys media_session</c>. Playback state 3 is playing, 2 is paused, 1 is stopped.
/// </summary>
public static partial class MediaSessionDump
{
    public static MediaSessionSnapshot Parse(string dump, string packageName)
    {
        var sessionCount = 0;
        int? state = null;
        string? title = null;
        var inside = false;
        foreach (var raw in dump.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Contains("package=" + packageName, StringComparison.Ordinal))
            {
                sessionCount++;
                inside = true;
            }
            else if (line.Contains(packageName + "/", StringComparison.Ordinal))
            {
                inside = true;
            }
            else if (inside && line.StartsWith("package=", StringComparison.Ordinal) &&
                     !line.Contains(packageName, StringComparison.Ordinal))
            {
                inside = false;
            }

            if (!inside)
            {
                continue;
            }

            var stateMatch = StatePattern().Match(line);
            if (stateMatch.Success)
            {
                state = int.Parse(stateMatch.Groups[1].Value);
            }

            var titleMatch = TitlePattern().Match(line);
            if (titleMatch.Success)
            {
                title = titleMatch.Groups[1].Value.Trim();
            }
        }

        return new MediaSessionSnapshot(sessionCount, state, title);
    }

    [GeneratedRegex(@"state=(?:[A-Z_]+\()?(\d+)\)?")]
    private static partial Regex StatePattern();

    [GeneratedRegex(@"description=([^,}\r\n]+)")]
    private static partial Regex TitlePattern();
}
