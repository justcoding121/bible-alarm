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

    public static double GetBottomInsetDip() =>
        GetBottomInsetDip(MiniPlaybackBarViewModel.Instance, MiniPlaybackBar.LastMeasuredHeight);

    /// <summary>
    /// Parameterized inset calculation: the parameterless overload supplies the live singleton and
    /// <see cref="MiniPlaybackBar.LastMeasuredHeight"/>.
    /// </summary>
    internal static double GetBottomInsetDip(MiniPlaybackBarViewModel? vm, double lastMeasuredHeightDip)
    {
        if (vm == null || !vm.IsVisible)
        {
            return 0;
        }

        var barHeight = lastMeasuredHeightDip > 0
            ? lastMeasuredHeightDip
            : FallbackMiniPlaybackBarHeightDip;

        return barHeight + GapAboveMiniBarDip;
    }
}
