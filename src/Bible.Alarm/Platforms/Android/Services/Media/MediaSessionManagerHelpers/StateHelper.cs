#nullable enable
using Android.Support.V4.Media.Session;

namespace Bible.Alarm.Platforms.Android.Services.Media.MediaSessionManagerHelpers;

/// <summary>
/// Helper methods for state name conversion and action descriptions.
/// </summary>
public static class StateHelper
{
    private static readonly string[] ActionsDescriptionNoneFallback = ["None"];

    /// <summary>
    /// Gets a human-readable name for a playback state.
    /// </summary>
    public static string GetStateName(int state)
    {
        return state switch
        {
            PlaybackStateCompat.StateNone => "StateNone",
            PlaybackStateCompat.StateStopped => "StateStopped",
            PlaybackStateCompat.StatePaused => "StatePaused",
            PlaybackStateCompat.StatePlaying => "StatePlaying",
            PlaybackStateCompat.StateFastForwarding => "StateFastForwarding",
            PlaybackStateCompat.StateRewinding => "StateRewinding",
            PlaybackStateCompat.StateBuffering => "StateBuffering",
            PlaybackStateCompat.StateError => "StateError",
            PlaybackStateCompat.StateConnecting => "StateConnecting",
            PlaybackStateCompat.StateSkippingToPrevious => "StateSkippingToPrevious",
            PlaybackStateCompat.StateSkippingToNext => "StateSkippingToNext",
            PlaybackStateCompat.StateSkippingToQueueItem => "StateSkippingToQueueItem",
            _ => $"Unknown({state})"
        };
    }

    /// <summary>
    /// Gets a description of playback actions.
    /// </summary>
    public static string GetActionsDescription(long actions)
    {
        var actionList = new List<string>();
        if ((actions & PlaybackStateCompat.ActionPlay) != 0) actionList.Add("Play");
        if ((actions & PlaybackStateCompat.ActionPause) != 0) actionList.Add("Pause");
        if ((actions & PlaybackStateCompat.ActionPlayPause) != 0) actionList.Add("PlayPause");
        if ((actions & PlaybackStateCompat.ActionSkipToNext) != 0) actionList.Add("Next");
        if ((actions & PlaybackStateCompat.ActionSkipToPrevious) != 0) actionList.Add("Previous");
        if ((actions & PlaybackStateCompat.ActionPlayFromMediaId) != 0) actionList.Add("PlayFromMediaId");
        return string.Join(", ", actionList.Count > 0 ? actionList : ActionsDescriptionNoneFallback);
    }

    /// <summary>
    /// Gets button state description from actions.
    /// </summary>
    public static string GetButtonStateFromActions(long actions)
    {
        var hasPlay = (actions & PlaybackStateCompat.ActionPlay) != 0;
        var hasPause = (actions & PlaybackStateCompat.ActionPause) != 0;

        if (hasPause) return "PAUSE_BUTTON_VISIBLE";
        if (hasPlay) return "PLAY_BUTTON_VISIBLE";
        return "NO_BUTTON";
    }
}
