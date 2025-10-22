using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
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
        var subscription1 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
            .Select(state => new { state.CurrentMusic, state.TentativeMusic })
            .Where(x => x.CurrentMusic != null && x.TentativeMusic != null)
            .DistinctUntilChanged()
            .Take(1)
            .Subscribe(async x =>
            {
                IsBusy = true;
                _current = x.CurrentMusic;
                _tentative = x.TentativeMusic;
                await Initialize(_tentative.LanguageCode, _tentative.PublicationCode);
                IsBusy = false;
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

        var subscription1 = Tracks.Select(added =>
            {
                return Observable
                    .FromEvent<PropertyChangedEventHandler, KeyValuePair<string, MusicTrackListViewItemModel>>(
                        onNextHandler => (object sender, PropertyChangedEventArgs e)
                            => onNextHandler(new KeyValuePair<string, MusicTrackListViewItemModel>(e.PropertyName,
                                (MusicTrackListViewItemModel)sender)),
                        handler => added.PropertyChanged += handler,
                        handler => added.PropertyChanged -= handler)
                    .Where(kv => kv.Key == "Play")
                    .Select(y => y.Value)
                    .Where(y => y.Play);
            }).Merge()
            .Do(async y =>
            {
                await _lock.WaitAsync();

                try
                {
                    if (_currentlyPlaying != null && _currentlyPlaying != y)
                    {
                        _currentlyPlaying.Play = false;
                        _currentlyPlaying.IsBusy = false;
                    }

                    _currentlyPlaying = y;

                    _currentlyPlaying.IsBusy = true;
                    try
                    {
                        var url = y.Url;

                        await Task.Run(async () =>
                        {
                            if (!await _downloadService.FileExists(url))
                                url = await _cacheService.GetMusicTrackUrl(
                                    _tentative.LanguageCode,
                                    y.LookUpPath);

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
                        _logger.Error(e, "TrackSelectionViewModel 1: @lock disposed error.");
                    }
                }
            })
            .Subscribe();

        var subscription2 = Tracks.Select(added =>
            {
                return Observable
                    .FromEvent<PropertyChangedEventHandler, KeyValuePair<string, MusicTrackListViewItemModel>>(
                        onNextHandler => (object sender, PropertyChangedEventArgs e)
                            => onNextHandler(new KeyValuePair<string, MusicTrackListViewItemModel>(e.PropertyName,
                                (MusicTrackListViewItemModel)sender)),
                        handler => added.PropertyChanged += handler,
                        handler => added.PropertyChanged -= handler)
                    .Where(kv => kv.Key == "Play")
                    .Select(y => y.Value)
                    .Where(y => !y.Play);
            })
            .Merge()
            .Do(y => { _playService.Stop(); })
            .Subscribe();

        var subscription3 = Observable.FromEvent(ev => _playService.OnStopped += ev,
                ev => _playService.OnStopped -= ev)
            .Do(async y =>
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
                        _logger.Error(e, "TrackSelectionViewModel 2: @lock disposed error.");
                    }
                }
            })
            .Subscribe();

        var subscription4 = Tracks.Select(added =>
            {
                return Observable
                    .FromEvent<PropertyChangedEventHandler, KeyValuePair<string, MusicTrackListViewItemModel>>(
                        onNextHandler => (object sender, PropertyChangedEventArgs e)
                            => onNextHandler(new KeyValuePair<string, MusicTrackListViewItemModel>(e.PropertyName,
                                (MusicTrackListViewItemModel)sender)),
                        handler => added.PropertyChanged += handler,
                        handler => added.PropertyChanged -= handler)
                    .Where(kv => kv.Key == "Repeat")
                    .Select(y => y.Value);
            })
            .Merge()
            .Do(x =>
            {
                _tentative.Repeat = x.Repeat;
                _tentative.TrackNumber = x.Number;

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

                if (x.Repeat) _toastService.ShowMessage("Alarm will always repeat this track.");
            })
            .Subscribe();

        _subscriptions.AddRange(new[] { subscription1, subscription2, subscription3, subscription4 });
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
        _subscriptions.ForEach(x => x.Dispose());

        _mediaService.Dispose();
        _toastService.Dispose();
        _playService.Dispose();
        _downloadService.Dispose();
        _cacheService.Dispose();
        _lock.Dispose();
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

    public string LookUpPath => _track.LookUpPath;
    public int Number => _track.Number;

    public string Title => _isMelody ? $"Melody Number(s) {_track.Title}" : _track.Title;
    public string Url => _track.Url;

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