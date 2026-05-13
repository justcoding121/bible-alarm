#nullable enable

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Cross-platform visibility rule for the schedule-page music strip (inverse of “this Bible publication is treated as music content”).
/// </summary>
public static class MusicSelectionStripVisibilityGate
{
    /// <summary>
    /// Returns whether the music-selection rows should show for the current Bible publication selection.
    /// </summary>
    public static bool ShouldShowMusicSelectionRow(bool biblePublicationIsMusic, string? biblePublicationCode)
    {
        var isMusicPublication = biblePublicationIsMusic
                                 || (!string.IsNullOrWhiteSpace(biblePublicationCode)
                                     && JwSourceHelper.MusicFlagPublicationCodes.Contains(biblePublicationCode));

        return !isMusicPublication;
    }
}
