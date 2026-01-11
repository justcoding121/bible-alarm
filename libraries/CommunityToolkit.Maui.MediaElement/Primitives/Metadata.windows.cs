using CommunityToolkit.Maui.Interfaces;
using Windows.Media;
using Windows.Storage.Streams;

namespace CommunityToolkit.Maui.Primitives;

sealed class Metadata
{
    readonly IMediaElement? mediaElement;
    readonly SystemMediaTransportControls? systemMediaControls;
    readonly IDispatcher dispatcher;
    /// <summary>
    /// Initializes a new instance of the <see cref="Metadata"/> class.
    /// </summary>
    public Metadata(SystemMediaTransportControls systemMediaTransportControls, IMediaElement mediaElement, IDispatcher dispatcher)
    {
        this.mediaElement = mediaElement;
        this.dispatcher = dispatcher;
        systemMediaControls = systemMediaTransportControls;
        systemMediaControls.ButtonPressed += OnSystemMediaControlsButtonPressed;
    }


    void OnSystemMediaControlsButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        if (mediaElement is null)
        {
            return;
        }

        if (args.Button == SystemMediaTransportControlsButton.Play)
        {
            if (dispatcher.IsDispatchRequired)
            {
                dispatcher.Dispatch(mediaElement.Play);
            }
            else
            {
                mediaElement.Play();
            }
        }
        else if (args.Button == SystemMediaTransportControlsButton.Pause)
        {
            if (dispatcher.IsDispatchRequired)
            {
                dispatcher.Dispatch(mediaElement.Pause);
            }
            else
            {
                mediaElement.Pause();
            }
        }
        // Note: Next/Previous button handling is now done by WindowsSmtcService in the app
        // which dispatches messages to trigger Fluxor actions for navigation
    }

    /// <summary>
    /// Sets the metadata for the given MediaElement.
    /// </summary>
    public void SetMetadata(IMediaElement mp)
    {
        if (systemMediaControls is null || mediaElement is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(mp.MetadataArtworkUrl))
        {
            systemMediaControls.DisplayUpdater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(mp.MetadataArtworkUrl ?? string.Empty));
        }
        systemMediaControls.DisplayUpdater.Type = MediaPlaybackType.Music;
        // Fix: Title and Artist were swapped - correct mapping
        systemMediaControls.DisplayUpdater.MusicProperties.Title = mp.MetadataTitle ?? string.Empty;
        systemMediaControls.DisplayUpdater.MusicProperties.Artist = mp.MetadataArtist ?? string.Empty;
        systemMediaControls.DisplayUpdater.Update();
    }
}