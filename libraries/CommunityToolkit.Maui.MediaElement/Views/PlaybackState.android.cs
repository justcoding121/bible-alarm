#nullable enable

using CommunityToolkit;

namespace CommunityToolkit.Maui.Views;

internal static class PlaybackState
{
    public const int StateBuffering = 6;
    public const int StateConnecting = 8;
    public const int StateFailed = 7;
    public const int StateFastForwarding = 4;
    public const int StateNone = 0;
    public const int StatePaused = 2;
    public const int StatePlaying = 3;
    public const int StateRewinding = 5;
    public const int StateSkippingToNext = 10;
    public const int StateSkippingToPrevious = 9;
    public const int StateSkippingToQueueItem = 11;
    public const int StateStopped = 1;
    public const int StateError = 7;
}

