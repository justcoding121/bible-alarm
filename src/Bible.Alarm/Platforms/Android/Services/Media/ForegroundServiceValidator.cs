#nullable enable

using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Validates whether foreground service operations can proceed.
/// </summary>
internal static class ForegroundServiceValidator
{
    private static readonly ILogger logger = Log.ForContext(typeof(ForegroundServiceValidator));

    public static bool CanStartAndroidAutoForeground(ForegroundServiceStateManager state)
    {
        if (!state.IsAndroidAutoConnected)
        {
            logger.Debug("Cannot start Android Auto foreground service - Android Auto is not connected");
            return false;
        }

        if (state.IsMediaElementPlaying || state.CurrentOwner == ForegroundServiceCoordinator.ForegroundServiceOwner.MediaElement)
        {
            logger.Debug("Cannot start Android Auto foreground service - MediaElement is playing (isPlaying: {IsPlaying}, owner: {Owner})",
                state.IsMediaElementPlaying, state.CurrentOwner);
            return false;
        }

        return true;
    }

    public static bool ShouldUpdateAndroidAutoForeground(ForegroundServiceStateManager state)
    {
        return state.CurrentOwner == ForegroundServiceCoordinator.ForegroundServiceOwner.AndroidAuto;
    }

    public static bool IsMediaElementActive(ForegroundServiceStateManager state)
    {
        return state.IsMediaElementPlaying || state.CurrentOwner == ForegroundServiceCoordinator.ForegroundServiceOwner.MediaElement;
    }
}
