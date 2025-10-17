using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using MediaManager;
using Microsoft.EntityFrameworkCore;
using Mvvmicro;
using NLog;
using Plugin.StoreReview;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.ViewModels
{
    public class AlarmViewModal : ViewModel, IDisposableModal
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private readonly IContainer container;
        private readonly IMediaManager mediaManager;
        private readonly IPlaybackService playbackService;

        private bool isDisposed = false;

        public ICommand DismissCommand { get; private set; }
        public ICommand CancelCommand { get; set; }

        public ICommand PlayCommand { get; set; }
        public ICommand PauseCommand { get; set; }
        public ICommand PreviousCommand { get; set; }
        public ICommand NextCommand { get; set; }
        public ICommand ForwardCommand { get; set; }
        public ICommand BackwardCommand { get; set; }

        public AlarmViewModal(IContainer container)
        {
            this.container = container;

            this.playbackService = this.container.Resolve<IPlaybackService>();
            this.mediaManager = this.container.Resolve<IMediaManager>();

            DismissCommand = new Command(async () =>
            {
                await playbackService.Dismiss();
                var navigationService = this.container.Resolve<INavigationService>();
                await navigationService?.CloseModal();

                using var scheduleDbContext = this.container.Resolve<ScheduleDbContext>();

                try
                {
                    if (!await scheduleDbContext.GeneralSettings
                                .AnyAsync(x => x.Key == "ReviewRequested"))
                    {
                        var dismissCount = await scheduleDbContext.GeneralSettings
                                    .FirstOrDefaultAsync(x => x.Key == "DismissCount");

                        if (dismissCount != null && int.Parse(dismissCount.Value) >= 6)
                        {
                            await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings()
                            {
                                Key = "ReviewRequested",
                                Value = "True"
                            });

                            await CrossStoreReview.Current.RequestReview(false);        
                        }
                        else
                        {
                            if (dismissCount != null)
                            {
                                dismissCount.Value = (int.Parse(dismissCount.Value) + 1).ToString();
                            }
                            else
                            {
                                await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings()
                                {
                                    Key = "DismissCount",
                                    Value = "1"
                                });
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happened when review was requested.");
                }

                await scheduleDbContext.SaveChangesAsync();
            });

            CancelCommand = new Command(async () =>
            {
                var navigationService = this.container.Resolve<INavigationService>();
                await navigationService?.GoBack();
            });

            PlayCommand = new Command(() =>
            {
                mediaManager.Play();
                refresh();
            });

            PauseCommand = new Command(() =>
            {
                mediaManager.Pause();
                refresh();
            });

            PreviousCommand = new Command(async () =>
            {
                await mediaManager.PlayPrevious();
                refresh();
            });

            NextCommand = new Command(async () =>
            {
                await mediaManager.PlayNext();
                refresh();
            });

            ForwardCommand = new Command(async () =>
            {
                await mediaManager.StepForward();
                refresh();
            });

            BackwardCommand = new Command(async () =>
            {
                await mediaManager.StepBackward();
                refresh();
            });

            Task.Run(async () =>
            {
                while (!isDisposed)
                {
                    refresh();
                    await Task.Delay(1000);

                    var isRunning = mediaManager.IsPreparedEx();

                    //check for 3 seconds
                    int count = 6;
                    while (!isRunning && count > 0)
                    {
                        await Task.Delay(500);
                        isRunning = mediaManager.IsPreparedEx();
                        count--;
                    }

                    if (!isRunning)
                    {
                        Dispose();
                    }
                }
            });
        }

        private void refresh()
        {
            try
            {
                var mediaItem = mediaManager.Queue?.Current;

                if (mediaItem == null)
                {
                    return;
                }

                Title = mediaItem.DisplayTitle;
                SubTitle = mediaItem.DisplaySubtitle;
                Description = mediaItem.DisplayDescription;

                if (mediaManager.IsPlaying())
                {
                    PlayVisible = false;
                    PauseVisible = true;
                }
                else
                {
                    PlayVisible = true;
                    PauseVisible = false;
                }

                CurrentTime = $"{mediaManager.Position.Minutes:00}:{mediaManager.Position.Seconds:00}";
                EndTime = $"{mediaManager.Duration.Minutes:00}:{mediaManager.Duration.Seconds:00}";

                var progress = mediaManager.Position.TotalMilliseconds / mediaManager.Duration.TotalMilliseconds;
                if (!double.IsNaN(progress) && !double.IsInfinity(progress))
                {
                    Progress = progress;
                }

                NextEnabled = mediaManager.Queue.HasNext;
                PreviousEnabled = mediaManager.Queue.HasPrevious;

            }
            catch { }
        }

        private string title;
        public string Title
        {
            get => title;
            set => this.Set(ref title, value);
        }

        private string subTitle;
        public string SubTitle
        {
            get => subTitle;
            set => this.Set(ref subTitle, value);
        }

        private string description;
        public string Description
        {
            get => description;
            set => this.Set(ref description, value);
        }

        private bool playVisible;
        public bool PlayVisible
        {
            get => playVisible;
            set => this.Set(ref playVisible, value);
        }

        private bool pauseVisible;
        public bool PauseVisible
        {
            get => pauseVisible;
            set => this.Set(ref pauseVisible, value);
        }

        private string currentTime;
        public string CurrentTime
        {
            get => currentTime;
            set => this.Set(ref currentTime, value);
        }
        private string endTime;
        public string EndTime
        {
            get => endTime;
            set => this.Set(ref endTime, value);
        }
        private double progress;
        public double Progress
        {
            get => progress;
            set => this.Set(ref progress, value);
        }

        private bool nextEnabled;
        public bool NextEnabled
        {
            get => nextEnabled;
            set => this.Set(ref nextEnabled, value);
        }

        private bool previousEnabled;
        public bool PreviousEnabled
        {
            get => previousEnabled;
            set => this.Set(ref previousEnabled, value);
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
            }

        }
    }
}
