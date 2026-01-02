#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Audio;
using Bible.Alarm.Services.Media.AudioPlayerHelpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Essentials;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;
#if IOS
using Bible.Alarm.Platforms.iOS.Helpers;
#endif

namespace Bible.Alarm.Services.Media;

public sealed class AudioPlayer : IAudioPlayer, IDisposable
{
    private readonly ILogger logger;
    private readonly IMediaElementService mediaElementService;
    private readonly IDisplayMetadataService displayMetadataService;
    private readonly IDispatcher dispatcher;
#if ANDROID
    private readonly IAndroidPlayerNotificationService? androidPlayerNotificationService;
#endif

    private AudioPlayerTrack? currentTrack;
    private TaskCompletionSource<bool>? mediaOpenedCompletionSource;
    // MediaElement instance - populated in PrepareAsync
    private MediaElement? mediaElement;

    // Helper classes
    private readonly AudioPlayerStateManager stateManager;
    private readonly AudioPlayerMetadataHandler metadataHandler;
    private readonly AudioPlayerPositionTracker positionTracker;
    private readonly EventHandlerManager eventHandlerManager;
    private readonly PlaybackController playbackController;
    private readonly MediaElementManager mediaElementManager;

    public TimeSpan? CurrentPosition => mediaElement?.Position;
    public TimeSpan Duration => mediaElement?.Duration ?? TimeSpan.Zero;
    public PlayStatus Status => stateManager.Status;

    /// <summary>
    /// Gets the actual current state of the MediaElement, not just the cached Status
    /// This checks the MediaElement's CurrentState property directly
    /// </summary>
    public bool IsActuallyPlayingOrPaused
    {
        get
        {
            // If we're resetting, don't check the actual state (it might be in transition)
            if (stateManager.IsResetting)
            {
                return false;
            }

            // If source is null, MediaElement is not playing anything
            if (mediaElement?.Source == null)
            {
                return false;
            }

            // CurrentState should be accessed on main thread, but for a simple check we'll do it directly
            // If this causes issues, we may need to make this async
            var actualState = mediaElement.CurrentState;

            // Only return true if actually playing, paused, or buffering
            // If it's Stopped, None, Opening, or Failed, return false
            return actualState is MediaElementState.Playing or
                   MediaElementState.Paused or
                   MediaElementState.Buffering;
        }
    }

    public event EventHandler<EventArgs>? MediaEnded;
    public event EventHandler<EventArgs>? MediaFailed;

    public AudioPlayer(ILogger logger, IMediaElementService mediaElementService, IDisplayMetadataService displayMetadataService, IDispatcher dispatcher
#if ANDROID
        , IAndroidPlayerNotificationService? androidPlayerNotificationService = null
#endif
        )
    {
        this.logger = logger;
        this.mediaElementService = mediaElementService;
        this.displayMetadataService = displayMetadataService;
        this.dispatcher = dispatcher;
#if ANDROID
        this.androidPlayerNotificationService = androidPlayerNotificationService;
#endif

        // Initialize helper classes
        stateManager = new AudioPlayerStateManager(logger, dispatcher);
        metadataHandler = new AudioPlayerMetadataHandler(logger, displayMetadataService, dispatcher);
        positionTracker = new AudioPlayerPositionTracker(logger, dispatcher);

        eventHandlerManager = new EventHandlerManager(
            logger,
            stateManager,
            metadataHandler,
            positionTracker,
            () => CurrentPosition,
            () => Duration,
            (status) => stateManager.Status = status,
            (e) => MediaEnded?.Invoke(this, e),
            (e) => MediaFailed?.Invoke(this, e),
            () => mediaOpenedCompletionSource,
            () => currentTrack);

        playbackController = new PlaybackController(logger, stateManager, () => mediaElement);

        mediaElementManager = new MediaElementManager(
            logger,
            mediaElementService,
            stateManager,
            positionTracker,
            eventHandlerManager
#if ANDROID
            , androidPlayerNotificationService
#endif
            );

        // MediaElement will be initialized lazily when first accessed
        // Event handlers will be attached in PrepareAsync

        // MediaElement is now a singleton for all platforms - do not register for DestroyMediaElementMessage
        // MediaElement will live for the entire app process lifetime
    }

    public async Task PrepareAsync(AudioPlayerTrack track, bool isFirstTrack = false, bool isLastTrack = false)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (string.IsNullOrEmpty(track.Uri))
        {
            throw new ArgumentException("Track URI cannot be null or empty", nameof(track));
        }

        currentTrack = track;
        mediaOpenedCompletionSource = new TaskCompletionSource<bool>();

        // Prepare MediaElement using manager
        mediaElement = await mediaElementManager.PrepareAsync(mediaElement, track, isFirstTrack, isLastTrack);

        // Wait for media to open (with timeout)
        // 5 second timeout
        var timeoutTask = Task.Delay(5000);
        var completedTask = await Task.WhenAny(mediaOpenedCompletionSource.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            logger.Warning("Timeout waiting for media to open");
        }
    }

    public Task PlayAsync() => playbackController.PlayAsync();

    public Task PauseAsync() => playbackController.PauseAsync();

    public Task ResumeAsync() => playbackController.ResumeAsync();

    public Task StopAsync() => playbackController.StopAsync();

    public async Task ResetAsync()
    {
        await mediaElementManager.ResetAsync(mediaElement, () =>
        {
            currentTrack = null;
            mediaOpenedCompletionSource?.TrySetCanceled();
            mediaOpenedCompletionSource = null;
            stateManager.Reset();
        });
    }

    public Task SeekToAsync(TimeSpan position) => playbackController.SeekToAsync(position);


    private bool isDisposed;

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Unsubscribe from current MediaElement if it exists
        if (mediaElement != null)
        {
            eventHandlerManager.UnsubscribeFromMediaElement(mediaElement);
        }

        // All injected services (_mediaElementService, _displayMetadataService, _dispatcher, 
        // _androidPlayerNotificationService) are singletons, so don't dispose them
    }

    // MediaElement is now a singleton for all platforms - it is never destroyed during app lifetime
    // No DestroyMediaElementMessage handling needed
}
