using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Music;
using Serilog;

namespace Bible.Alarm.ViewModels.Music;

public class TrackSelectionViewModel : ViewModel, IDisposable
{
    private readonly ILogger _logger;

    private readonly MediaService _mediaService;
    private readonly IToastService _toastService;
    private readonly IPreviewPlayService _playService;
    private readonly INavigationService _navigationService;
    private readonly IMediaCacheService _cacheService;
    private readonly IDownloadService _downloadService;

    private AlarmMusic _current;
    private AlarmMusic _tentative;

    private readonly List<IDisposable> _subscriptions = [];
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
        _navigationService = navigationService;
        _downloadService = downloadService;
        _cacheService = cacheService;

        _subscriptions.Add(_mediaService);

        BackCommand = new Command(async () =>
        {
            IsBusy = true;
            await _navigationService.GoBack();
            IsBusy = false;
        });

        SetTrackCommand = new Command<MusicTrackListViewItemModel>(x =>
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

            ReduxContainer.Store.Dispatch(new TrackSelectedAction
            {
                CurrentMusic = new AlarmMusic
                {
                    MusicType = _tentative.MusicType,
                    LanguageCode = _tentative.LanguageCode,
                    PublicationCode = _tentative.PublicationCode,
                    TrackNumber = _tentative.TrackNumber,
                    Repeat = _tentative.Repeat
                }
            });

            IsBusy = false;
        });

        //set schedules from initial state.
        //this should fire only once 
        IDisposable subscription1 = null;
        subscription1 = ReduxContainer.Store.Subscribe(state =>
        {
            if (state.CurrentMusic != null && state.TentativeMusic != null)
            {
                _current = state.CurrentMusic;
                _tentative = state.TentativeMusic;
                Task.Run(async () =>
                {
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
                    await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
                    await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
                });
                _subscriptions.Remove(subscription1);
                subscription1?.Dispose();
            }
        });

        _subscriptions.Add(subscription1);
    }

    public ICommand BackCommand { get; set; }
    public ICommand SetTrackCommand { get; set; }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => this.Set(ref _isBusy, value);
    }

    public ObservableCollection<MusicTrackListViewItemModel> Tracks { get; set; } = [];

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
        Tracks.CollectionChanged += (sender, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (MusicTrackListViewItemModel item in e.NewItems)
                {
                    SubscribeToTrackEvents(item);
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

        // Subscribe to play service stopped event
        _playService.OnStopped += OnPlayServiceStopped;
    }

    private void SubscribeToTrackEvents(MusicTrackListViewItemModel track)
    {
        PropertyChangedEventHandler handler = (sender, e) =>
        {
            if (sender is MusicTrackListViewItemModel item)
            {
                if (e.PropertyName == "Play")
                {
                    if (item.Play)
                    {
                        _ = HandlePlayTrack(item);
                    }
                    else
                    {
                        _playService.Stop();
                    }
                }
                else if (e.PropertyName == "Repeat")
                {
                    HandleRepeatChanged(item);
                }
            }
        };

        track.PropertyChanged += handler;
        _propertyChangedHandlers[track] = handler;
    }

    private void UnsubscribeFromTrackEvents(MusicTrackListViewItemModel track)
    {
        if (_propertyChangedHandlers.TryGetValue(track, out var handler))
        {
            track.PropertyChanged -= handler;
            _propertyChangedHandlers.Remove(track);
        }
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

        ReduxContainer.Store.Dispatch(new TrackSelectedAction
        {
            CurrentMusic = new AlarmMusic
            {
                MusicType = _tentative.MusicType,
                LanguageCode = _tentative.LanguageCode,
                PublicationCode = _tentative.PublicationCode,
                TrackNumber = _tentative.TrackNumber,
                Repeat = _tentative.Repeat
            }
        });

        if (track.Repeat) _toastService.ShowMessage("Alarm will always repeat this track.");
    }

    private async void OnPlayServiceStopped()
    {
        await _lock.WaitAsync();

        try
        {
            if (_currentlyPlaying != null)
            {
                _currentlyPlaying.Play = false;
                _currentlyPlaying.IsBusy = false;
                _currentlyPlaying = null;
            }
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

    private async Task PopulateTracks(string languageCode, string publicationCode)
    {
        var isVocal = languageCode != null;

        var tracks = isVocal
            ? await _mediaService.GetVocalMusicTracks(languageCode, publicationCode)
            : await _mediaService.GetMelodyMusicTracks(
                (await _mediaService.GetMelodyMusicReleases()).First().Value.Code);

        var trackVMs = new ObservableCollection<MusicTrackListViewItemModel>();

        if (DeviceInfo.Platform == DevicePlatform.WinUI) Tracks = trackVMs;

        foreach (var track in tracks.Select(x => x.Value))
        {
            var musicTrackListViewItemViewModel = new MusicTrackListViewItemModel(track, !isVocal);

            trackVMs.Add(musicTrackListViewItemViewModel);

            if (_current.MusicType == _tentative.MusicType
                && _current.TrackNumber == track.Number
                && (_current.MusicType == MusicType.Melodies ||
                    (_current.LanguageCode == _tentative.LanguageCode
                     && _current.PublicationCode == _tentative.PublicationCode))
               )
            {
                SelectedTrack = musicTrackListViewItemViewModel;
                SelectedTrack.IsSelected = true;
                SelectedTrack.Repeat = _current.Repeat;
            }
        }

        if (DeviceInfo.Platform != DevicePlatform.WinUI) Tracks = trackVMs;
    }

    public void Dispose()
    {
        _playService.OnStopped -= OnPlayServiceStopped;
        
        foreach (var track in Tracks)
        {
            UnsubscribeFromTrackEvents(track);
        }
        
        _subscriptions.ForEach(x => x.Dispose());
        _propertyChangedHandlers.Clear();

        _lock.Dispose();
        
        // Note: _mediaService (MediaService), _toastService (IToastService), 
        // _playService (IPreviewPlayService), _downloadService (IDownloadService), 
        // and _cacheService (IMediaCacheService) are singletons and should not be 
        // disposed here as they are managed by the DI container
    }
}

public class MusicTrackListViewItemModel : ViewModel, IComparable
{
    private readonly MusicTrack _track;
    private readonly bool _isMelody;

    public MusicTrackListViewItemModel(MusicTrack track, bool isMelody)
    {
        _track = track;
        _isMelody = isMelody;

        TogglePlayCommand = new Command(() => Play = !Play);
        ToggleRepeatCommand = new Command(() => Repeat = !Repeat);
    }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.Set(ref _isSelected, value);
    }

    public string LookUpPath => _track.Source.LookUpPath;
    public int Number => _track.Number;

    public string Title => _isMelody ? $"Melody Number(s) {_track.Title}" : _track.Title;
    public string Url => _track.Source.Url;

    private bool _play;

    public bool Play
    {
        get => _play;
        set => this.Set(ref _play, value);
    }

    private bool _repeat;

    public bool Repeat
    {
        get => _repeat;
        set => this.Set(ref _repeat, value);
    }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => this.Set(ref _isBusy, value);
    }

    public ICommand TogglePlayCommand { get; set; }
    public ICommand ToggleRepeatCommand { get; set; }

    public int CompareTo(object obj)
    {
        return Number.CompareTo((obj as MusicTrackListViewItemModel).Number);
    }
}