using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Mvvmicro;
using Serilog;
using Plugin.StoreReview;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.ViewModels;

public class AlarmViewModal : ViewModel, IDisposableModal
{
    private readonly ILogger _logger;
    private readonly IPlaybackService _playbackService;
    private readonly IServiceScopeFactory _scopeFactory;

    private bool _isDisposed = false;

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
        _logger = logger;
        _playbackService = playbackService;
        _scopeFactory = scopeFactory;

        DismissCommand = new Command(async () =>
        {
            await _playbackService.Dismiss();
            
            using var scope = _scopeFactory.CreateScope();
            var navigationService = scope.ServiceProvider.GetRequiredService<INavigationService>();
            await navigationService?.CloseModal();

            using var scheduleDbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

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
                _logger.Error(e, "An error happened when review was requested.");
            }

            await scheduleDbContext.SaveChangesAsync();
        });

        CancelCommand = new Command(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var navigationService = scope.ServiceProvider.GetRequiredService<INavigationService>();
            await navigationService?.GoBack();
        });

        PlayCommand = new Command(async () =>
        {
            await _playbackService.Play();
            Refresh();
        });

        PauseCommand = new Command(async () =>
        {
            await _playbackService.Pause();
            Refresh();
        });

        PreviousCommand = new Command(async () =>
        {
            await _playbackService.PlayPrevious();
            Refresh();
        });

        NextCommand = new Command(async () =>
        {
            await _playbackService.PlayNext();
            Refresh();
        });

        ForwardCommand = new Command(async () =>
        {
            // MediaElement doesn't have StepForward - using seek instead
            await _playbackService.Play();
            Refresh();
        });

        BackwardCommand = new Command(async () =>
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
        catch
        {
        }
    }

    private string _title;

    public string Title
    {
        get => _title;
        set => this.Set(ref _title, value);
    }

    private string _subTitle;

    public string SubTitle
    {
        get => _subTitle;
        set => this.Set(ref _subTitle, value);
    }

    private string _description;

    public string Description
    {
        get => _description;
        set => this.Set(ref _description, value);
    }

    private bool _playVisible;

    public bool PlayVisible
    {
        get => _playVisible;
        set => this.Set(ref _playVisible, value);
    }

    private bool _pauseVisible;

    public bool PauseVisible
    {
        get => _pauseVisible;
        set => this.Set(ref _pauseVisible, value);
    }

    private string _currentTime;

    public string CurrentTime
    {
        get => _currentTime;
        set => this.Set(ref _currentTime, value);
    }

    private string _endTime;

    public string EndTime
    {
        get => _endTime;
        set => this.Set(ref _endTime, value);
    }

    private double _progress;

    public double Progress
    {
        get => _progress;
        set => this.Set(ref _progress, value);
    }

    private bool _nextEnabled;

    public bool NextEnabled
    {
        get => _nextEnabled;
        set => this.Set(ref _nextEnabled, value);
    }

    private bool _previousEnabled;

    public bool PreviousEnabled
    {
        get => _previousEnabled;
        set => this.Set(ref _previousEnabled, value);
    }

    public void Dispose()
    {
        if (!_isDisposed) _isDisposed = true;
    }
}