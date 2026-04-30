#nullable enable
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.ViewModels.Music;
using Serilog;

namespace Bible.Alarm.ViewModels.Music.MusicTrackSelectionViewModelHelpers;

/// <summary>
/// Handles track list management and population for MusicTrackSelectionViewModel.
/// Music type is inferred from isMelodyMusic parameter (true = instrumental, false = vocal).
/// </summary>
public sealed class MusicTrackListManager(
    ILogger logger,
    IMediaService mediaService)
{
    private readonly Dictionary<MusicTrackListViewItemModel, PropertyChangedEventHandler> propertyChangedHandlers = [];
    private ObservableCollection<MusicTrackListViewItemModel>? collectionWithHandler;
    private NotifyCollectionChangedEventHandler? collectionChangedHandler;

    public async Task PopulateTracks(
        bool isMelodyMusic,
        string? languageCode,
        string publicationCode,
        string? sectionCode,
        ObservableCollection<MusicTrackListViewItemModel> tracks)
    {
        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            try
            {
                tracks.Clear();

                // Run database operations off UI thread
                SortedDictionary<int, MusicTrack> tracksFromDb;
                if (isMelodyMusic)
                {
                    // IMPORTANT: Some melody publications (notably "iam") are sectioned by disc.
                    // For those, the track list must be loaded for the currently selected section code.
                    if (PublicationTypeHelper.HasSectionStructure(publicationCode) && !string.IsNullOrWhiteSpace(sectionCode))
                    {
                        tracksFromDb = await Task.Run(async () =>
                            await mediaService.GetMelodyMusicTracksBySection(publicationCode, sectionCode));
                    }
                    else
                    {
                        tracksFromDb = await Task.Run(async () =>
                            await mediaService.GetMelodyMusicTracks(publicationCode));
                    }
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

                var trackVMs = new List<MusicTrackListViewItemModel>();
                foreach (var track in tracksFromDb.Select(x => x.Value))
                {
                    var trackVm = new MusicTrackListViewItemModel(track);
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
        });
    }

    public void SubscribeToTrackEvents(MusicTrackListViewItemModel track, ObservableCollection<MusicTrackListViewItemModel> tracks)
    {
        PropertyChangedEventHandler handler = (sender, e) =>
        {
            if (sender is not MusicTrackListViewItemModel item)
            {
                return;
            }

            if (string.Equals(e.PropertyName, nameof(MusicTrackListViewItemModel.Repeat), StringComparison.Ordinal))
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

    private static void HandleRepeatChanged(MusicTrackListViewItemModel item, ObservableCollection<MusicTrackListViewItemModel> tracks)
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
        TeardownCollectionChangedHandler();
        collectionChangedHandler = (_, e) =>
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
        tracks.CollectionChanged += collectionChangedHandler;
        collectionWithHandler = tracks;
    }

    public void TeardownCollectionChangedHandler()
    {
        if (collectionWithHandler != null && collectionChangedHandler != null)
        {
            collectionWithHandler.CollectionChanged -= collectionChangedHandler;
            foreach (var track in collectionWithHandler)
            {
                UnsubscribeFromTrackEvents(track);
            }
            collectionWithHandler = null;
            collectionChangedHandler = null;
        }
    }

    public static void SetSelectedTrack(AlarmMusic? current, ObservableCollection<MusicTrackListViewItemModel> tracks, Action<MusicTrackListViewItemModel?> setSelectedTrack)
    {
        if (current == null || tracks == null || tracks.Count == 0)
        {
            return;
        }

        var track = tracks.FirstOrDefault(t => !string.IsNullOrWhiteSpace(current.TrackCode) &&
            Bible.Alarm.Shared.Helpers.CodeComparisonHelper.Equals(t.TrackCode, current.TrackCode));
        if (track != null)
        {
            setSelectedTrack(track);
            track.IsSelected = true;
            track.Repeat = current.Repeat;
        }
    }

    private readonly SemaphoreSlim @lock = new(1);
}
