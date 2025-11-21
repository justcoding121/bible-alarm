using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Database;
using Bible.Alarm.Models;
using Bible.Alarm.Shared.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel;
using Plugin.StoreReview;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared;

public class AlarmViewModal : ObservableObject, IDisposable, 
    IRecipient<MediaProgressMessage>,
    IRecipient<AudioMetadataMessage>,
    IRecipient<AudioPositionMessage>,
    IRecipient<AudioStatusMessage>
{
    private readonly IPlaybackService _playbackService;

    private bool _isDisposed;

    public ICommand DismissCommand { get; private set; }
    public ICommand CancelCommand { get; set; }

    public ICommand PlayCommand { get; set; }
    public ICommand PauseCommand { get; set; }
    public ICommand PreviousCommand { get; set; }
    public ICommand NextCommand { get; set; }
    public ICommand ForwardCommand { get; set; }
    public ICommand BackwardCommand { get; set; }

    public AlarmViewModal(ILogger logger, IPlaybackService playbackService, IServiceScopeFactory scopeFactory)
    {
        _playbackService = playbackService;
        WeakReferenceMessenger.Default.Register(this);
        
        var audioMessenger = AudioPlayer.GetMessenger();
        audioMessenger.Register<AudioMetadataMessage>(this);
        audioMessenger.Register<AudioPositionMessage>(this);
        audioMessenger.Register<AudioStatusMessage>(this);

        DismissCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.Dismiss();
            
            using var scope = scopeFactory.CreateScope();
            var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

            try
            {
                if (!await scheduleDbContext.GeneralSettings
                        .AnyAsync(x => x.Key == AppConstants.GeneralSettingsKeys.ReviewRequested))
                {
                    var dismissCount = await scheduleDbContext.GeneralSettings
                        .FirstOrDefaultAsync(x => x.Key == AppConstants.GeneralSettingsKeys.DismissCount);

                    if (dismissCount != null && int.Parse(dismissCount.Value) >= 6)
                    {
                        await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings
                        {
                            Key = AppConstants.GeneralSettingsKeys.ReviewRequested,
                            Value = "True"
                        });

                        await CrossStoreReview.Current.RequestReview(false);
                    }
                    else
                    {
                        if (dismissCount != null)
                            dismissCount.Value = (int.Parse(dismissCount.Value) + 1).ToString();
                        else
                            await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings
                            {
                                Key = AppConstants.GeneralSettingsKeys.DismissCount,
                                Value = "1"
                            });
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "An error happened when review was requested.");
            }

            await scheduleDbContext.SaveChangesAsync();
        });

        CancelCommand = new RelayCommand(() =>
        {
            // This command doesn't need navigation - the modal will be closed when playback is dismissed
        });

        PlayCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.Play();
        });

        PauseCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.Pause();
        });

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.PlayPrevious();
        });

        NextCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.PlayNext();
        });

        ForwardCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.Play();
        });

        BackwardCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.Pause();
        });

        // Initialize properties with default values
        Title = "";
        SubTitle = "";
        Description = "";
        CurrentTime = "00:00";
        EndTime = "00:00";
        Progress = 0.0;
        PlayVisible = true;
        PauseVisible = false;
        NextEnabled = false;
        PreviousEnabled = false;

        Task.Run(async () =>
        {
            while (!_isDisposed)
            {
                await Task.Delay(1000);

                var isRunning = _playbackService.IsPrepared;

                var count = 6;
                while (!isRunning && count > 0)
                {
                    await Task.Delay(500);
                    isRunning = _playbackService.IsPrepared;
                    count--;
                }

                if (!isRunning) Dispose();
            }
        });
    }

    private void Refresh()
    {
        try
        {
    
            if (_playbackService.IsPlaying)
            {
                PlayVisible = false;
                PauseVisible = true;
            }
            else
            {
                PlayVisible = true;
                PauseVisible = false;
            }

            // Basic time display - MediaElement doesn't have the same properties
            var position = _playbackService.CurrentTrackPosition;
            CurrentTime = $"{position.Minutes:00}:{position.Seconds:00}";

            // For now, set basic values since MediaElement doesn't have all MediaManager properties
            Title = "";
            SubTitle = "";
            Description = "";
            EndTime = "00:00";
            Progress = 0.0;
            NextEnabled = false;
            PreviousEnabled = false;
        }
        catch(Exception e) 
        {
            // Log and ignore refresh errors
            Log.Error(e, "Error refreshing AlarmViewModal.");
        }
    }

    private string _title;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _subTitle;

    public string SubTitle
    {
        get => _subTitle;
        set => SetProperty(ref _subTitle, value);
    }

    private string _description;

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private bool _playVisible;

    public bool PlayVisible
    {
        get => _playVisible;
        set => SetProperty(ref _playVisible, value);
    }

    private bool _pauseVisible;

    public bool PauseVisible
    {
        get => _pauseVisible;
        set => SetProperty(ref _pauseVisible, value);
    }

    private string _currentTime;

    public string CurrentTime
    {
        get => _currentTime;
        set => SetProperty(ref _currentTime, value);
    }

    private string _endTime;

    public string EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, value);
    }

    private double _progress;

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private bool _nextEnabled;

    public bool NextEnabled
    {
        get => _nextEnabled;
        set => SetProperty(ref _nextEnabled, value);
    }

    private bool _previousEnabled;

    public bool PreviousEnabled
    {
        get => _previousEnabled;
        set => SetProperty(ref _previousEnabled, value);
    }

    private bool _isPreparing;
    private int _loadedTracks;
    private int _totalTracks;

    public bool IsPreparing
    {
        get => _isPreparing;
        set => SetProperty(ref _isPreparing, value);
    }

    public string ProgressText => $"Preparing tracks {(_totalTracks > 0 ? $"{_loadedTracks}/{_totalTracks}" : "")}..";
    
    public double PreparationProgress { get; private set; }

    public void Receive(MediaProgressMessage message)
    {
        Task.Run(() =>
        {
            if (message.Value is not Tuple<int, int> kv) return;
            _loadedTracks = kv.Item1;
            _totalTracks = kv.Item2;
            PreparationProgress = _totalTracks > 0 ? _loadedTracks / (double)_totalTracks : 0.0;
            IsPreparing = _totalTracks > 0 && _loadedTracks < _totalTracks;
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(PreparationProgress));
                OnPropertyChanged(nameof(IsPreparing));
            });
        });
    }

    public void Receive(AudioMetadataMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Title = message.Title ?? "";
            SubTitle = message.Artist ?? "";
            Description = message.Album ?? "";
        });
    }

    public void Receive(AudioPositionMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (message.CurrentPosition.HasValue)
            {
                var position = message.CurrentPosition.Value;
                CurrentTime = $"{position.Minutes:00}:{position.Seconds:00}";
            }
            else
            {
                CurrentTime = "00:00";
            }

            var duration = message.Duration;
            EndTime = $"{duration.Minutes:00}:{duration.Seconds:00}";

            if (message.CurrentPosition.HasValue && duration.TotalSeconds > 0)
            {
                Progress = message.CurrentPosition.Value.TotalSeconds / duration.TotalSeconds;
            }
            else
            {
                Progress = 0.0;
            }
        });
    }

    public void Receive(AudioStatusMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var isPlaying = message.Status == PlayStatus.Playing;
            PlayVisible = !isPlaying;
            PauseVisible = isPlaying;
        });
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            WeakReferenceMessenger.Default.Unregister<MediaProgressMessage>(this);
            AudioPlayer.GetMessenger().Unregister<AudioMetadataMessage>(this);
            AudioPlayer.GetMessenger().Unregister<AudioPositionMessage>(this);
            AudioPlayer.GetMessenger().Unregister<AudioStatusMessage>(this);
            _isDisposed = true;
        }
    }
}