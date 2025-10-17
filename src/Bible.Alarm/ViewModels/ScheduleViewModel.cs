
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Contracts.Battery;
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
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.ViewModels
{
    public class ScheduleViewModel : ViewModel, IDisposable
    {
        private static readonly Lazy<Logger> lazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger logger => lazyLogger.Value;

        private readonly IContainer container;

        ScheduleDbContext scheduleDbContext;
        MediaDbContext mediaDbContext;

        IAlarmService alarmService;
        IToastService popUpService;
        INavigationService navigationService;
        IPlaybackService playbackService;
        IMediaManager mediaManager;
        INotificationService notificationService;

        private List<IDisposable> subscriptions = new List<IDisposable>();

        public Command BatteryOptimizationExcludeCommand { get; private set; }
        public Command BatteryOptimizationDismissCommand { get; private set; }

        public ICommand PreviousBookCommand { get; set; }
        public ICommand NextBookCommand { get; set; }

        public ICommand PreviousChapterCommand { get; set; }
        public ICommand NextChapterCommand { get; set; }

        private IBatteryOptimizationManager batteryOptimizationManager;

        public ScheduleViewModel(IContainer container)
        {
            this.container = container;

            if (CurrentDevice.RuntimePlatform == Device.Android)
            {
                this.batteryOptimizationManager = container.Resolve<IBatteryOptimizationManager>();
            }

            this.scheduleDbContext = this.container.Resolve<ScheduleDbContext>();
            this.mediaDbContext = this.container.Resolve<MediaDbContext>();
            this.popUpService = this.container.Resolve<IToastService>();
            this.alarmService = this.container.Resolve<IAlarmService>();
            this.navigationService = this.container.Resolve<INavigationService>();
            this.playbackService = this.container.Resolve<IPlaybackService>();
            this.mediaManager = this.container.Resolve<IMediaManager>();
            this.notificationService = this.container.Resolve<INotificationService>();

            subscriptions.Add(scheduleDbContext);

            //set schedules from initial state.
            //this should fire only once (look at the where condition).
            var subscription = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
               .Select(state => state.CurrentScheduleListItem)
               .DistinctUntilChanged()
               .Take(1)
               .Subscribe(async x =>
               {
                   scheduleListItem = x;

                   var model = x?.Schedule;

                   IsNewSchedule = model == null ? true : false;

                   AlarmSchedule modelToSet;

                   if (model == null)
                   {
                       modelToSet = await AlarmSchedule.GetSampleSchedule(true, mediaDbContext);
                   }
                   else
                   {
                       modelToSet = model;
                   }

                   setModel(modelToSet);

                   IsBusy = false;
               });

            subscriptions.Add(subscription);

            var subscription2 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
           .Select(state => state.CurrentMusic)
           .Where(x => x != null && x != Music)
           .Subscribe(x =>
           {
               Music = x;
               musicUpdated = true;
           });

            subscriptions.Add(subscription2);

            var subscription3 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
            .Select(state => state.CurrentBibleReadingSchedule)
            .Where(x => x != null && x != BibleReadingSchedule)
            .DistinctUntilChanged()
            .Subscribe(x =>
            {
                BibleReadingSchedule = x;
                bibleReadingUpdated = true;
                refreshChapterName();
            });

            subscriptions.Add(subscription3);

            CancelCommand = new Command(async () =>
            {
                IsBusy = true;

                await navigationService.GoBack();

                IsBusy = false;
            });

            SaveCommand = new Command(async () =>
            {
                IsBusy = true;

                if (IsEnabled &&
                      (CurrentDevice.RuntimePlatform == Device.iOS
                        || CurrentDevice.RuntimePlatform == Device.WinUI)
                     && !await notificationService.CanSchedule())
                {
                    IsEnabled = false;
                }

                if (!IsNewSchedule)
                {
                    if (this.mediaManager.IsPreparedEx()
                        && scheduleId == this.playbackService.CurrentlyPlayingScheduleId)
                    {
                        await this.playbackService.Dismiss();
                    }
                }

                var saved = await saveAsync();

                if (saved)
                {
                    await navigationService.GoBack();
                }

                if (saved && IsEnabled)
                {
                    await popUpService.ShowScheduledNotification(Model);
                }

                IsBusy = false;
            });

            DeleteCommand = new Command(async () =>
            {
                IsBusy = true;

                if (this.mediaManager.IsPreparedEx()
                       && scheduleId == this.playbackService.CurrentlyPlayingScheduleId)
                {
                    await this.playbackService.Dismiss();
                }

                await deleteAsync();

                await navigationService.GoBack();

                IsBusy = false;
            });

            ToggleDayCommand = new Command<DaysOfWeek>(x =>
            {
                toggle(x);
            });

            ToggleAlwaysPlayFromStartCommand = new Command(x => AlwaysPlayFromStart = !AlwaysPlayFromStart);

            SelectMusicCommand = new Command(async () =>
            {
                IsBusy = true;

                var viewModel = this.container.Resolve<MusicSelectionViewModel>();
                await navigationService.Navigate(viewModel);

                await Task.Run(async () =>
                {
                    //get the latest music track
                    if (Music == null || (!IsNewSchedule && !musicUpdated))
                    {
                        Music = await scheduleDbContext.AlarmMusic
                        .AsNoTracking()
                        .FirstAsync(x => x.AlarmScheduleId == scheduleId);
                    }
                });


                ReduxContainer.Store.Dispatch(new MusicSelectionAction()
                {
                    CurrentMusic = Music
                });

                IsBusy = false;
            });

            SelectBibleCommand = new Command(async () =>
            {
                IsBusy = true;

                var viewModel = this.container.Resolve<BibleSelectionViewModel>();
                await navigationService.Navigate(viewModel);

                await Task.Run(async () =>
                {
                    //get the latest bible track
                    if (BibleReadingSchedule == null || (!IsNewSchedule && !bibleReadingUpdated))
                    {
                        BibleReadingSchedule = await scheduleDbContext.BibleReadingSchedules
                                                .AsNoTracking()
                                                .FirstAsync(x => x.AlarmScheduleId == scheduleId);

                        refreshChapterName();
                    }

                });

                ReduxContainer.Store.Dispatch(new BibleSelectionAction()
                {
                    CurrentBibleReadingSchedule = BibleReadingSchedule,
                    TentativeBibleReadingSchedule = new BibleReadingSchedule()
                    {
                        PublicationCode = BibleReadingSchedule.PublicationCode,
                        LanguageCode = BibleReadingSchedule.LanguageCode
                    }
                });

                IsBusy = false;
            });


            OpenModalCommand = new Command(async () =>
            {
                IsBusy = true;
                await navigationService.ShowModal("NumberOfChaptersModal", this);
                IsBusy = false;
            });


            CloseModalCommand = new Command(async () =>
            {
                IsBusy = true;
                await navigationService.CloseModal();
                IsBusy = false;
            });

            SelectNumberOfChaptersCommand = new Command<NumberOfChaptersListViewItemModel>(async x =>
            {
                IsBusy = true;
                if (CurrentNumberOfChapters != null)
                {
                    CurrentNumberOfChapters.IsSelected = false;
                }

                CurrentNumberOfChapters = x;
                CurrentNumberOfChapters.IsSelected = true;

                await navigationService.CloseModal();

                IsBusy = false;
            });

            NotificationEnabledCommand = new Command(() =>
            {
                NotificationEnabled = !NotificationEnabled;
            });

            BatteryOptimizationExcludeCommand = new Command(async () =>
            {
                await markBatteryOptimizationModalAsShown();

                await navigationService.CloseModal();

                this.batteryOptimizationManager.ShowBatteryOptimizationExclusionSettingsPage();
            });

            BatteryOptimizationDismissCommand = new Command(async () =>
            {
                await markBatteryOptimizationModalAsShown();

                await navigationService.CloseModal();
            });

            PreviousBookCommand = new Command(async () =>
            {
                using var playlistService = container.Resolve<IPlaylistService>();
                var nextBook = await playlistService.GetPreviousBibleBook(BibleReadingSchedule.LanguageCode, BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber);

                BibleReadingSchedule.BookNumber = nextBook.Value.Number;
                BibleReadingSchedule.ChapterNumber = 1;
                BibleReadingSchedule.FinishedDuration = default;
                bibleReadingUpdated = true;
                refreshChapterName();
            });

            NextBookCommand = new Command(async () =>
            {
                using var playlistService = container.Resolve<IPlaylistService>();
                var nextBook = await playlistService.GetNextBibleBook(BibleReadingSchedule.LanguageCode, BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber);

                BibleReadingSchedule.BookNumber = nextBook.Value.Number;
                BibleReadingSchedule.ChapterNumber = 1;
                BibleReadingSchedule.FinishedDuration = default;
                bibleReadingUpdated = true;
                refreshChapterName();
            });

            PreviousChapterCommand = new Command(async () =>
            {
                using var playlistService = container.Resolve<IPlaylistService>();
                var prevChapter = await playlistService.GetPreviousBibleChapter(BibleReadingSchedule.LanguageCode, BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber, BibleReadingSchedule.ChapterNumber);

                BibleReadingSchedule.BookNumber = prevChapter.Key.Number;
                BibleReadingSchedule.ChapterNumber = prevChapter.Value.Number;
                BibleReadingSchedule.FinishedDuration = default;
                bibleReadingUpdated = true;
                refreshChapterName();
            });

            NextChapterCommand = new Command(async () =>
            {
                using var playlistService = container.Resolve<IPlaylistService>();
                var nextChapter = await playlistService.GetNextBibleChapter(BibleReadingSchedule.LanguageCode, BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber, BibleReadingSchedule.ChapterNumber);

                BibleReadingSchedule.BookNumber = nextChapter.Key.Number;
                BibleReadingSchedule.ChapterNumber = nextChapter.Value.Number;
                BibleReadingSchedule.FinishedDuration = default;
                bibleReadingUpdated = true;
                refreshChapterName();
            });
        }

        private bool canOptimizeBattery = false;
        public bool CanOptimizeBattery
        {
            get => canOptimizeBattery;
            set
            {
                this.Set(ref canOptimizeBattery, value);
            }
        }

        private async Task markBatteryOptimizationModalAsShown()
        {
            if (!await scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == "AndroidBatteryOptimizationExclusionPromptShown"))
            {
                await scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings()
                {
                    Key = "AndroidBatteryOptimizationExclusionPromptShown",
                    Value = "True"
                });

                await scheduleDbContext.SaveChangesAsync();
            }
        }

        private async Task showBatteryOptimizationExclusionPage()
        {
            if (batteryOptimizationManager.CanShowOptimizeActivity())
            {
                CanOptimizeBattery = true;
            }

            if (!await scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == "AndroidBatteryOptimizationExclusionPromptShown"))
            {
                await navigationService.ShowModal("BatteryOptimizationExclusionModal", this);
            }
        }


        private ScheduleListItem scheduleListItem;

        public ICommand CancelCommand { get; set; }

        public ICommand SaveCommand { get; set; }
        public ICommand DeleteCommand { get; set; }

        public ICommand SelectMusicCommand { get; set; }
        public ICommand SelectBibleCommand { get; set; }

        public ICommand NotificationEnabledCommand { get; set; }

        public ICommand ToggleDayCommand { get; set; }
        public ICommand ToggleAlwaysPlayFromStartCommand { get; set; }
        public AlarmSchedule Model { get; private set; }

        public ICommand OpenModalCommand { get; set; }
        public ICommand CloseModalCommand { get; set; }
        public ICommand SelectNumberOfChaptersCommand { get; set; }

        private ObservableCollection<NumberOfChaptersListViewItemModel> numberOfChaptersList;
        public ObservableCollection<NumberOfChaptersListViewItemModel> NumberOfChaptersList
        {
            get => numberOfChaptersList;
            set => this.Set(ref numberOfChaptersList, value);
        }

        private NumberOfChaptersListViewItemModel currentNumberOfChapters;
        public NumberOfChaptersListViewItemModel CurrentNumberOfChapters
        {
            get => currentNumberOfChapters;
            set => this.Set(ref currentNumberOfChapters, value);
        }

        private void populateNumberOfChaptersListView(AlarmSchedule model)
        {

            var chapterVMs = new ObservableCollection<NumberOfChaptersListViewItemModel>();

            for (int i = 1; i <= 21; i++)
            {
                var chaptersVM = new NumberOfChaptersListViewItemModel(i);

                if (model.NumberOfChaptersToRead == i)
                {
                    chaptersVM.IsSelected = true;
                    CurrentNumberOfChapters = chaptersVM;
                }

                chapterVMs.Add(chaptersVM);
            }

            NumberOfChaptersList = chapterVMs;
        }

        private AlarmSchedule getModel()
        {
            Model.Id = scheduleId;

            Model.Name = Name;
            Model.IsEnabled = IsEnabled;
            Model.DaysOfWeek = DaysOfWeek;
            Model.Hour = Time.Hours;
            Model.Minute = Time.Minutes;
            Model.MusicEnabled = MusicEnabled;
            Model.NotificationEnabled = notificationEnabled;
            Model.AlwaysPlayFromStart = AlwaysPlayFromStart;
            Model.NumberOfChaptersToRead = CurrentNumberOfChapters.Value;

            return Model;
        }

        private void setModel(AlarmSchedule model)
        {
            this.Model = model.DeepClone();

            scheduleId = model.Id;
            name = model.Name;
            isEnabled = model.IsEnabled;
            daysOfWeek = model.DaysOfWeek;
            time = new TimeSpan(model.Hour, model.Minute, model.Second);
            musicEnabled = model.MusicEnabled;
            notificationEnabled = model.NotificationEnabled;
            alwaysPlayFromStart = model.AlwaysPlayFromStart;

            populateNumberOfChaptersListView(model);
            refreshChapterName();
        }

        private int scheduleId;

        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set => this.Set(ref isBusy, value);
        }

        private string name;
        public string Name
        {
            get => name;
            set => this.Set(ref name, value);
        }

        private bool isEnabled;
        public bool IsEnabled
        {
            get => isEnabled;
            set => this.Set(ref isEnabled, value);
        }

        private DaysOfWeek daysOfWeek;
        public DaysOfWeek DaysOfWeek
        {
            get => daysOfWeek;
            set => this.Set(ref daysOfWeek, value);
        }

        private TimeSpan time;
        public TimeSpan Time
        {
            get => time;
            set => this.Set(ref time, value);

        }

        public string Hour
        {
            get => (Time.Hours % 12).ToString("D2");
        }

        public string Minute
        {
            get => Time.Minutes.ToString("D2");
        }

        public Meridien Meridien
        {
            get => Time.Hours < 12 ? Meridien.AM : Meridien.PM;
        }

        private bool musicEnabled;
        public bool MusicEnabled
        {
            get => musicEnabled;
            set => this.Set(ref musicEnabled, value);
        }

        private bool notificationEnabled;
        public bool NotificationEnabled
        {
            get => notificationEnabled;
            set
            {
                if (!value)
                {
                    _ = showBatteryOptimizationExclusionPage();
                }

                this.Set(ref notificationEnabled, value);
            }
        }

        private bool alwaysPlayFromStart;
        public bool AlwaysPlayFromStart
        {
            get => alwaysPlayFromStart;
            set => this.Set(ref alwaysPlayFromStart, value);
        }

        private bool musicUpdated;

        public AlarmMusic Music { get => Model.Music; set => Model.Music = value; }

        private bool bibleReadingUpdated;
        public BibleReadingSchedule BibleReadingSchedule { get => Model.BibleReadingSchedule; set => Model.BibleReadingSchedule = value; }

        private string bibleReadingTitleText;
        public string BibleReadingTitleText
        {
            get => bibleReadingTitleText;
            set => this.Set(ref bibleReadingTitleText, value);
        }

        public bool IsNewSchedule { get; private set; }
        public bool IsExistingSchedule => !IsNewSchedule;

        private void toggle(DaysOfWeek day)
        {
            if ((DaysOfWeek & day) == day)
            {
                DaysOfWeek &= ~day;
            }
            else
            {
                DaysOfWeek = DaysOfWeek | day;
            }

            this.RaiseProperty("DaysOfWeek");
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

        private async Task<bool> saveAsync()
        {
            if (!await validate())
            {
                return false;
            }

            if (IsNewSchedule)
            {
                IsEnabled = true;
            }

            var model = getModel();

            if (IsNewSchedule)
            {
                await Task.Run(async () =>
                {
                    await scheduleDbContext.AlarmSchedules.AddAsync(model);
                    await scheduleDbContext.SaveChangesAsync();
                    if (model.IsEnabled)
                    {
                        await alarmService.Create(model);
                    }
                });

                ReduxContainer.Store.Dispatch(new AddScheduleAction()
                {
                    ScheduleListItem = new ScheduleListItem(container, model)
                });
            }
            else
            {
                await Task.Run(async () =>
                {
                    var existing = await scheduleDbContext.AlarmSchedules
                         .Include(x => x.Music)
                         .Include(x => x.BibleReadingSchedule)
                         .FirstAsync(x => x.Id == model.Id);

                    existing.Hour = model.Hour;
                    existing.Minute = model.Minute;
                    existing.DaysOfWeek = model.DaysOfWeek;
                    existing.IsEnabled = model.IsEnabled;

                    if (model.Music != null && musicUpdated)
                    {
                        existing.Music.Repeat = model.Music.Repeat;
                        existing.Music.LanguageCode = model.Music.LanguageCode;
                        existing.Music.MusicType = model.Music.MusicType;
                        existing.Music.PublicationCode = model.Music.PublicationCode;
                        existing.Music.TrackNumber = model.Music.TrackNumber;
                    }

                    if (model.BibleReadingSchedule != null)
                    {
                        existing.BibleReadingSchedule.BookNumber = model.BibleReadingSchedule.BookNumber;
                        existing.BibleReadingSchedule.ChapterNumber = model.BibleReadingSchedule.ChapterNumber;
                        existing.BibleReadingSchedule.LanguageCode = model.BibleReadingSchedule.LanguageCode;
                        existing.BibleReadingSchedule.PublicationCode = model.BibleReadingSchedule.PublicationCode;

                        if (bibleReadingUpdated)
                        {
                            existing.BibleReadingSchedule.FinishedDuration = default(TimeSpan);
                        }
                    }

                    existing.MusicEnabled = model.MusicEnabled;
                    existing.NotificationEnabled = model.NotificationEnabled;
                    existing.AlwaysPlayFromStart = model.AlwaysPlayFromStart;
                    existing.NumberOfChaptersToRead = model.NumberOfChaptersToRead;
                    existing.Name = model.Name;
                    existing.Second = model.Second;
                    existing.SnoozeMinutes = model.SnoozeMinutes;

                    await scheduleDbContext.SaveChangesAsync();
                    alarmService.Update(model);
                });

                scheduleListItem.Schedule = model;
                scheduleListItem.RaisePropertiesChangedEvent();
                scheduleListItem.RefreshChapterName(true);

                ReduxContainer.Store.Dispatch(new UpdateScheduleAction() { ScheduleListItem = scheduleListItem });
            }

            setupMediaCache(model.Id);

            return true;
        }

        private async Task<bool> validate()
        {
            if (DaysOfWeek == 0)
            {
                await popUpService.ShowMessage("Please select day(s) of Week.");
                return false;
            }

            return true;
        }

        private async Task deleteAsync()
        {

            if (scheduleId >= 0)
            {
                await Task.Run(async () =>
                {
                    alarmService.Delete(scheduleId);
                    var model = await scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == scheduleId);
                    scheduleDbContext.AlarmSchedules.Remove(model);
                    await scheduleDbContext.SaveChangesAsync();

                });

                ReduxContainer.Store.Dispatch(new RemoveScheduleAction() { ScheduleListItem = scheduleListItem });
            }

        }

        private void refreshChapterName()
        {
            var syncContext = this.container.Resolve<TaskScheduler>();

            Task.Run(async () =>
            {
                try
                {
                    using var mediaDbContext = container.Resolve<MediaDbContext>();

                    var bookName = await mediaDbContext.BibleBook
                                    .Where(x => x.BibleTranslation.Code == BibleReadingSchedule.PublicationCode
                                            && x.BibleTranslation.Language.Code == BibleReadingSchedule.LanguageCode
                                            && x.Number == BibleReadingSchedule.BookNumber)
                                    .Select(x => x.Name)
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync();

                    return bookName;
                }
                catch (Exception e)
                {
                    logger.Error(e, "An error happened in refreshChapterName task under schedule view model.");
                }

                return null;
            })
            .ContinueWith((x) =>
            {
                if (x.IsCompleted)
                {
                    try
                    {
                        BibleReadingTitleText = $"{x.Result} {BibleReadingSchedule.ChapterNumber}";
                        RaiseProperty("SubTitle");
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "An error happened in refreshChapterName task continue with under schedule view model.");
                    }
                }

            }, syncContext);

        }

        public void Dispose()
        {
            subscriptions.ForEach(x => x.Dispose());
            subscriptions.Clear();

            this.scheduleDbContext.Dispose();
            this.popUpService.Dispose();
            this.alarmService.Dispose();
            this.notificationService.Dispose();
            this.mediaDbContext.Dispose();
            this.batteryOptimizationManager?.Dispose();
        }
    }
}
