using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.Essentials;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public class TrackSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _logger;

    private readonly IMediaService _mediaService;
    private readonly IToastService _toastService;
    private readonly IAudioPreviewer _playService;
    private readonly IMediaUrlRefreshService _urlRefreshService;
    private readonly IDownloadService _downloadService;
    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    private AlarmMusic _current;
    private AlarmMusic _tentative;
    private bool _initComplete;

    private readonly Dictionary<MusicTrackListViewItemModel, PropertyChangedEventHandler> _propertyChangedHandlers = [];

    public TrackSelectionViewModel(
        ILogger logger,
        IMediaService mediaService,
        IToastService toastService,
        IAudioPreviewer playService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaUrlRefreshService urlRefreshService,
        IState<ApplicationState> state,
        IDispatcher dispatcher)
    {
        _logger = logger;
        _mediaService = mediaService;
        _toastService = toastService;
        _playService = playService;
        _downloadService = downloadService;
        _urlRefreshService = urlRefreshService;
        
        _state = state;
        _dispatcher = dispatcher;

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            
            // Stop any ongoing preview playback before navigating away
            _playService.Stop();
            if (_currentlyPlaying != null)
            {
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
                _currentlyPlaying = null;
            }
            
            await navigationService.PopAsync();
            IsBusy = false;
        });

        SetTrackCommand = new RelayCommand<MusicTrackListViewItemModel>(x =>
        {
            if (x == null) return;
            
            // Ensure _tentative is set from state if it's null
            if (_tentative == null)
            {
                var stateValue = _state.Value;
                _tentative = stateValue.TentativeMusic;
                _current = stateValue.CurrentMusic ?? _tentative;
            }

            if (_tentative == null) return;

            IsBusy = true;
            if (SelectedTrack != null)
            {
                SelectedTrack.IsSelected = false;
                SelectedTrack.Repeat = false;
            }

            SelectedTrack = x;
            SelectedTrack.IsSelected = true;

            _tentative.TrackNumber = x.Number;
            _tentative.Repeat = x.Repeat;

            _dispatcher.Dispatch(new TrackSelectedAction(new AlarmMusic
            {
                MusicType = _tentative.MusicType,
                LanguageCode = _tentative.LanguageCode,
                PublicationCode = _tentative.PublicationCode,
                TrackNumber = _tentative.TrackNumber,
                Repeat = _tentative.Repeat
            }));

            IsBusy = false;
        });

        _state.StateChanged += OnMusicInitialized;
    }

    private void OnMusicInitialized(object o, EventArgs eventArgs)
    {
        if (_initComplete) return;
        var stateValue = _state.Value;
        // For track selection, we only need TentativeMusic to initialize
        // CurrentMusic might be null when navigating directly to track selection
        if (stateValue.TentativeMusic == null) return;
        _tentative = stateValue.TentativeMusic;
        // Use TentativeMusic as CurrentMusic if CurrentMusic is null
        _current = stateValue.CurrentMusic ?? stateValue.TentativeMusic;
        _initComplete = true;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
            
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
    private bool _isBusy = true;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private ObservableCollection<MusicTrackListViewItemModel> _tracks = [];

    public ObservableCollection<MusicTrackListViewItemModel> Tracks
    {
        get => _tracks;
        set => SetProperty(ref _tracks, value);
    }

    public MusicTrackListViewItemModel SelectedTrack { get; set; }

    private MusicTrackListViewItemModel _currentlyPlaying;

    private readonly SemaphoreSlim _lock = new(1);

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

            if (e.OldItems == null) return;
            {
                foreach (MusicTrackListViewItemModel item in e.OldItems)
                {
                    UnsubscribeFromTrackEvents(item);
                }
            }
        };

        // Subscribe to play service stopped event
        _playService.OnStopped += OnPlayServiceStopped;
    }

    private void SubscribeToTrackEvents(MusicTrackListViewItemModel track)
    {
        PropertyChangedEventHandler handler = (sender, e) =>
        {
            if (sender is not MusicTrackListViewItemModel item) return;
            switch (e.PropertyName)
            {
                case "Play" when item.Play:
                    _ = HandlePlayTrack(item);
                    break;
                case "Play":
                    _playService.Stop();
                    break;
                case "Repeat":
                    HandleRepeatChanged(item);
                    break;
            }
        };

        track.PropertyChanged += handler;
        _propertyChangedHandlers[track] = handler;
    }

    private void UnsubscribeFromTrackEvents(MusicTrackListViewItemModel track)
    {
        if (!_propertyChangedHandlers.TryGetValue(track, out var handler)) return;
        track.PropertyChanged -= handler;
        _propertyChangedHandlers.Remove(track);
    }

    private async Task HandlePlayTrack(MusicTrackListViewItemModel track)
    {
        await ConcurrencyHelper.ExecuteAsync(_lock, async () =>
        {
            if (_currentlyPlaying != null && _currentlyPlaying != track)
            {
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
            }

            _currentlyPlaying = track;
            _currentlyPlaying.IsBusy = true;
            try
            {
                var url = track.Url;

                await Task.Run(async () =>
                {
                    if (_tentative == null) return;
                    if (!await _downloadService.FileExists(url))
                        url = await _urlRefreshService.GetMusicTrackUrl(
                            _tentative.LanguageCode,
                            track.LookUpPath);

                    await _playService.Play(url);
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error playing track preview");
                _currentlyPlaying.Play = false;
                await _toastService.ShowMessage("Media download failed. Check your internet connection.");
            }

            _currentlyPlaying.IsBusy = false;
        }, ex => _logger.Error(ex, "TrackSelectionViewModel: @lock disposed error."));
    }

    private void HandleRepeatChanged(MusicTrackListViewItemModel track)
    {
        if (_tentative == null) return;

        _tentative.Repeat = track.Repeat;
        _tentative.TrackNumber = track.Number;

        _dispatcher.Dispatch(new TrackSelectedAction(new AlarmMusic
        {
            MusicType = _tentative.MusicType,
            LanguageCode = _tentative.LanguageCode,
            PublicationCode = _tentative.PublicationCode,
            TrackNumber = _tentative.TrackNumber,
            Repeat = _tentative.Repeat
        }));

        if (track.Repeat) _toastService.ShowMessage("Reminder will always repeat this track.");
    }

    private async void OnPlayServiceStopped()
    {
        try
        {
            await ConcurrencyHelper.ExecuteAsync(_lock, () =>
            {
                if (_currentlyPlaying == null) return Task.CompletedTask;
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
                _currentlyPlaying = null;
                return Task.CompletedTask;
            }, ex => _logger.Error(ex, "TrackSelectionViewModel: @lock disposed error."));
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error in OnPlayServiceStopped.");
        }
    }

    private async Task PopulateTracks(string languageCode, string publicationCode)
    {
        var isVocal = languageCode != null;

        var tracks = isVocal
            ? await _mediaService.GetVocalMusicTracks(languageCode, publicationCode)
            : await _mediaService.GetMelodyMusicTracks(
                (await _mediaService.GetMelodyMusicReleases()).FirstOrDefault().Value?.Code ?? "iam");

        // Build the list of track view models
        var trackViewModelList = new List<MusicTrackListViewItemModel>();
        MusicTrackListViewItemModel selectedTrack = null;

        foreach (var track in tracks.Select(x => x.Value))
        {
            var musicTrackListViewItemViewModel = new MusicTrackListViewItemModel(track, !isVocal);

            trackViewModelList.Add(musicTrackListViewItemViewModel);

            if (_current == null || _tentative == null) continue;
            if (_current.MusicType != _tentative.MusicType
                || _current.TrackNumber != track.Number
                || (_current.MusicType != MusicType.Melodies &&
                    (_current.LanguageCode != _tentative.LanguageCode
                     || _current.PublicationCode != _tentative.PublicationCode))) continue;
            selectedTrack = musicTrackListViewItemViewModel;
            selectedTrack.IsSelected = true;
            selectedTrack.Repeat = _current.Repeat;
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
        _state.StateChanged -= OnMusicInitialized;
        _playService.OnStopped -= OnPlayServiceStopped;
        
        // Stop any ongoing preview playback when navigating away
        _playService.Stop();
        
        if (Tracks != null)
        {
            foreach (var track in Tracks)
            {
                UnsubscribeFromTrackEvents(track);
            }
        }

        _propertyChangedHandlers.Clear();

        _lock.Dispose();
    }
}

public class MusicTrackListViewItemModel : ObservableObject, IComparable
{
    private readonly MusicTrack _track;
    private readonly bool _isMelody;

    public MusicTrackListViewItemModel(MusicTrack track, bool isMelody)
    {
        _track = track;
        _isMelody = isMelody;

        TogglePlayCommand = new RelayCommand(() => Play = !Play);
        ToggleRepeatCommand = new RelayCommand(() => Repeat = !Repeat);
    }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string LookUpPath => _track.Source.LookUpPath;
    public int Number => _track.Number;

    public string Title => _isMelody ? $"Melody Number(s) {_track.Title}" : _track.Title;
    public string Url => _track.Source.Url;

    private bool _play;

    public bool Play
    {
        get => _play;
        set => SetProperty(ref _play, value);
    }

    private bool _repeat;

    public bool Repeat
    {
        get => _repeat;
        set => SetProperty(ref _repeat, value);
    }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand TogglePlayCommand { get; set; }
    public ICommand ToggleRepeatCommand { get; set; }

    public int CompareTo(object obj)
    {
        return Number.CompareTo((obj as MusicTrackListViewItemModel)?.Number);
    }
}