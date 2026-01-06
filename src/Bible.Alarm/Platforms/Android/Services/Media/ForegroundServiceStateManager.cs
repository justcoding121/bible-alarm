#nullable enable

using Android.App;
using Serilog;

namespace Bible.Alarm.Platforms.Android.Services.Media;

/// <summary>
/// Manages the state of foreground service ownership.
/// </summary>
internal sealed class ForegroundServiceStateManager
{
    private static readonly ILogger logger = Log.ForContext<ForegroundServiceStateManager>();
    
    public ForegroundServiceCoordinator.ForegroundServiceOwner CurrentOwner { get; private set; } = ForegroundServiceCoordinator.ForegroundServiceOwner.None;
    public bool IsMediaElementPlaying { get; private set; }
    public bool IsAndroidAutoConnected { get; private set; }
    public Service? AndroidAutoService { get; private set; }

    public void SetOwner(ForegroundServiceCoordinator.ForegroundServiceOwner owner)
    {
        if (CurrentOwner != owner)
        {
            logger.Debug("Foreground service owner changed: {OldOwner} -> {NewOwner}", CurrentOwner, owner);
            CurrentOwner = owner;
        }
    }

    public void SetMediaElementPlaying(bool isPlaying)
    {
        if (IsMediaElementPlaying != isPlaying)
        {
            logger.Debug("MediaElement playing state changed: {OldState} -> {NewState}", IsMediaElementPlaying, isPlaying);
            IsMediaElementPlaying = isPlaying;
        }
    }

    public void SetAndroidAutoConnected(bool connected, Service? service = null)
    {
        if (IsAndroidAutoConnected != connected)
        {
            logger.Debug("Android Auto connection state changed: {OldState} -> {NewState}", IsAndroidAutoConnected, connected);
            IsAndroidAutoConnected = connected;
        }
        
        AndroidAutoService = service;
    }

    public void ClearAndroidAutoService()
    {
        AndroidAutoService = null;
    }
}
