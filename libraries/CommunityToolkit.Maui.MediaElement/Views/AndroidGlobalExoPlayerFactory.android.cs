#nullable enable

using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using CommunityToolkit.Maui.Interfaces;
using Microsoft.Maui;

namespace CommunityToolkit.Maui.Views;

internal static class AndroidGlobalExoPlayerFactory
{
    static bool globalExoPlayerCreated;
    static readonly Lock globalExoPlayerLock = new();
    static PlatformMediaElement? globalPlayer;

    internal static PlatformMediaElement CreateOrReuse(IMauiContext mauiContext, IPlayerListener listener)
    {
        lock (globalExoPlayerLock)
        {
            if (globalExoPlayerCreated && globalPlayer is not null)
            {
                return globalPlayer;
            }

            if (globalExoPlayerCreated)
            {
                globalExoPlayerCreated = false;
                globalPlayer = null;
            }

            globalExoPlayerCreated = true;
        }

        var context = mauiContext.Context;
        if (context == null)
        {
            Reset();
            throw new InvalidOperationException("Cannot create ExoPlayer - MauiContext.Context is null. Ensure bootstrap has completed before calling PrepareAndPlayAsync.");
        }

        Serilog.Log.Information("MediaManager", $"MediaManager: Creating ExoPlayer directly via ExoPlayerBuilder. Context: {context.GetType().FullName}");

        var exoPlayerBuilder = new ExoPlayerBuilder(context);
        // Disable the default 3-second threshold that causes seekToPrevious() to restart
        // the current track instead of transitioning to the previous media item.
        // With long.MaxValue, seekToPrevious() always goes to the previous item.
        exoPlayerBuilder.SetMaxSeekToPreviousPositionMs(long.MaxValue);
        var exoPlayer = exoPlayerBuilder.Build();
        var player = exoPlayer ?? throw new InvalidOperationException("Failed to create ExoPlayer");
        player.AddListener(listener);

        // Headless audio-only config (critical for no surface/view)
        player.SetVideoSurfaceView(null);

        Serilog.Log.Information("MediaManager", $"MediaManager: ExoPlayer created headlessly. Type: {player.GetType().FullName}");

        lock (globalExoPlayerLock)
        {
            globalPlayer = player;
        }

        return player;
    }

    internal static void Reset()
    {
        lock (globalExoPlayerLock)
        {
            globalExoPlayerCreated = false;

            try { globalPlayer?.Stop(); } catch (Exception) { /* Best-effort reset; player may be in bad state. */ }
            try { globalPlayer?.Release(); } catch (Exception) { /* Same: ignore release errors during global teardown. */ }

            globalPlayer = null;
        }
    }
}

