#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.TrackSelectionViewModelHelpers;

/// <summary>
/// Handles track list management and population for TrackSelectionViewModel.
/// </summary>
public sealed class TrackListManager(
    ILogger logger,
    IMediaService mediaService)
{
    private readonly Dictionary<MusicTrackListViewItemModel, PropertyChangedEventHandler> propertyChangedHandlers = [];

    public async Task PopulateTracks(MusicType musicType, string? languageCode, string publicationCode, ObservableCollection<MusicTrackListViewItemModel> tracks)
    {
        await @lock.WaitAsync();
        try
        {
            tracks.Clear();

            // Run database operations off UI thread
            SortedDictionary<int, MusicTrack> tracksFromDb;
            if (musicType == MusicType.Melodies)
            {
                tracksFromDb = await Task.Run(async () =>
                    await mediaService.GetMelodyMusicTracks(publicationCode));
            }
            else
            {
                if (string.IsNullOrEmpty(languageCode))
                {
                    return;
                }
                tracksFromDb = await Task.Run(async () =>
                    await mediaService.GetVocalMusicTracks(languageCode, publicationCode));
            }

            var trackVMs = new ObservableCollection<MusicTrackListViewItemModel>();
            var isMelody = musicType == MusicType.Melodies;

            foreach (var track in tracksFromDb.Select(x => x.Value))
            {
                var trackVm = new MusicTrackListViewItemModel(track, isMelody);
                trackVMs.Add(trackVm);
            }

            // Assign collection on main thread to ensure UI updates
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                tracks.Clear();
                foreach (var track in trackVMs)
                {
                    tracks.Add(track);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error populating tracks for languageCode: {LanguageCode}, publicationCode: {PublicationCode}",
                languageCode, publicationCode);
        }
        finally
        {
            @lock.Release();
        }
    }

    public void SubscribeToTrackEvents(MusicTrackListViewItemModel track, ObservableCollection<MusicTrackListViewItemModel> tracks)
    {
        PropertyChangedEventHandler handler = (sender, e) =>
        {
            if (sender is not MusicTrackListViewItemModel item)
            {
                return;
            }

            if (e.PropertyName == "Repeat")
            {
                HandleRepeatChanged(item, tracks);
            }
        };

        track.PropertyChanged += handler;
        propertyChangedHandlers[track] = handler;
    }

    public void UnsubscribeFromTrackEvents(MusicTrackListViewItemModel track)
    {
        if (propertyChangedHandlers.TryGetValue(track, out var handler))
        {
            track.PropertyChanged -= handler;
            propertyChangedHandlers.Remove(track);
        }
    }

    private void HandleRepeatChanged(MusicTrackListViewItemModel item, ObservableCollection<MusicTrackListViewItemModel> tracks)
    {
        // Ensure only one track has Repeat = true
        if (item.Repeat)
        {
            foreach (var track in tracks.Where(t => t != item))
            {
                track.Repeat = false;
            }
        }
    }

    public void SetupCollectionChangedHandler(ObservableCollection<MusicTrackListViewItemModel> tracks)
    {
        tracks.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (MusicTrackListViewItemModel item in e.NewItems)
                {
                    SubscribeToTrackEvents(item, tracks);
                }
            }

            if (e.OldItems != null)
            {
                foreach (MusicTrackListViewItemModel item in e.OldItems)
                {
                    UnsubscribeFromTrackEvents(item);
                }
            }
        };
    }

    public void SetSelectedTrack(AlarmMusic? current, ObservableCollection<MusicTrackListViewItemModel> tracks, Action<MusicTrackListViewItemModel?> setSelectedTrack)
    {
        if (current == null || tracks == null || tracks.Count == 0)
        {
            return;
        }

        var track = tracks.FirstOrDefault(t => t.Number == current.TrackNumber);
        if (track != null)
        {
            setSelectedTrack(track);
            track.IsSelected = true;
            track.Repeat = current.Repeat;
        }
    }

    private readonly SemaphoreSlim @lock = new(1);
}
