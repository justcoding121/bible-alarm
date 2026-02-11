#nullable enable
using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer.Source;
using Serilog;
using Uri = Android.Net.Uri;

namespace Bible.Alarm.Platforms.Android.Services.Media.AndroidPlayerNotificationHelpers;

/// <summary>
/// Handles creation of media sources and dummy items for Android player notifications.
/// </summary>
public sealed class MediaSourceBuilder(ILogger logger)
{
    /// <summary>
    /// Builds media sources for the player queue including dummy items for navigation buttons.
    /// Always includes previous and next dummy items so ExoPlayer's HasPreviousMediaItem /
    /// HasNextMediaItem are true, enabling lock screen notification Previous/Next buttons.
    /// Queue layout: [previous_dummy, current, next_dummy].
    /// </summary>
    public List<IMediaSource>? BuildMediaSources(DefaultDataSource.Factory dataSourceFactory, Uri androidUri)
    {
        var sources = new List<IMediaSource>();
        var currentSource = CreateCurrentMediaSource(dataSourceFactory, androidUri);
        if (currentSource == null)
        {
            return null;
        }

        // Always add previous dummy so HasPreviousMediaItem is true.
        var previousSource = CreateDummyMediaSource("bible_alarm_previous_dummy", dataSourceFactory);
        if (previousSource == null)
        {
            return null;
        }
        sources.Add(previousSource);

        // Add current item (always at index 1).
        sources.Add(currentSource);

        // Always add next dummy so HasNextMediaItem is true.
        var nextSource = CreateDummyMediaSource("bible_alarm_next_dummy", dataSourceFactory);
        if (nextSource == null)
        {
            return null;
        }
        sources.Add(nextSource);

        return sources;
    }

    /// <summary>
    /// Creates the current media source from the provided URI.
    /// </summary>
    private IMediaSource? CreateCurrentMediaSource(DefaultDataSource.Factory dataSourceFactory, Uri androidUri)
    {
        var currentItemBuilder = new MediaItem.Builder()
            .SetUri(androidUri)?
            .SetMediaId("bible_alarm_current");

        var currentItem = currentItemBuilder?.Build();
        if (currentItem == null)
        {
            logger.Error("Failed to build MediaItem for current item");
            return null;
        }

        var currentSource = new ProgressiveMediaSource.Factory(dataSourceFactory)
            .CreateMediaSource(currentItem);
        if (currentSource == null)
        {
            logger.Error("Failed to create MediaSource for current item");
        }

        return currentSource;
    }

    /// <summary>
    /// Creates a dummy media source using a silent MP3 file.
    /// </summary>
    private IMediaSource? CreateDummyMediaSource(string mediaId, DefaultDataSource.Factory dataSourceFactory)
    {
        var silentMp3Uri = GetSilentMp3Uri();
        if (string.IsNullOrEmpty(silentMp3Uri))
        {
            logger.Error("Failed to get silent MP3 URI for dummy item");
            return null;
        }

        var dummyUri = ParseUri(silentMp3Uri);
        if (dummyUri == null)
        {
            return null;
        }

        return CreateMediaSourceFromUri(dummyUri, mediaId, dataSourceFactory);
    }

    /// <summary>
    /// Creates a media source from a URI and media ID.
    /// </summary>
    private IMediaSource? CreateMediaSourceFromUri(Uri uri, string mediaId, DefaultDataSource.Factory dataSourceFactory)
    {
        var mediaItem = CreateMediaItem(uri, mediaId);
        if (mediaItem == null)
        {
            return null;
        }

        var source = new ProgressiveMediaSource.Factory(dataSourceFactory)
            .CreateMediaSource(mediaItem);
        if (source == null)
        {
            logger.Error("Failed to create MediaSource for item with MediaId: {MediaId}", mediaId);
        }

        return source;
    }

    /// <summary>
    /// Creates a MediaItem from URI and media ID.
    /// </summary>
    private MediaItem? CreateMediaItem(Uri uri, string mediaId)
    {
        var itemBuilder = new MediaItem.Builder()
            .SetUri(uri)?
            .SetMediaId(mediaId);

        var item = itemBuilder?.Build();
        if (item == null)
        {
            logger.Error("Failed to build MediaItem with MediaId: {MediaId}", mediaId);
        }

        return item;
    }

    /// <summary>
    /// Parses a URI string to Android Uri.
    /// </summary>
    private Uri? ParseUri(string uri)
    {
        var androidUri = Uri.Parse(uri);
        if (androidUri == null)
        {
            logger.Error("Failed to parse URI: {Uri}", uri);
        }
        return androidUri;
    }

    /// <summary>
    /// Gets the URI of the silent MP3 file from the storage directory.
    /// </summary>
    private string? GetSilentMp3Uri()
    {
        try
        {
            var silentMp3Provider = new SilentMp3Provider(logger);
            return silentMp3Provider.GetSilentMp3Uri();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting silent MP3 URI");
            return null;
        }
    }
}
