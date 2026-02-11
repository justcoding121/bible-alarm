namespace Bible.Alarm.ViewModels.Shared.AlarmViewModelHelpers;

/// <summary>
/// Computes progress text for PlaybackViewModel preparation state.
/// </summary>
public static class PlaybackViewModelProgressTextHelper
{
    public static string GetProgressText(int loadedTracks, int totalTracks, long totalBytesDownloaded, long? totalBytesExpected)
    {
        if (loadedTracks < totalTracks)
        {
            if (totalBytesDownloaded > 0 && totalBytesExpected.HasValue && totalBytesExpected.Value > 0)
            {
                var percentage = (totalBytesDownloaded * 100.0) / totalBytesExpected.Value;
                return $"{percentage:F0}%";
            }
            return "0%";
        }
        return "100%";
    }
}
