using Bible.Alarm.Common.DataStructures;
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Common.Mvvm;
using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions;
using MediaManager;
using Microsoft.EntityFrameworkCore;
using Mvvmicro;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.ViewModels
{
    public class HomeViewModel : ViewModel, IDisposable
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        private IContainer container;

        private ScheduleDbContext scheduleDbContext;
        private MediaDbContext mediaDbContext;

        private IToastService popUpService;
        private INavigationService navigationService;
        private IMediaCacheService mediaCacheService;
        private IAlarmService alarmService;

        private INotificationService notificationService;

        private List<IDisposable> subscriptions = new List<IDisposable>();


        public HomeViewModel(IContainer container,
            ScheduleDbContext scheduleDbContext,
            IToastService popUpService, INavigationService navigationService,
            IMediaCacheService mediaCacheService,
            IAlarmService alarmService,
            INotificationService notificationService,
            MediaDbContext mediaDbContext)
        {
            this.container = container;
            this.scheduleDbContext = scheduleDbContext;
            this.popUpService = popUpService;
            this.navigationService = navigationService;
            this.mediaCacheService = mediaCacheService;
            this.alarmService = alarmService;
            this.notificationService = notificationService;
            this.mediaDbContext = mediaDbContext;

            subscriptions.Add(scheduleDbContext);

            AddScheduleCommand = new Command(async () =>
            {
                ReduxContainer.Store.Dispatch(new ViewScheduleAction());
                var viewModel = this.container.Resolve<ScheduleViewModel>();
                await this.navigationService.Navigate(viewModel);
            });

            ViewScheduleCommand = new Command<ScheduleListItem>(async x =>
            {
                x.Schedule.IsEnabled = x.IsEnabled;

                ReduxContainer.Store.Dispatch(new ViewScheduleAction()
                {
                    SelectedScheduleListItem = x
                });

                var viewModel = this.container.Resolve<ScheduleViewModel>();
                await this.navigationService.Navigate(viewModel);

            });


            //set schedules from initial state.
            //this should fire only once (look at the where condition).
            var subscription = ReduxContainer.Store
               .Select(state => state.Schedules)
               .Where(x => x != null)
               .DistinctUntilChanged()
               .Subscribe(x =>
               {
                   Schedules = x;
                   listenIsEnabledChanges();
                   IsBusy = false;
               });
            subscriptions.Add(subscription);

            initialize();
        }

        private async Task seedDefaultAlarm()
        {
            if (!await scheduleDbContext.AlarmSchedules.AnyAsync()
                && !await scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == "AlarmSeeded")
                //for existing apps before version 1.30
                && !await scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == "AndroidBatteryOptimizationExclusionPromptShown"))
            {
                var schedule = await AlarmSchedule.GetSampleSchedule(false, mediaDbContext);

                await scheduleDbContext.AlarmSchedules.AddAsync(schedule);
                await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings()
                {
                    Key = "AlarmSeeded",
                    Value = "True"
                });

                await scheduleDbContext.SaveChangesAsync();
            }
        }

        private ObservableHashSet<ScheduleListItem> schedules;
        public ObservableHashSet<ScheduleListItem> Schedules
        {
            get => schedules;
            set => this.Set(ref schedules, value);
        }

        private bool isBusy = true;
        public bool IsBusy
        {
            get => isBusy;
            set
            {
                this.Set(ref isBusy, value);
                Loaded = !isBusy;
            }
        }


        private bool loaded = false;
        public bool Loaded
        {
            get => loaded;
            set => this.Set(ref loaded, value);
        }

        public ICommand AddScheduleCommand { get; set; }
        public ICommand ViewScheduleCommand { get; set; }

        private ScheduleViewModel selectedSchedule;

        public ScheduleViewModel SelectedSchedule
        {
            get => selectedSchedule;
            set => this.Set(ref selectedSchedule, value);
        }

        private bool initialized = false;
        private SemaphoreSlim @lock = new SemaphoreSlim(1);
        private void initialize()
        {
            Messenger<bool>.Subscribe(MvvmMessages.Initialized, async vm =>
            {
                await @lock.WaitAsync();

                try
                {
                    if (!initialized)
                    {
                        await seedDefaultAlarm();

                        var alarmSchedules = await scheduleDbContext.AlarmSchedules
                                            .Include(x => x.BibleReadingSchedule)
                                            .Include(x => x.Music)
                                            .ToListAsync();

                        if (CurrentDevice.RuntimePlatform == Device.Android)
                        {
                            //bible gateway is not supported anymore due to copyright issues
                            var toRemove = alarmSchedules.Where(x => BgSourceHelper.PublicationCodeToNameMappings.Any(y => y.Key == x.BibleReadingSchedule.PublicationCode)).ToList();

                            if (toRemove.Any())
                            {
                                foreach (var item in toRemove)
                                {
                                    item.BibleReadingSchedule.PublicationCode = "bi12";
                                    item.BibleReadingSchedule.FinishedDuration = TimeSpan.Zero;
                                }

                                await scheduleDbContext.SaveChangesAsync();
                            }
                        }

                        var initialSchedules = new ObservableHashSet<ScheduleListItem>();
                        foreach (var schedule in alarmSchedules)
                        {
                            initialSchedules.Add(new ScheduleListItem(container, schedule));
                        }

                        ReduxContainer.Store.Dispatch(new InitializeAction() { ScheduleList = initialSchedules });

                        initialized = true;
                    }
                }
                finally
                {
                    try
                    {
                        @lock.Release();
                    }
                    catch (ObjectDisposedException e)
                    {
                        logger.Error(e, "HomeViewModel: @lock disposed error.");
                    }
                }

            }, true);
        }

        private void listenIsEnabledChanges()
        {
            var scheduleListChangedObservable = Observable.FromEventPattern((EventHandler<NotifyCollectionChangedEventArgs> ev)
                            => new NotifyCollectionChangedEventHandler(ev),
                                  ev => Schedules.CollectionChanged += ev,
                                  ev => Schedules.CollectionChanged -= ev);

            //for schedules currently shown on screen.
            var isEnabledObservable = Schedules.Select(item =>
            {
                var removedObservable = scheduleListChangedObservable.Any(z =>
                {
                    var oldItem = z.EventArgs.OldItems?.Cast<ScheduleListItem>();
                    return oldItem != null && oldItem.Any(removed => item == removed);
                });

                //observe until the schedule is removed from the list.
                return Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, ScheduleListItem>>(
                               onNextHandler => (object sender, PropertyChangedEventArgs e)
                                             => onNextHandler(new KeyValuePair<string, ScheduleListItem>(e.PropertyName, (ScheduleListItem)sender)),
                                               handler => item.PropertyChanged += handler,
                                               handler => item.PropertyChanged -= handler)
                                               .TakeUntil(removedObservable)
                                               .Where(kv => kv.Key == "IsEnabled")
                                               .Select(y => y.Value);
            }).Merge();

            //observe for all future schedules. 
            var isEnableObservableForNewSchedules = scheduleListChangedObservable
                                .SelectMany(x =>
                                {
                                    var newItems = x.EventArgs.NewItems?.Cast<ScheduleListItem>();
                                    if (newItems == null)
                                    {
                                        return Enumerable.Empty<IObservable<ScheduleListItem>>();
                                    }

                                    //observe until the schedule is removed from the list.
                                    return newItems.Select(added =>
                                    {
                                        var removedObservable = scheduleListChangedObservable.Any(z =>
                                        {
                                            var oldItem = z.EventArgs.OldItems?.Cast<ScheduleListItem>();
                                            return oldItem != null && oldItem.Any(removed => added == removed);
                                        });

                                        return Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, ScheduleListItem>>(
                                                       onNextHandler => (object sender, PropertyChangedEventArgs e)
                                                                     => onNextHandler(new KeyValuePair<string, ScheduleListItem>(e.PropertyName, (ScheduleListItem)sender)),
                                                                       handler => added.PropertyChanged += handler,
                                                                       handler => added.PropertyChanged -= handler)
                                                                       .TakeUntil(removedObservable)
                                                                       .Where(kv => kv.Key == "IsEnabled")
                                                                       .Select(y => y.Value);
                                    });
                                })
                                 .Merge();

            //now the actual job (show the scheduled notification).
            var subscription = Observable.Merge(isEnabledObservable, isEnableObservableForNewSchedules)
                                 .ObserveOn(Scheduler.CurrentThread)
                                 .Do(async y =>
                                 {
                                     IsBusy = true;

                                     if (y.IsEnabled &&
                                       (CurrentDevice.RuntimePlatform == Device.iOS
                                       || CurrentDevice.RuntimePlatform == Device.WinUI)
                                       && !await notificationService.CanSchedule())
                                     {
                                         y.IsEnabled = false;

                                         if (CurrentDevice.RuntimePlatform == Device.iOS)
                                         {
                                             await popUpService.ShowMessage("Cannot schedule alarm because you've disabled notifications. " +
                                                 "Please enable notification for this app under system settings.", 7);
                                         }
                                         else
                                         {
                                             await popUpService.ShowMessage("Cannot schedule alarm because you've denied backgroud apps permission. " +
                                               "Please grant background apps permission for this app under system settings.", 7);
                                         }

                                         IsBusy = false;
                                         return;
                                     }

                                     await Task.Run(async () =>
                                     {
                                         using var scheduleDbContext = container.Resolve<ScheduleDbContext>();
                                         var existing = await scheduleDbContext.AlarmSchedules.FirstAsync(x => x.Id == y.ScheduleId);
                                         existing.IsEnabled = y.IsEnabled;
                                         await scheduleDbContext.SaveChangesAsync();

                                         alarmService.Update(existing);
                                     });

                                     if (y.IsEnabled)
                                     {
                                         await popUpService.ShowScheduledNotification(y.Schedule);
                                     }

                                     setupMediaCache(y.Schedule.Id);

                                     y.RaisePropertiesChangedEvent();

                                     IsBusy = false;
                                 })
                                .Subscribe();

            subscriptions.Add(subscription);
        }

        private void setupMediaCache(long scheduleId)
        {
            Task.Run(async () =>
            {
                try
                {
                    using var mediaCacheService = container.Resolve<IMediaCacheService>();
                    await mediaCacheService.SetupAlarmCache(scheduleId);
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happened in SetupAlarmCache task.");
                }
            });
        }


        public void Dispose()
        {
            subscriptions.ForEach(x => x.Dispose());

            this.scheduleDbContext.Dispose();
            this.popUpService.Dispose();
            this.mediaCacheService.Dispose();
            this.alarmService.Dispose();
            this.notificationService.Dispose();
            this.mediaDbContext.Dispose();

            @lock.Dispose();
        }
    }

    public class ScheduleListItem : ViewModel, IComparable, IDisposable
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;


        private readonly IContainer container;

        public AlarmSchedule Schedule;
        private IDisposable subscription;
        public ScheduleListItem(IContainer container, AlarmSchedule schedule)
        {
            this.container = container;

            Schedule = schedule;
            isEnabled = schedule.IsEnabled;

            PlayCommand = new Command(() =>
            {
                _ = Task.Run(async () =>
                {
                    using var toastService = container.Resolve<IToastService>();

                    try
                    {
                        if (Schedule.Id > 0)
                        {
                            var mediaManager = container.Resolve<IMediaManager>();

                            await toastService.ShowMessage("Your schedule will start playing in a few seconds.", 5);

                            using var notificationService = container.Resolve<INotificationService>();
                            await notificationService.ShowNotification(Schedule.Id);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Info(e, "An error happenned when playing alarm.");
                        await toastService.ShowMessage("Error. Network may not be available." +
                            "Please try again.", 5);
                    }
                });
            });

            RefreshChapterName(true);

            subscription = Messenger<object>.Subscribe(MvvmMessages.TrackChanged,
                   (x) =>
                   {
                       if ((int)x == Schedule.Id)
                       {
                           RefreshChapterName();
                       }

                       return Task.CompletedTask;
                   });

            PreviousCommand = new Command(async () =>
            {
                if (await canMove(schedule.Id))
                {
                    using var playlistService = container.Resolve<IPlaylistService>();
                    await playlistService.MoveToPreviousBibleChapter(schedule.Id);

                    RefreshChapterName(true);
                }
            });

            NextCommand = new Command(async () =>
            {
                if (await canMove(schedule.Id))
                {
                    using var playlistService = container.Resolve<IPlaylistService>();
                    await playlistService.MoveToNextBibleChapter(schedule.Id);

                    RefreshChapterName(true);
                }
            });
        }

        private async Task<bool> canMove(long scheduleId)
        {
            var playbackService = container.Resolve<IPlaybackService>();
            
            if(!playbackService.IsPlaying)
            {
                return true;
            }

            var toastService = container.Resolve<IToastService>();

            await toastService.ShowMessage("Cannot update the chapter when schedule is in progress.");

            return false;
        }

        public long ScheduleId => Schedule.Id;

        public string Name => Schedule.Name;

        public string SubTitle { get; private set; }

        private bool isEnabled;
        public bool IsEnabled
        {
            get => isEnabled;
            set => this.Set(ref isEnabled, value);
        }

        public DaysOfWeek DaysOfWeek => Schedule.DaysOfWeek;

        public string TimeText => Schedule.TimeText;

        public string Hour => Schedule.MeridienHour.ToString("D2");

        public string Minute => Schedule.Minute.ToString("D2");

        public Meridien Meridien => Schedule.Meridien;

        public ScheduleListItem This => this;

        public ICommand PlayCommand { get; private set; }

        public ICommand PreviousCommand { get; set; }
        public ICommand NextCommand { get; set; }

        public void RaisePropertiesChangedEvent()
        {
            RaiseProperties(GetType()
                .GetProperties()
                .Where(x => x.Name != "IsEnabled")
                .Select(x => x.Name).ToArray());
        }

        public void RefreshChapterName(bool force = false)
        {
            var syncContext = this.container.Resolve<TaskScheduler>();

            _ = Task.Run(async () =>
             {
                 try
                 {
                     var mediaManager = container.Resolve<IMediaManager>();

                     if (Schedule == null || (!force && !mediaManager.IsPreparedEx()))
                     {
                         return null;
                     }

                     using var scheduleDbContext = container.Resolve<ScheduleDbContext>();

                     var schedule = await scheduleDbContext.AlarmSchedules
                                             .Include(x => x.BibleReadingSchedule)
                                             .AsNoTracking()
                                             .Where(x => x.Id == Schedule.Id)
                                             .FirstOrDefaultAsync();

                     if (schedule != null)
                     {
                         using var mediaDbContext = container.Resolve<MediaDbContext>();

                         var bookName = await mediaDbContext.BibleBook
                                         .Where(x => x.BibleTranslation.Code == schedule.BibleReadingSchedule.PublicationCode
                                                 && x.BibleTranslation.Language.Code == schedule.BibleReadingSchedule.LanguageCode
                                                 && x.Number == schedule.BibleReadingSchedule.BookNumber)
                                         .Select(x => x.Name)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync();

                         if (bookName != null)
                         {
                             Schedule.BibleReadingSchedule.BookNumber = schedule.BibleReadingSchedule.BookNumber;
                             Schedule.BibleReadingSchedule.ChapterNumber = schedule.BibleReadingSchedule.ChapterNumber;
                             return new Tuple<string, int>(bookName, schedule.BibleReadingSchedule.ChapterNumber);
                         }
                     }

                 }
                 catch (Exception e)
                 {
                     logger.Error(e, "An error happened in RefreshChapterName task under list item.");
                 }

                 return null;
             })
              .ContinueWith((x) =>
              {
                  try
                  {
                      if (x.IsCompleted && x.Result != null)
                      {
                          SubTitle = $"{x.Result.Item1} {x.Result.Item2}";
                          RaiseProperty("SubTitle");
                      }
                  }
                  catch (Exception e)
                  {
                      logger.Error(e, "An error happened in RefreshChapterName continue with task under list item.");
                  }

              }, syncContext);
        }

        public int CompareTo(object obj)
        {
            return ScheduleId.CompareTo((obj as ScheduleListItem).ScheduleId);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
