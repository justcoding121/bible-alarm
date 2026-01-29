#nullable enable

using Android.OS;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;
using CommunityToolkit.Maui.Interfaces;
using Microsoft.Maui;
using Application = Android.App.Application;

namespace CommunityToolkit.Maui.Core.Views;

internal static class AndroidGlobalExoPlayerFactory
{
    static bool globalExoPlayerCreated;
    static readonly Lock globalExoPlayerLock = new();
    static PlatformMediaElement? globalPlayer;
    static MediaSession? globalSession;

    internal static (PlatformMediaElement Player, MediaSession Session) CreateOrReuse(IMauiContext mauiContext, IPlayerListener listener)
    {
        lock (globalExoPlayerLock)
        {
            if (globalExoPlayerCreated && globalPlayer is not null && globalSession is not null)
            {
                return (globalPlayer, globalSession);
            }

            if (globalExoPlayerCreated)
            {
                // Defensive: created flag is set but shared instances are missing. Reset and recreate.
                globalExoPlayerCreated = false;
                globalPlayer = null;
                globalSession = null;
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

        var exoPlayer = new ExoPlayerBuilder(context).Build();
        var player = exoPlayer ?? throw new InvalidOperationException("Failed to create ExoPlayer");
        player.AddListener(listener);

        // Headless audio-only config (critical for no surface/view)
        player.SetVideoSurfaceView(null); // No surface ever

        Serilog.Log.Information("MediaManager", $"MediaManager: ExoPlayer created headlessly. Type: {player.GetType().FullName}");

        var mediaSession = new MediaSession.Builder(Platform.AppContext, player);
        mediaSession.SetId(Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..8]);

        var session = mediaSession.Build() ?? throw new InvalidOperationException("Session cannot be null");
        ArgumentNullException.ThrowIfNull(session.Id);

        lock (globalExoPlayerLock)
        {
            globalPlayer = player;
            globalSession = session;
        }

        return (player, session);
    }

    internal static void Reset()
    {
        lock (globalExoPlayerLock)
        {
            globalExoPlayerCreated = false;
            globalPlayer = null;
            globalSession = null;
        }
    }
}

