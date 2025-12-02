#nullable enable
using System.IO;
using System.Windows.Input;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Media.Models;
using Bible.Alarm.Database;
using Bible.Alarm.Models;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel;
using Plugin.StoreReview;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Shared;

public class AlarmViewModal : ObservableObject, IDisposable, IRecipient<PlaybackPositionChangedMessage>, IRecipient<PlaybackPreparationProgressMessage>
{
    private readonly ILogger _logger;
    private readonly IPlaybackService _playbackService;
    private readonly IState<PlaybackState> _playbackState;
    private readonly IDispatcher _dispatcher;

    private bool _isDisposed;
    private bool _hasReceivedInitialState;
    private string? _previousTrackTitle;
    private string? _previousTrackArtist;
    private string? _previousTrackAlbum;
    private TimeSpan _currentDuration = TimeSpan.Zero;

    public ICommand DismissCommand { get; private set; }
    public ICommand CancelCommand { get; set; }

    public ICommand PlayCommand { get; set; }
    public ICommand PauseCommand { get; set; }
    public ICommand PreviousCommand { get; set; }
    public ICommand NextCommand { get; set; }
    public ICommand ForwardCommand { get; set; }
    public ICommand BackwardCommand { get; set; }

    public AlarmViewModal(ILogger logger, IPlaybackService playbackService, IServiceScopeFactory scopeFactory, IState<PlaybackState> playbackState, IDispatcher dispatcher)
    {
        _logger = logger;
        _playbackService = playbackService;
        _playbackState = playbackState;
        _dispatcher = dispatcher;
        
        // Initialize string fields to avoid nullable warnings
        _title = "";
        _subTitle = "";
        _description = "";
        _currentTime = "00:00";
        _endTime = "00:00";
        
        // Controls are hidden until first playback state is received
        _hasReceivedInitialState = false;
        
        // Subscribe to Fluxor state changes for reactive updates
        _playbackState.StateChanged += OnPlaybackStateChanged;
        
        // Subscribe to position and preparation progress messages (high-frequency updates)
        WeakReferenceMessenger.Default.Register<PlaybackPositionChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<PlaybackPreparationProgressMessage>(this);
        
        // Initialize from current state
        UpdateFromState();

        DismissCommand = new AsyncRelayCommand(async () =>
        {
            // Set IsBusy immediately to show progress indicator right away
            IsBusy = true;
            // Force property change notification to ensure UI updates immediately
            OnPropertyChanged(nameof(IsBusy));
            // Give UI time to render the progress indicator before starting dismiss operation
            await Task.Delay(100);
            
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

    private ImageSource? _artworkSource;
    private bool _isArtworkLoading;
    private byte[]? _artworkBytes; // Keep bytes in memory for stream-based images
    private string? _lastArtworkUrl; // Track last artwork URL to avoid unnecessary updates

    public ImageSource? ArtworkSource
    {
        get => _artworkSource;
        private set
        {
            if (SetProperty(ref _artworkSource, value))
            {
                // Update loading state when artwork source changes
                IsArtworkLoading = false;
                OnPropertyChanged(nameof(HasArtwork));
            }
        }
    }

    public bool IsArtworkLoading
    {
        get => _isArtworkLoading;
        private set
        {
            if (SetProperty(ref _isArtworkLoading, value))
            {
                OnPropertyChanged(nameof(HasArtwork));
            }
        }
    }

    public bool HasArtwork => ArtworkSource != null && !IsArtworkLoading;

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

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(AreControlsEnabled));
            }
        }
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
                OnPropertyChanged(nameof(AreControlsEnabled));
            }
        }
    }

    /// <summary>
    /// Controls are enabled when initial state has been received, not preparing tracks, there's no error, and not busy (dismissing)
    /// </summary>
    public bool AreControlsEnabled => _hasReceivedInitialState && !IsPreparing && !HasError && !IsBusy;

    public string ProgressText => $"Preparing tracks {(_totalTracks > 0 ? $"{_loadedTracks}/{_totalTracks}" : "")}..";
    
    public double PreparationProgress { get; private set; }

    private string _errorMessage = string.Empty;
    
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(AreControlsEnabled));
            }
        }
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
            
            // Detect track change by comparing current metadata with previous
            var currentTitle = state.Title ?? "";
            var currentArtist = state.Artist ?? "";
            var currentAlbum = state.Album ?? "";
            
            var trackChanged = _hasReceivedInitialState && 
                               (_previousTrackTitle != currentTitle || 
                                _previousTrackArtist != currentArtist || 
                                _previousTrackAlbum != currentAlbum);
            
            if (trackChanged)
            {
                // Track has changed - disable controls until we receive play-related event for new track
                _hasReceivedInitialState = false;
                OnPropertyChanged(nameof(AreControlsEnabled));
            }
            
            // Mark that we've received initial state only when playback is actually playing
            // This ensures controls are disabled until playback actually starts (not just when track metadata is available)
            if (!_hasReceivedInitialState && state.Status == PlayStatus.Playing)
            {
                _hasReceivedInitialState = true;
                OnPropertyChanged(nameof(AreControlsEnabled));
            }
            
            // Update previous track metadata only after we've received initial state for the new track
            // This ensures we can detect the next track change correctly
            if (_hasReceivedInitialState)
            {
                _previousTrackTitle = currentTitle;
                _previousTrackArtist = currentArtist;
                _previousTrackAlbum = currentAlbum;
            }
            
            // Update navigation controls
            NextEnabled = state.CanPlayNext;
            PreviousEnabled = state.CanPlayPrevious;
            
            // Update metadata
            Title = currentTitle;
            SubTitle = currentArtist;
            Description = currentAlbum;
            
            // Update artwork
            UpdateArtwork(state.ArtworkUrl);
            
            // Update duration (from Fluxor state, only changes when track changes)
            var duration = state.Duration;
            _currentDuration = duration;
            EndTime = $"{duration.Minutes:00}:{duration.Seconds:00}";
            
            // Note: Position and PreparationProgress are updated via messages (high-frequency updates)
            
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
            OnPropertyChanged(nameof(AreControlsEnabled));
        });
    }

    public void Receive(PlaybackPositionChangedMessage message)
    {
        // Handle high-frequency position updates via messaging
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (message.CurrentPosition.HasValue)
            {
                var position = message.CurrentPosition.Value;
                CurrentTime = $"{position.Minutes:00}:{position.Seconds:00}";
                
                // Update progress based on current position and duration
                if (_currentDuration.TotalSeconds > 0)
                {
                    Progress = position.TotalSeconds / _currentDuration.TotalSeconds;
                }
                else
                {
                    Progress = 0.0;
                }
            }
            else
            {
                CurrentTime = "00:00";
                Progress = 0.0;
            }
            
            // Notify property changes
            OnPropertyChanged(nameof(ProgressText));
        });
    }

    public void Receive(PlaybackPreparationProgressMessage message)
    {
        // Handle high-frequency preparation progress updates via messaging
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _loadedTracks = message.LoadedTracks;
            _totalTracks = message.TotalTracks;
            PreparationProgress = _totalTracks > 0 ? _loadedTracks / (double)_totalTracks : 0.0;
            IsPreparing = _totalTracks > 0 && _loadedTracks < _totalTracks;
            
            // Notify property changes
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(PreparationProgress));
        });
    }

    private void UpdateArtwork(string? artworkUrl)
    {
        // Avoid unnecessary updates if URL hasn't changed
        if (_lastArtworkUrl == artworkUrl)
        {
            return;
        }

        var previousUrl = _lastArtworkUrl;
        _lastArtworkUrl = artworkUrl;
        
        if (string.IsNullOrEmpty(artworkUrl))
        {
            // Only clear if we currently have artwork displayed
            if (_artworkSource != null)
            {
                ClearArtwork();
            }
            return;
        }

        // Only clear existing artwork if we're switching to a different artwork
        // Don't clear if we're loading artwork for the first time (to avoid showing bell icon briefly)
        if (_artworkSource != null && !string.IsNullOrEmpty(previousUrl) && previousUrl != artworkUrl)
        {
            ClearArtwork();
        }

        IsArtworkLoading = true;

        try
        {
            if (TryLoadFromUri(artworkUrl))
            {
                return;
            }

            var filePath = ResolveFilePath(artworkUrl);
            if (!string.IsNullOrEmpty(filePath))
            {
                LoadFromFile(filePath);
            }
            else
            {
                ClearArtwork();
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error updating artwork from URL: {ArtworkUrl}", artworkUrl);
            ClearArtwork();
        }
    }

    private void ClearArtwork()
    {
        ArtworkSource = null;
        _artworkBytes = null;
        IsArtworkLoading = false;
        _lastArtworkUrl = null; // Reset so we can reload if needed
    }

    private bool TryLoadFromUri(string artworkUrl)
    {
        if (!Uri.TryCreate(artworkUrl, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return false;
        }

        ArtworkSource = ImageSource.FromUri(uri);
        IsArtworkLoading = false;
        return true;
    }

    private string? ResolveFilePath(string artworkUrl)
    {
        // Handle file:// URIs
        if (artworkUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return new Uri(artworkUrl).LocalPath;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to convert file:// URI to local path: {ArtworkUrl}", artworkUrl);
                return null;
            }
        }

        // Handle direct file paths
        if (Path.IsPathRooted(artworkUrl))
        {
            return artworkUrl;
        }

        return null;
    }

    private void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ClearArtwork();
            return;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            ClearArtwork();
            return;
        }

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            LoadFromFileAndroid(filePath);
        }
        else
        {
            LoadFromFileOtherPlatforms(filePath);
        }
    }

    private void LoadFromFileAndroid(string filePath)
    {
        try
        {
            _artworkBytes = File.ReadAllBytes(filePath);
            if (_artworkBytes == null || _artworkBytes.Length == 0)
            {
                ClearArtwork();
                return;
            }

            // Store bytes in field to keep them alive, create new stream each time
            var bytes = _artworkBytes; // Capture for lambda
            ArtworkSource = ImageSource.FromStream(() => new MemoryStream(bytes));
            IsArtworkLoading = false;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to create ImageSource from stream for Android, trying FromFile fallback: {FilePath}", filePath);
            _artworkBytes = null;
            LoadFromFileFallback(filePath);
        }
    }

    private void LoadFromFileOtherPlatforms(string filePath)
    {
        try
        {
            ArtworkSource = ImageSource.FromFile(filePath);
            IsArtworkLoading = false;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to create ImageSource from file for artwork: {FilePath}", filePath);
            ClearArtwork();
        }
    }

    private void LoadFromFileFallback(string filePath)
    {
        try
        {
            ArtworkSource = ImageSource.FromFile(filePath);
            IsArtworkLoading = false;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to create ImageSource from file for artwork (fallback): {FilePath}", filePath);
            ClearArtwork();
        }
    }

    /// <summary>
    /// Hides the Home page overlay. Called when the Alarm Modal is fully rendered and visible.
    /// </summary>
    public void HideHomePageOverlay()
    {
        _dispatcher.Dispatch(new SetHomePageOverlayAction { IsVisible = false });
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _playbackState.StateChanged -= OnPlaybackStateChanged;
            WeakReferenceMessenger.Default.Unregister<PlaybackPositionChangedMessage>(this);
            WeakReferenceMessenger.Default.Unregister<PlaybackPreparationProgressMessage>(this);
            _isDisposed = true;
        }
    }
}