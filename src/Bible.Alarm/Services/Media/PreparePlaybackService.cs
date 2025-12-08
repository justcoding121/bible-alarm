#nullable enable
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions.Playback;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Services.Media;

public class PreparePlaybackService(
    ILogger logger,
    IPlaylistService playlistService,
    IMediaCacheService cacheService,
    IDispatcher dispatcher) : IPreparePlaybackService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IPlaylistService _playlistService = playlistService;
    private readonly IMediaCacheService _cacheService = cacheService;
    private readonly IDispatcher _dispatcher = dispatcher;
    private bool _isDisposed;

    public async Task<List<AudioPlayerTrack>?> PrepareTracksAsync(int scheduleId)
    {
        var playItems = await _playlistService.NextTracks(scheduleId);
        var preparedTracks = new List<AudioPlayerTrack>();
        var totalTracks = playItems.Count;
        var loadedTracks = 0;

        // Send initial progress message with total count
        WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
        {
            LoadedTracks = 0,
            TotalTracks = totalTracks
        });

        foreach (var playItem in playItems)
        {
            var uri = await _cacheService.GetOrDownloadTrackUriAsync(playItem);
            
            if (uri == null)
            {
                _logger.Warning($"Failed to download {playItem.Url}");
                return null;
            }

            preparedTracks.Add(new AudioPlayerTrack
            {
                Uri = uri,
                PlayItem = playItem
            });

            // Send progress update message after each track is prepared
            loadedTracks++;
            WeakReferenceMessenger.Default.Send(new PlaybackPreparationProgressMessage
            {
                LoadedTracks = loadedTracks,
                TotalTracks = totalTracks
            });
            
            // Add delay to ensure each progress state (1/3, 2/3, 3/3) is visible on UI
            if (loadedTracks < totalTracks)
            {
                await Task.Delay(50);
            }
        }

        return preparedTracks;
    }

    /// <summary>
    /// Dispatches playlist information to Fluxor state for CarPlay/Android Auto.
    /// Gets playlist info from PlaylistService (source of truth).
    /// </summary>
    public Task DispatchPlaylistChangedAsync(List<AudioPlayerTrack>? playlist, int currentTrackIndex)
    {
        if (playlist == null || playlist.Count == 0)
        {
            _dispatcher.Dispatch(new PlaybackPlaylistChangedAction
            {
                Playlist = null,
                CurrentTrackIndex = -1
            });
            return Task.CompletedTask;
        }

        try
        {
            // Build playlist info from PlayItems already in prepared tracks
            // This avoids calling NextTracks again - we already have PlayItems in AudioPlayerTrack.PlayItem
            var playItems = playlist.Select(track => track.PlayItem).ToList();
            var playlistInfo = BuildPlaylistInfo(playItems);
            
            _dispatcher.Dispatch(new PlaybackPlaylistChangedAction
            {
                Playlist = playlistInfo,
                CurrentTrackIndex = currentTrackIndex
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error dispatching playlist changed action");
            // Dispatch with basic info if metadata extraction fails
            var basicPlaylist = playlist.Select((track, index) => new PlaylistTrackInfo
            {
                Title = $"Track {index + 1}",
                Artist = null,
                Album = null,
                Index = index
            }).ToList();

            _dispatcher.Dispatch(new PlaybackPlaylistChangedAction
            {
                Playlist = basicPlaylist,
                CurrentTrackIndex = currentTrackIndex
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds playlist info from PlayItems for CarPlay/Android Auto.
    /// </summary>
    private List<PlaylistTrackInfo> BuildPlaylistInfo(List<PlayItem> playItems)
    {
        return playItems.Select((playItem, index) =>
        {
            var trackMetadata = playItem.Metadata;
            string? title = null;
            string? artist = null;
            string? album = null;

            if (trackMetadata != null)
            {
                if (trackMetadata.PlayType == PlayType.Bible)
                {
                    // Build title from book and chapter number
                    title = $"Book {trackMetadata.BookNumber} Chapter {trackMetadata.ChapterNumber}";
                    artist = trackMetadata.PublicationCode; // Use publication code as artist
                    album = trackMetadata.LanguageCode; // Use language code as album
                }
                else
                {
                    // Music track
                    title = trackMetadata.TrackNumber > 0 
                        ? $"Track {trackMetadata.TrackNumber}" 
                        : $"Track {index + 1}";
                    artist = trackMetadata.PublicationCode;
                    album = trackMetadata.LanguageCode;
                }
            }
            else
            {
                title = $"Track {index + 1}";
            }

            return new PlaylistTrackInfo
            {
                Title = title ?? $"Track {index + 1}",
                Artist = artist,
                Album = album,
                Index = index
            };
        }).ToList();
    }
    
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // All injected services are singletons, so don't dispose them
        // No event handlers to unsubscribe
    }
}

