using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public class TrackSelectionViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _logger;

    private readonly MediaService _mediaService;
    private readonly IToastService _toastService;
    private readonly IPreviewPlayService _playService;
    private readonly IMediaCacheService _cacheService;
    private readonly IDownloadService _downloadService;
    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    private AlarmMusic _current;
    private AlarmMusic _tentative;
    private bool _initialized;

    private readonly Dictionary<MusicTrackListViewItemModel, PropertyChangedEventHandler> _propertyChangedHandlers = [];

    public TrackSelectionViewModel(
        ILogger logger,
        MediaService mediaService,
        IToastService toastService,
        IPreviewPlayService playService,
        INavigationService navigationService,
        IDownloadService downloadService,
        IMediaCacheService cacheService)
    {
        _logger = logger;
        _mediaService = mediaService;
        _toastService = toastService;
        _playService = playService;
        var navigationService1 = navigationService;
        _downloadService = downloadService;
        _cacheService = cacheService;
        
        _state = MauiAppHolder.Services.GetRequiredService<IState<ApplicationState>>();
        _dispatcher = MauiAppHolder.Services.GetRequiredService<IDispatcher>();

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService1.PopAsync();
            IsBusy = false;
        });

        SetTrackCommand = new RelayCommand<MusicTrackListViewItemModel>(x =>
        {
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
        
        // Check current state immediately in case state is already set
        var currentState = _state.Value;
        if (currentState.TentativeMusic != null && !_initialized)
        {
            OnMusicInitialized(null, EventArgs.Empty);
        }

        return;

        void OnMusicInitialized(object o, EventArgs eventArgs)
        {
            if (_initialized) return;
            var stateValue = _state.Value;
            // For track selection, we only need TentativeMusic to initialize
            // CurrentMusic might be null when navigating directly to track selection
            if (stateValue.TentativeMusic == null) return;
            _tentative = stateValue.TentativeMusic;
            // Use TentativeMusic as CurrentMusic if CurrentMusic is null
            _current = stateValue.CurrentMusic ?? stateValue.TentativeMusic;
            _initialized = true;
            Task.Run(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
                await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
            });
            _state.StateChanged -= OnMusicInitialized;
        }
    }

    public ICommand BackCommand { get; set; }
    public ICommand SetTrackCommand { get; set; }

    private bool _isBusy;

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
        await _lock.WaitAsync();

        try
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
                    if (!await _downloadService.FileExists(url))
                        url = await _cacheService.GetMusicTrackUrl(
                            _tentative.LanguageCode,
                            track.LookUpPath);

                    await _playService.Play(url);
                });
            }
            catch
            {
                _currentlyPlaying.Play = false;
                await _toastService.ShowMessage("Failed to download the file.");
            }

            _currentlyPlaying.IsBusy = false;
        }
        finally
        {
            try
            {
                _lock.Release();
            }
            catch (ObjectDisposedException e)
            {
                _logger.Error(e, "TrackSelectionViewModel: @lock disposed error.");
            }
        }
    }

    private void HandleRepeatChanged(MusicTrackListViewItemModel track)
    {
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

        if (track.Repeat) _toastService.ShowMessage("Alarm will always repeat this track.");
    }

    private async void OnPlayServiceStopped()
    {
        try
        {
            await _lock.WaitAsync();

            try
            {
                if (_currentlyPlaying == null) return;
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
                _currentlyPlaying = null;
            }
            finally
            {
                try
                {
                    _lock.Release();
                }
                catch (ObjectDisposedException e)
                {
                    _logger.Error(e, "TrackSelectionViewModel: @lock disposed error.");
                }
            }
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

        var trackVMs = new ObservableCollection<MusicTrackListViewItemModel>();

        // On WinUI, assign empty collection first on main thread so ListView can observe CollectionChanged
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            await MainThread.InvokeOnMainThreadAsync(() => Tracks = trackVMs);
        }

        // Build the list of track view models
        var trackViewModelList = new List<MusicTrackListViewItemModel>();
        MusicTrackListViewItemModel selectedTrack = null;

        foreach (var track in tracks.Select(x => x.Value))
        {
            var musicTrackListViewItemViewModel = new MusicTrackListViewItemModel(track, !isVocal);

            trackViewModelList.Add(musicTrackListViewItemViewModel);

            if (_current.MusicType != _tentative.MusicType
                || _current.TrackNumber != track.Number
                || (_current.MusicType != MusicType.Melodies &&
                    (_current.LanguageCode != _tentative.LanguageCode
                     || _current.PublicationCode != _tentative.PublicationCode))) continue;
            selectedTrack = musicTrackListViewItemViewModel;
            selectedTrack.IsSelected = true;
            selectedTrack.Repeat = _current.Repeat;
        }

        // Add items to collection on main thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                // On WinUI, add items one by one to the already-assigned collection
                // Since Tracks = trackVMs, adding to Tracks will raise CollectionChanged events
                foreach (var trackVm in trackViewModelList)
                {
                    Tracks.Add(trackVm);
                }
            }
            else
            {
                // On other platforms, assign the populated collection
                Tracks = new ObservableCollection<MusicTrackListViewItemModel>(trackViewModelList);
            }

            if (selectedTrack != null)
            {
                SelectedTrack = selectedTrack;
            }
        });
    }

    public void Dispose()
    {
        _playService.OnStopped -= OnPlayServiceStopped;
        
        foreach (var track in Tracks)
        {
            UnsubscribeFromTrackEvents(track);
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