using System.Windows.Input;
using Bible.Alarm.Common.Interfaces.Media;
using Bible.Alarm.Database;
using Bible.Alarm.Models;
using Bible.Alarm.Shared.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Plugin.StoreReview;
using Serilog;

namespace Bible.Alarm.ViewModels.Shared;

public class AlarmViewModal : ObservableObject, IDisposable
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
        var logger1 = logger;
        _playbackService = playbackService;
        var scopeFactory1 = scopeFactory;

        DismissCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.Dismiss();
            
            using var scope = scopeFactory1.CreateScope();
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
                logger1.Error(e, "An error happened when review was requested.");
            }

            await scheduleDbContext.SaveChangesAsync();
        });

        CancelCommand = new Command(() =>
        {
            // This command doesn't need navigation - the modal will be closed when playback is dismissed
        });

        PlayCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.Play();
            Refresh();
        });

        PauseCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.Pause();
            Refresh();
        });

        PreviousCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.PlayPrevious();
            Refresh();
        });

        NextCommand = new AsyncRelayCommand(async () =>
        {
            await _playbackService.PlayNext();
            Refresh();
        });

        ForwardCommand = new AsyncRelayCommand(async () =>
        {
            // MediaElement doesn't have StepForward - using seek instead
            await _playbackService.Play();
            Refresh();
        });

        BackwardCommand = new AsyncRelayCommand(async () =>
        {
            // MediaElement doesn't have StepBackward - using pause instead
            await _playbackService.Pause();
            Refresh();
        });

        Task.Run(async () =>
        {
            while (!_isDisposed)
            {
                Refresh();
                await Task.Delay(1000);

                var isRunning = _playbackService.IsPrepared;

                //check for 3 seconds
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
            // Simplified refresh for MediaElement
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
            Title = "Audio Playback";
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

    public void Dispose()
    {
        if (!_isDisposed) _isDisposed = true;
    }
}