#nullable enable
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Database;
using Bible.Alarm.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel;
using Plugin.StoreReview;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared;

public class AlarmViewModal : ObservableObject, IDisposable
{
    private readonly IPlaybackService _playbackService;
    private readonly IState<PlaybackState> _playbackState;

    private bool _isDisposed;

    public ICommand DismissCommand { get; private set; }
    public ICommand CancelCommand { get; set; }

    public ICommand PlayCommand { get; set; }
    public ICommand PauseCommand { get; set; }
    public ICommand PreviousCommand { get; set; }
    public ICommand NextCommand { get; set; }
    public ICommand ForwardCommand { get; set; }
    public ICommand BackwardCommand { get; set; }

    public AlarmViewModal(ILogger logger, IPlaybackService playbackService, IServiceScopeFactory scopeFactory, IState<PlaybackState> playbackState)
    {
        _playbackService = playbackService;
        _playbackState = playbackState;
        
        // Initialize string fields to avoid nullable warnings
        _title = "";
        _subTitle = "";
        _description = "";
        _currentTime = "00:00";
        _endTime = "00:00";
        
        // Subscribe to Fluxor state changes for reactive updates
        _playbackState.StateChanged += OnPlaybackStateChanged;
        
        // Initialize from current state
        UpdateFromState();

        DismissCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.StopAsync();
            
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
            await _playbackService.PlayAsync();
        });

        PauseCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.PauseAsync();
        });

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.PlayPreviousAsync();
        });

        NextCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.PlayNextAsync();
        });

        ForwardCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.SeekForwardAsync();
        });

        BackwardCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.SeekBackwardAsync();
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

                var isRunning = _playbackState.Value.IsPreparingOrPlaying;

                var count = 6;
                while (!isRunning && count > 0)
                {
                    await Task.Delay(500);
                    isRunning = _playbackState.Value.IsPreparingOrPlaying;
                    count--;
                }

                if (!isRunning) Dispose();
            }
        });
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
        set
        {
            if (SetProperty(ref _isPreparing, value))
            {
                OnPropertyChanged(nameof(AreControlsVisible));
            }
        }
    }

    /// <summary>
    /// Controls are visible when not preparing tracks
    /// </summary>
    public bool AreControlsVisible => !IsPreparing;

    public string ProgressText => $"Preparing tracks {(_totalTracks > 0 ? $"{_loadedTracks}/{_totalTracks}" : "")}..";
    
    public double PreparationProgress { get; private set; }

    private string _errorMessage = string.Empty;
    
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }
    
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        UpdateFromState();
    }
    
    private void UpdateFromState()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var state = _playbackState.Value;
            
            // Update navigation controls
            NextEnabled = state.CanPlayNext;
            PreviousEnabled = state.CanPlayPrevious;
            
            // Update metadata
            Title = state.Title ?? "";
            SubTitle = state.Artist ?? "";
            Description = state.Album ?? "";
            
            // Update position and duration
            if (state.CurrentPosition.HasValue)
            {
                var position = state.CurrentPosition.Value;
                CurrentTime = $"{position.Minutes:00}:{position.Seconds:00}";
            }
            else
            {
                CurrentTime = "00:00";
            }

            var duration = state.Duration;
            EndTime = $"{duration.Minutes:00}:{duration.Seconds:00}";

            if (state.CurrentPosition.HasValue && duration.TotalSeconds > 0)
            {
                Progress = state.CurrentPosition.Value.TotalSeconds / duration.TotalSeconds;
            }
            else
            {
                Progress = 0.0;
            }
            
            // Update preparation progress
            _loadedTracks = state.LoadedTracks;
            _totalTracks = state.TotalTracks;
            PreparationProgress = _totalTracks > 0 ? _loadedTracks / (double)_totalTracks : 0.0;
            IsPreparing = state.IsPreparing;
            
            // Update error message
            ErrorMessage = state.ErrorMessage ?? "";
            
            // Update play/pause visibility based on status
            var isPlaying = state.Status == PlayStatus.Playing;
            PlayVisible = !isPlaying;
            PauseVisible = isPlaying;
            
            // Notify property changes
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(PreparationProgress));
            OnPropertyChanged(nameof(HasError));
        });
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _playbackState.StateChanged -= OnPlaybackStateChanged;
            _isDisposed = true;
        }
    }
}