#nullable enable

using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views.Shared;

namespace Bible.Alarm.Services.UI.ToastLayoutHelpers;

/// <summary>
/// Bottom inset so in-app toasts sit above the mini playback bar when it is visible.
/// Uses the actual measured height reported by MiniPlaybackBar.SizeChanged,
/// falling back to an estimate if the bar hasn't been laid out yet.
/// </summary>
internal static class ToastMiniBarInsetHelper
{
    private const double FallbackMiniPlaybackBarHeightDip = 80;
    private const double GapAboveMiniBarDip = 16;

    public static double GetBottomInsetDip()
    {
        var vm = MiniPlaybackBarViewModel.Instance;
        if (vm == null || !vm.IsVisible)
        {
            return 0;
        }

        var barHeight = MiniPlaybackBar.LastMeasuredHeight > 0
            ? MiniPlaybackBar.LastMeasuredHeight
            : FallbackMiniPlaybackBarHeightDip;

        return barHeight + GapAboveMiniBarDip;
    }
}
