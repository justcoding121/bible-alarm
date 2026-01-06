#nullable enable

using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Validates whether foreground service operations can proceed.
/// 
/// THREAD SAFETY: All methods in this class access state without internal locking.
/// They MUST be called from within the ForegroundServiceCoordinator's lock to ensure thread safety.
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

    /// <summary>
    /// Checks if the app is already in foreground or has an active foreground service.
    /// If so, we don't need to start a new foreground service.
    /// </summary>
    public static bool ShouldSkipForegroundServiceStart(ForegroundServiceStateManager state)
    {
        // If app is in foreground, we don't need foreground service
        if (App.IsInForeground)
        {
            logger.Debug("Skipping foreground service start - app is already in foreground");
            return true;
        }
        
        // If MediaElement is active, it already has foreground service
        if (IsMediaElementActive(state))
        {
            logger.Debug("Skipping foreground service start - MediaElement is active and has foreground service");
            return true;
        }
        
        // If Android Auto already owns foreground service, we don't need to start another
        if (state.CurrentOwner == ForegroundServiceCoordinator.ForegroundServiceOwner.AndroidAuto)
        {
            logger.Debug("Skipping foreground service start - Android Auto already owns foreground service");
            return true;
        }
        
        return false;
    }
}
