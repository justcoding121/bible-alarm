
using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Contracts.Battery;
using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Devices;
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
        private static readonly Lazy<Logger> LazyLogger = new Lazy<Logger>(() => LogManager.GetCurrentClassLogger());
        private static Logger Logger => LazyLogger.Value;

        private readonly IContainer _container;

        ScheduleDbContext _scheduleDbContext;
        MediaDbContext _mediaDbContext;

        IAlarmService _alarmService;
        IToastService _popUpService;
        INavigationService _navigationService;
        IPlaybackService _playbackService;
        INotificationService _notificationService;

        private List<IDisposable> _subscriptions = new List<IDisposable>();

        public Command BatteryOptimizationExcludeCommand { get; private set; }
        public Command BatteryOptimizationDismissCommand { get; private set; }

        public ICommand PreviousBookCommand { get; set; }
        public ICommand NextBookCommand { get; set; }

        public ICommand PreviousChapterCommand { get; set; }
        public ICommand NextChapterCommand { get; set; }

        private IBatteryOptimizationManager _batteryOptimizationManager;

        public ScheduleViewModel(IContainer container)
        {
            _container = container;

            if (CurrentDevice.RuntimePlatform == DevicePlatform.Android.ToString())
            {
                _batteryOptimizationManager = container.Resolve<IBatteryOptimizationManager>();
            }

            _scheduleDbContext = _container.Resolve<ScheduleDbContext>();
            _mediaDbContext = _container.Resolve<MediaDbContext>();
            _popUpService = _container.Resolve<IToastService>();
            _alarmService = _container.Resolve<IAlarmService>();
            _navigationService = _container.Resolve<INavigationService>();
            _playbackService = _container.Resolve<IPlaybackService>();
            _notificationService = _container.Resolve<INotificationService>();

            _subscriptions.Add(_scheduleDbContext);

            //set schedules from initial state.
            //this should fire only once (look at the where condition).
            var subscription = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
               .Select(state => state.CurrentScheduleListItem)
               .DistinctUntilChanged()
               .Take(1)
               .Subscribe(async x =>
               {
                   _scheduleListItem = x;

                   var model = x?.Schedule;

                   IsNewSchedule = model == null ? true : false;

                   AlarmSchedule modelToSet;

                   if (model == null)
                   {
                       modelToSet = await AlarmSchedule.GetSampleSchedule(true, _mediaDbContext);
                   }
                   else
                   {
                       modelToSet = model;
                   }

                   SetModel(modelToSet);

                   IsBusy = false;
               });

            _subscriptions.Add(subscription);

            var subscription2 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
           .Select(state => state.CurrentMusic)
           .Where(x => x != null && x != Music)
           .Subscribe(x =>
           {
               Music = x;
               _musicUpdated = true;
           });

            _subscriptions.Add(subscription2);

            var subscription3 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
            .Select(state => state.CurrentBibleReadingSchedule)
            .Where(x => x != null && x != BibleReadingSchedule)
            .DistinctUntilChanged()
            .Subscribe(x =>
            {
                BibleReadingSchedule = x;
                _bibleReadingUpdated = true;
                RefreshChapterName();
            });

            _subscriptions.Add(subscription3);

            CancelCommand = new Command(async () =>
            {
                IsBusy = true;

                await _navigationService.GoBack();

                IsBusy = false;
            });

            SaveCommand = new Command(async () =>
            {
                IsBusy = true;

                if (IsEnabled &&
                      (CurrentDevice.RuntimePlatform == DevicePlatform.iOS.ToString()
                        || CurrentDevice.RuntimePlatform == DevicePlatform.WinUI.ToString())
                     && !await _notificationService.CanSchedule())
                {
                    IsEnabled = false;
                }

                if (!IsNewSchedule)
                {
                    if (_playbackService.IsPrepared
                        && _scheduleId == _playbackService.CurrentlyPlayingScheduleId)
                    {
                        await _playbackService.Dismiss();
                    }
                }

                var saved = await SaveAsync();

                if (saved)
                {
                    await _navigationService.GoBack();
                }

                if (saved && IsEnabled)
                {
                    await _popUpService.ShowScheduledNotification(Model);
                }

                IsBusy = false;
            });

            DeleteCommand = new Command(async () =>
            {
                IsBusy = true;

                if (_playbackService.IsPrepared
                       && _scheduleId == _playbackService.CurrentlyPlayingScheduleId)
                {
                    await _playbackService.Dismiss();
                }

                await DeleteAsync();

                await _navigationService.GoBack();

                IsBusy = false;
            });

            ToggleDayCommand = new Command<DaysOfWeek>(x =>
            {
                Toggle(x);
            });

            ToggleAlwaysPlayFromStartCommand = new Command(x => AlwaysPlayFromStart = !AlwaysPlayFromStart);

            SelectMusicCommand = new Command(async () =>
            {
                IsBusy = true;

                var viewModel = _container.Resolve<MusicSelectionViewModel>();
                await _navigationService.Navigate(viewModel);

                await Task.Run(async () =>
                {
                    //get the latest music track
                    if (Music == null || (!IsNewSchedule && !_musicUpdated))
                    {
                        Music = await _scheduleDbContext.AlarmMusic
                        .AsNoTracking()
                        .FirstAsync(x => x.AlarmScheduleId == _scheduleId);
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

                var viewModel = _container.Resolve<BibleSelectionViewModel>();
                await _navigationService.Navigate(viewModel);

                await Task.Run(async () =>
                {
                    //get the latest bible track
                    if (BibleReadingSchedule == null || (!IsNewSchedule && !_bibleReadingUpdated))
                    {
                        BibleReadingSchedule = await _scheduleDbContext.BibleReadingSchedules
                                                .AsNoTracking()
                                                .FirstAsync(x => x.AlarmScheduleId == _scheduleId);

                        RefreshChapterName();
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
                await _navigationService.ShowModal("NumberOfChaptersModal", this);
                IsBusy = false;
            });


            CloseModalCommand = new Command(async () =>
            {
                IsBusy = true;
                await _navigationService.CloseModal();
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

                await _navigationService.CloseModal();

                IsBusy = false;
            });

            NotificationEnabledCommand = new Command(() =>
            {
                NotificationEnabled = !NotificationEnabled;
            });

            BatteryOptimizationExcludeCommand = new Command(async () =>
            {
                await MarkBatteryOptimizationModalAsShown();

                await _navigationService.CloseModal();

                _batteryOptimizationManager.ShowBatteryOptimizationExclusionSettingsPage();
            });

            BatteryOptimizationDismissCommand = new Command(async () =>
            {
                await MarkBatteryOptimizationModalAsShown();

                await _navigationService.CloseModal();
            });

            PreviousBookCommand = new Command(async () =>
            {
                using var playlistService = container.Resolve<IPlaylistService>();
                var nextBook = await playlistService.GetPreviousBibleBook(BibleReadingSchedule.LanguageCode, BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber);

                BibleReadingSchedule.BookNumber = nextBook.Value.Number;
                BibleReadingSchedule.ChapterNumber = 1;
                BibleReadingSchedule.FinishedDuration = default;
                _bibleReadingUpdated = true;
                RefreshChapterName();
            });

            NextBookCommand = new Command(async () =>
            {
                using var playlistService = container.Resolve<IPlaylistService>();
                var nextBook = await playlistService.GetNextBibleBook(BibleReadingSchedule.LanguageCode, BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber);

                BibleReadingSchedule.BookNumber = nextBook.Value.Number;
                BibleReadingSchedule.ChapterNumber = 1;
                BibleReadingSchedule.FinishedDuration = default;
                _bibleReadingUpdated = true;
                RefreshChapterName();
            });

            PreviousChapterCommand = new Command(async () =>
            {
                using var playlistService = container.Resolve<IPlaylistService>();
                var prevChapter = await playlistService.GetPreviousBibleChapter(BibleReadingSchedule.LanguageCode, BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber, BibleReadingSchedule.ChapterNumber);

                BibleReadingSchedule.BookNumber = prevChapter.Key.Number;
                BibleReadingSchedule.ChapterNumber = prevChapter.Value.Number;
                BibleReadingSchedule.FinishedDuration = default;
                _bibleReadingUpdated = true;
                RefreshChapterName();
            });

            NextChapterCommand = new Command(async () =>
            {
                using var playlistService = container.Resolve<IPlaylistService>();
                var nextChapter = await playlistService.GetNextBibleChapter(BibleReadingSchedule.LanguageCode, BibleReadingSchedule.PublicationCode, BibleReadingSchedule.BookNumber, BibleReadingSchedule.ChapterNumber);

                BibleReadingSchedule.BookNumber = nextChapter.Key.Number;
                BibleReadingSchedule.ChapterNumber = nextChapter.Value.Number;
                BibleReadingSchedule.FinishedDuration = default;
                _bibleReadingUpdated = true;
                RefreshChapterName();
            });
        }

        private bool _canOptimizeBattery = false;
        public bool CanOptimizeBattery
        {
            get => _canOptimizeBattery;
            set
            {
                this.Set(ref _canOptimizeBattery, value);
            }
        }

        private async Task MarkBatteryOptimizationModalAsShown()
        {
            if (!await _scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == "AndroidBatteryOptimizationExclusionPromptShown"))
            {
                await _scheduleDbContext.GeneralSettings.AddAsync(new GeneralSettings()
                {
                    Key = "AndroidBatteryOptimizationExclusionPromptShown",
                    Value = "True"
                });

                await _scheduleDbContext.SaveChangesAsync();
            }
        }

        private async Task ShowBatteryOptimizationExclusionPage()
        {
            if (_batteryOptimizationManager.CanShowOptimizeActivity())
            {
                CanOptimizeBattery = true;
            }

            if (!await _scheduleDbContext.GeneralSettings.AnyAsync(x => x.Key == "AndroidBatteryOptimizationExclusionPromptShown"))
            {
                await _navigationService.ShowModal("BatteryOptimizationExclusionModal", this);
            }
        }


        private ScheduleListItem _scheduleListItem;

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

        private ObservableCollection<NumberOfChaptersListViewItemModel> _numberOfChaptersList;
        public ObservableCollection<NumberOfChaptersListViewItemModel> NumberOfChaptersList
        {
            get => _numberOfChaptersList;
            set => this.Set(ref _numberOfChaptersList, value);
        }

        private NumberOfChaptersListViewItemModel _currentNumberOfChapters;
        public NumberOfChaptersListViewItemModel CurrentNumberOfChapters
        {
            get => _currentNumberOfChapters;
            set => this.Set(ref _currentNumberOfChapters, value);
        }

        private void PopulateNumberOfChaptersListView(AlarmSchedule model)
        {

            var chapterVMs = new ObservableCollection<NumberOfChaptersListViewItemModel>();

            for (int i = 1; i <= 21; i++)
            {
                var chaptersVm = new NumberOfChaptersListViewItemModel(i);

                if (model.NumberOfChaptersToRead == i)
                {
                    chaptersVm.IsSelected = true;
                    CurrentNumberOfChapters = chaptersVm;
                }

                chapterVMs.Add(chaptersVm);
            }

            NumberOfChaptersList = chapterVMs;
        }

        private AlarmSchedule GetModel()
        {
            Model.Id = _scheduleId;

            Model.Name = Name;
            Model.IsEnabled = IsEnabled;
            Model.DaysOfWeek = DaysOfWeek;
            Model.Hour = Time.Hours;
            Model.Minute = Time.Minutes;
            Model.MusicEnabled = MusicEnabled;
            Model.NotificationEnabled = _notificationEnabled;
            Model.AlwaysPlayFromStart = AlwaysPlayFromStart;
            Model.NumberOfChaptersToRead = CurrentNumberOfChapters.Value;

            return Model;
        }

        private void SetModel(AlarmSchedule model)
        {
            Model = model.DeepClone();

            _scheduleId = model.Id;
            _name = model.Name;
            _isEnabled = model.IsEnabled;
            _daysOfWeek = model.DaysOfWeek;
            _time = new TimeSpan(model.Hour, model.Minute, model.Second);
            _musicEnabled = model.MusicEnabled;
            _notificationEnabled = model.NotificationEnabled;
            _alwaysPlayFromStart = model.AlwaysPlayFromStart;

            PopulateNumberOfChaptersListView(model);
            RefreshChapterName();
        }

        private int _scheduleId;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => this.Set(ref _isBusy, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => this.Set(ref _name, value);
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.Set(ref _isEnabled, value);
        }

        private DaysOfWeek _daysOfWeek;
        public DaysOfWeek DaysOfWeek
        {
            get => _daysOfWeek;
            set => this.Set(ref _daysOfWeek, value);
        }

        private TimeSpan _time;
        public TimeSpan Time
        {
            get => _time;
            set => this.Set(ref _time, value);

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
            get => Time.Hours < 12 ? Meridien.Am : Meridien.Pm;
        }

        private bool _musicEnabled;
        public bool MusicEnabled
        {
            get => _musicEnabled;
            set => this.Set(ref _musicEnabled, value);
        }

        private bool _notificationEnabled;
        public bool NotificationEnabled
        {
            get => _notificationEnabled;
            set
            {
                if (!value)
                {
                    _ = ShowBatteryOptimizationExclusionPage();
                }

                this.Set(ref _notificationEnabled, value);
            }
        }

        private bool _alwaysPlayFromStart;
        public bool AlwaysPlayFromStart
        {
            get => _alwaysPlayFromStart;
            set => this.Set(ref _alwaysPlayFromStart, value);
        }

        private bool _musicUpdated;

        public AlarmMusic Music { get => Model.Music; set => Model.Music = value; }

        private bool _bibleReadingUpdated;
        public BibleReadingSchedule BibleReadingSchedule { get => Model.BibleReadingSchedule; set => Model.BibleReadingSchedule = value; }

        private string _bibleReadingTitleText;
        public string BibleReadingTitleText
        {
            get => _bibleReadingTitleText;
            set => this.Set(ref _bibleReadingTitleText, value);
        }

        public bool IsNewSchedule { get; private set; }
        public bool IsExistingSchedule => !IsNewSchedule;

        private void Toggle(DaysOfWeek day)
        {
            if ((DaysOfWeek & day) == day)
            {
                DaysOfWeek &= ~day;
            }
            else
            {
                DaysOfWeek = DaysOfWeek | day;
            }

            RaiseProperty("DaysOfWeek");
        }

        private void SetupMediaCache(long scheduleId)
        {
            Task.Run(async () =>
             {
                 try
                 {
                     using var mediaCacheService = _container.Resolve<IMediaCacheService>();
                     await mediaCacheService.SetupAlarmCache(scheduleId);
                 }
                 catch (Exception e)
                 {
                     Logger.Error(e, "An error happened in SetupAlarmCache task.");
                 }
             });
        }

        private async Task<bool> SaveAsync()
        {
            if (!await Validate())
            {
                return false;
            }

            if (IsNewSchedule)
            {
                IsEnabled = true;
            }

            var model = GetModel();

            if (IsNewSchedule)
            {
                await Task.Run(async () =>
                {
                    await _scheduleDbContext.AlarmSchedules.AddAsync(model);
                    await _scheduleDbContext.SaveChangesAsync();
                    if (model.IsEnabled)
                    {
                        await _alarmService.Create(model);
                    }
                });

                ReduxContainer.Store.Dispatch(new AddScheduleAction()
                {
                    ScheduleListItem = new ScheduleListItem(_container, model)
                });
            }
            else
            {
                await Task.Run(async () =>
                {
                    var existing = await _scheduleDbContext.AlarmSchedules
                         .Include(x => x.Music)
                         .Include(x => x.BibleReadingSchedule)
                         .FirstAsync(x => x.Id == model.Id);

                    existing.Hour = model.Hour;
                    existing.Minute = model.Minute;
                    existing.DaysOfWeek = model.DaysOfWeek;
                    existing.IsEnabled = model.IsEnabled;

                    if (model.Music != null && _musicUpdated)
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

                        if (_bibleReadingUpdated)
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

                    await _scheduleDbContext.SaveChangesAsync();
                    _alarmService.Update(model);
                });

                _scheduleListItem.Schedule = model;
                _scheduleListItem.RaisePropertiesChangedEvent();
                _scheduleListItem.RefreshChapterName(true);

                ReduxContainer.Store.Dispatch(new UpdateScheduleAction() { ScheduleListItem = _scheduleListItem });
            }

            SetupMediaCache(model.Id);

            return true;
        }

        private async Task<bool> Validate()
        {
            if (DaysOfWeek == 0)
            {
                await _popUpService.ShowMessage("Please select day(s) of Week.");
                return false;
            }

            return true;
        }

        private async Task DeleteAsync()
        {

            if (_scheduleId >= 0)
            {
                await Task.Run(async () =>
                {
                    _alarmService.Delete(_scheduleId);
                    var model = await _scheduleDbContext.AlarmSchedules.FirstOrDefaultAsync(x => x.Id == _scheduleId);
                    _scheduleDbContext.AlarmSchedules.Remove(model);
                    await _scheduleDbContext.SaveChangesAsync();

                });

                ReduxContainer.Store.Dispatch(new RemoveScheduleAction() { ScheduleListItem = _scheduleListItem });
            }

        }

        private void RefreshChapterName()
        {
            var syncContext = _container.Resolve<TaskScheduler>();

            Task.Run(async () =>
            {
                try
                {
                    using var mediaDbContext = _container.Resolve<MediaDbContext>();

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
                    Logger.Error(e, "An error happened in refreshChapterName task under schedule view model.");
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
                        Logger.Error(e, "An error happened in refreshChapterName task continue with under schedule view model.");
                    }
                }

            }, syncContext);

        }

        public void Dispose()
        {
            _subscriptions.ForEach(x => x.Dispose());
            _subscriptions.Clear();

            _scheduleDbContext.Dispose();
            _popUpService.Dispose();
            _alarmService.Dispose();
            _notificationService.Dispose();
            _mediaDbContext.Dispose();
            _batteryOptimizationManager?.Dispose();
        }
    }
}
