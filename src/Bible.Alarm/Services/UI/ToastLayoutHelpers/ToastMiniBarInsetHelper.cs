#nullable enable

using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Services.UI.ToastLayoutHelpers;

/// <summary>
/// Bottom inset so in-app toasts sit above the mini playback bar when it is visible.
/// </summary>
internal static class ToastMiniBarInsetHelper
{
    /// <summary>
    /// Matches MiniPlaybackBar: 3 progress + controls row (8 + 48 + 8 padding/height).
    /// </summary>
    private const double MiniPlaybackBarHeightDip = 72;

    private const double GapBetweenToastAndMiniBarDip = 8;

    /// <summary>
    /// Extra device-independent space to reserve above the screen bottom when the mini bar is shown.
    /// </summary>
    public static double GetBottomInsetDip()
    {
        var vm = MiniPlaybackBarViewModel.Instance;
        if (vm == null || !vm.IsVisible)
        {
            return 0;
        }

        return MiniPlaybackBarHeightDip + GapBetweenToastAndMiniBarDip;
    }
}
