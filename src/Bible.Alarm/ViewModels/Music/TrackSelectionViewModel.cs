using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public class TrackSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger logger;

    private readonly IMediaService mediaService;
    private readonly IToastService toastService;
    private readonly IAudioPreviewer playService;
    private readonly IMediaUrlRefreshService urlRefreshService;
    private readonly IDownloadService downloadService;
    private readonly IDispatcher dispatcher;
    private readonly IState<ApplicationState> state;
    private readonly IMapper mapper;

    private AlarmMusic current;
    private AlarmMusic tentative;
    private bool initComplete;

    private readonly Dictionary<MusicTrackListViewItemModel, PropertyChangedEventHandler> propertyChangedHandlers = [];

    public TrackSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IToastService toastService,
        IAudioPreviewer playService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaUrlRefreshService urlRefreshService,
        IState<ApplicationState> state,
        IDispatcher dispatcher,
        IMapper mapper)
    {
        this.logger = logger;
        this.mediaService = mediaService;
        this.toastService = toastService;
        this.playService = playService;
        this.downloadService = downloadService;
        this.urlRefreshService = urlRefreshService;
        this.mapper = mapper;

        this.state = state;
        this.dispatcher = dispatcher;

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;

            // Stop any ongoing preview playback before navigating away
            playService.Stop();
            if (currentlyPlaying != null)
            {
                currentlyPlaying.Play = false;
                currentlyPlaying.IsBusy = false;
                currentlyPlaying = null;
            }

            await navigationService.PopAsync();
            IsBusy = false;
        });

        SetTrackCommand = new RelayCommand<MusicTrackListViewItemModel>(x =>
        {
            if (x == null)
            {
                return;
            }

            // Ensure tentative is set from state if it's null
            if (tentative == null)
            {
                var stateValue = state.Value;
                if (stateValue.TentativeMusic != null)
                {
                    tentative = mapper.Map<AlarmMusic>(stateValue.TentativeMusic);
                }
                if (stateValue.CurrentMusic != null)
                {
                    current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
                }
                else if (tentative != null)
                {
                    current = tentative;
                }
            }

            if (tentative == null)
            {
                return;
            }

            IsBusy = true;
            if (SelectedTrack != null)
            {
                SelectedTrack.IsSelected = false;
                SelectedTrack.Repeat = false;
            }

            SelectedTrack = x;
            SelectedTrack.IsSelected = true;

            tentative.TrackNumber = x.Number;
            tentative.Repeat = x.Repeat;

            // Map entity to DTO before dispatching
            var trackSelectedItem = new MusicStateItem
            {
                MusicType = tentative.MusicType,
                LanguageCode = tentative.LanguageCode,
                PublicationCode = tentative.PublicationCode,
                TrackNumber = tentative.TrackNumber,
                Repeat = tentative.Repeat
            };
            dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

            IsBusy = false;
        });

        state.StateChanged += OnMusicInitialized;
    }

    private void OnMusicInitialized(object o, EventArgs eventArgs)
    {
        if (initComplete)
        {
            return;
        }

        var stateValue = state.Value;
        // For track selection, we only need TentativeMusic to initialize
        // CurrentMusic might be null when navigating directly to track selection
        if (stateValue.TentativeMusic == null)
        {
            return;
        }
        // Map DTOs to entities
        tentative = mapper.Map<AlarmMusic>(stateValue.TentativeMusic);
        // Use TentativeMusic as CurrentMusic if CurrentMusic is null
        if (stateValue.CurrentMusic != null)
        {
            current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
        }
        else
        {
            current = tentative;
        }
        initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(tentative.LanguageCode, tentative.PublicationCode);

            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following book selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);

            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    public ICommand BackCommand { get; set; }
    public ICommand SetTrackCommand { get; set; }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    private ObservableCollection<MusicTrackListViewItemModel> tracks = [];

    public ObservableCollection<MusicTrackListViewItemModel> Tracks
    {
        get => tracks;
        set => SetProperty(ref tracks, value);
    }

    public MusicTrackListViewItemModel SelectedTrack { get; set; }

    private MusicTrackListViewItemModel currentlyPlaying;

    private readonly SemaphoreSlim @lock = new(1);

    private async Task Initialize(string languageCode, string publicationCode)
    {
        await PopulateTracks(languageCode, publicationCode);

        // Subscribe to PropertyChanged events for Play/Stop/Repeat
        foreach (var track in Tracks)
        {
            SubscribeToTrackEvents(track);
        }

        // Subscribe to collection changes to handle new items
        Tracks.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (MusicTrackListViewItemModel item in e.NewItems)
                {
                    SubscribeToTrackEvents(item);
                }
            }

            if (e.OldItems == null)
            {
                return;
            }

            {
                foreach (MusicTrackListViewItemModel item in e.OldItems)
                {
                    UnsubscribeFromTrackEvents(item);
                }
            }
        };

        // Subscribe to play service stopped event
        playService.OnStopped += OnPlayServiceStopped;
    }

    private void SubscribeToTrackEvents(MusicTrackListViewItemModel track)
    {
        PropertyChangedEventHandler handler = (sender, e) =>
        {
            if (sender is not MusicTrackListViewItemModel item)
            {
                return;
            }

            switch (e.PropertyName)
            {
                case "Play" when item.Play:
                    _ = HandlePlayTrack(item);
                    break;
                case "Play":
                    playService.Stop();
                    break;
                case "Repeat":
                    HandleRepeatChanged(item);
                    break;
            }
        };

        track.PropertyChanged += handler;
        propertyChangedHandlers[track] = handler;
    }

    private void UnsubscribeFromTrackEvents(MusicTrackListViewItemModel track)
    {
        if (!propertyChangedHandlers.TryGetValue(track, out var handler))
        {
            return;
        }

        track.PropertyChanged -= handler;
        propertyChangedHandlers.Remove(track);
    }

    private async Task HandlePlayTrack(MusicTrackListViewItemModel track)
    {
        await ConcurrencyHelper.ExecuteAsync(@lock, async () =>
        {
            if (currentlyPlaying != null && currentlyPlaying != track)
            {
                currentlyPlaying.Play = false;
                currentlyPlaying.IsBusy = false;
            }

            currentlyPlaying = track;
            currentlyPlaying.IsBusy = true;
            try
            {
                var url = track.Url;

                await Task.Run(async () =>
                {
                    if (tentative == null)
                    {
                        return;
                    }

                    if (!await downloadService.FileExists(url))
                    {
                        url = await urlRefreshService.GetMusicTrackUrl(
                            tentative.LanguageCode,
                            track.LookUpPath);
                    }

                    await playService.Play(url);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error playing track preview");
                currentlyPlaying.Play = false;
                await toastService.ShowMessage("Media download failed. Check your internet connection.");
            }

            currentlyPlaying.IsBusy = false;
        }, ex => logger.Error(ex, "TrackSelectionViewModel: @lock disposed error."));
    }

    private void HandleRepeatChanged(MusicTrackListViewItemModel track)
    {
        if (tentative == null)
        {
            return;
        }

        tentative.Repeat = track.Repeat;
        tentative.TrackNumber = track.Number;

        // Map entity to DTO before dispatching
        var trackSelectedItem = new MusicStateItem
        {
            MusicType = tentative.MusicType,
            LanguageCode = tentative.LanguageCode,
            PublicationCode = tentative.PublicationCode,
            TrackNumber = tentative.TrackNumber,
            Repeat = tentative.Repeat
        };
        dispatcher.Dispatch(new TrackSelectedAction(trackSelectedItem));

        if (track.Repeat)
        {
            toastService.ShowMessage("Reminder will always repeat this track.");
        }
    }

    private async void OnPlayServiceStopped()
    {
        try
        {
            await ConcurrencyHelper.ExecuteAsync(@lock, () =>
            {
                if (currentlyPlaying == null)
                {
                    return Task.CompletedTask;
                }

                currentlyPlaying.Play = false;
                currentlyPlaying.IsBusy = false;
                currentlyPlaying = null;
                return Task.CompletedTask;
            }, ex => logger.Error(ex, "TrackSelectionViewModel: @lock disposed error."));
        }
        catch (Exception e)
        {
            logger.Error(e, "Error in OnPlayServiceStopped.");
        }
    }

    private async Task PopulateTracks(string languageCode, string publicationCode)
    {
        var isVocal = languageCode != null;

        // Run database operations off UI thread
        var tracks = await Task.Run(async () => isVocal
            ? await mediaService.GetVocalMusicTracks(languageCode, publicationCode)
            : await mediaService.GetMelodyMusicTracks(
                (await mediaService.GetMelodyMusicReleases()).FirstOrDefault().Value?.Code ?? "iam"));

        // Build the list of track view models
        var trackViewModelList = new List<MusicTrackListViewItemModel>();
        MusicTrackListViewItemModel selectedTrack = null;

        foreach (var track in tracks.Select(x => x.Value))
        {
            var musicTrackListViewItemViewModel = new MusicTrackListViewItemModel(track, !isVocal);

            trackViewModelList.Add(musicTrackListViewItemViewModel);

            if (current == null || tentative == null)
            {
                continue;
            }

            if (current.MusicType != tentative.MusicType
                || current.TrackNumber != track.Number
                || (current.MusicType != MusicType.Melodies &&
                    (current.LanguageCode != tentative.LanguageCode
                     || current.PublicationCode != tentative.PublicationCode)))
            {
                continue;
            }

            selectedTrack = musicTrackListViewItemViewModel;
            selectedTrack.IsSelected = true;
            selectedTrack.Repeat = current.Repeat;
        }

        // Assign the complete collection on main thread to ensure CollectionView refreshes
        // CollectionView responds better to property change notifications than collection modification
        // Follow the same pattern as language modal: assign collection but don't set IsBusy here
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Tracks = new ObservableCollection<MusicTrackListViewItemModel>(trackViewModelList);

            if (selectedTrack != null)
            {
                SelectedTrack = selectedTrack;
            }
        });
    }

    public void Dispose()
    {
        state.StateChanged -= OnMusicInitialized;
        playService.OnStopped -= OnPlayServiceStopped;

        // Stop any ongoing preview playback when navigating away
        playService.Stop();

        if (Tracks != null)
        {
            foreach (var track in Tracks)
            {
                UnsubscribeFromTrackEvents(track);
            }
        }

        propertyChangedHandlers.Clear();

        @lock.Dispose();
    }
}

public class MusicTrackListViewItemModel : ObservableObject, IComparable
{
    private readonly MusicTrack track;
    private readonly bool isMelody;

    public MusicTrackListViewItemModel(MusicTrack track, bool isMelody)
    {
        this.track = track;
        this.isMelody = isMelody;

        TogglePlayCommand = new RelayCommand(() => Play = !Play);
        ToggleRepeatCommand = new RelayCommand(() => Repeat = !Repeat);
    }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public string LookUpPath => track.Source.LookUpPath;
    public int Number => track.Number;

    public string Title => isMelody ? $"Melody Number(s) {track.Title}" : track.Title;
    public string Url => track.Source.Url;

    private bool play;

    public bool Play
    {
        get => play;
        set => SetProperty(ref play, value);
    }

    private bool repeat;

    public bool Repeat
    {
        get => repeat;
        set => SetProperty(ref repeat, value);
    }

    private bool isBusy;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
    }

    public ICommand TogglePlayCommand { get; set; }
    public ICommand ToggleRepeatCommand { get; set; }

    public int CompareTo(object obj) => Number.CompareTo((obj as MusicTrackListViewItemModel)?.Number);
}
