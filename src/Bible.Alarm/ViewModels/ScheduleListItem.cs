using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Input;
using Bible.Alarm.UI.Messenger;
using Bible.Alarm.Contracts.Media;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Infrastructure.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.ViewModels;

public class ScheduleListItem : ObservableObject, IComparable, IDisposable, IRecipient<TrackChangedMessage>
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AlarmSchedule Schedule;
    private readonly IDisposable _subscription;

    public ScheduleListItem(AlarmSchedule schedule, ILogger logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        Schedule = schedule;
        _isEnabled = schedule.IsEnabled;

        PlayCommand = new Command(() =>
        {
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                using var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();

                try
                {
                    if (Schedule.Id > 0)
                    {
                        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();
                        await playbackService.PrepareAndPlay(Schedule.Id, true);

                        await toastService.ShowMessage("Your schedule will start playing in a few seconds.", 5);

                        using var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        await notificationService.ShowNotification(Schedule.Id);
                    }
                }
                catch (Exception e)
                {
                    _logger.Information(e, "An error happened when playing alarm.");
                    await toastService.ShowMessage("Error. Network may not be available." +
                                                   "Please try again.", 5);
                }
            });
        });

        RefreshChapterName(true);

        WeakReferenceMessenger.Default.Register<TrackChangedMessage>(this);

        PreviousCommand = new Command(async () =>
        {
            if (!await CanMove()) return;
            using var scope = _scopeFactory.CreateScope();
            using var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            await playlistService.MoveToPreviousBibleChapter(schedule.Id);

            RefreshChapterName(true);
        });

        NextCommand = new Command(async () =>
        {
            if (!await CanMove()) return;

            using var scope = _scopeFactory.CreateScope();
            using var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            await playlistService.MoveToNextBibleChapter(schedule.Id);

            RefreshChapterName(true);
        });
    }

    private async Task<bool> CanMove()
    {
        using var scope = _scopeFactory.CreateScope();
        var playbackService = scope.ServiceProvider.GetRequiredService<IPlaybackService>();

        if (!playbackService.IsPlaying) return true;

        using var toastService = scope.ServiceProvider.GetRequiredService<IToastService>();

        await toastService.ShowMessage("Cannot update the chapter when schedule is in progress.");

        return false;
    }

    public long ScheduleId => Schedule.Id;

    public string Name => Schedule.Name;

    public string SubTitle { get; private set; }

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
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
        var properties = GetType()
            .GetProperties()
            .Where(x => x.Name != "IsEnabled")
            .Select(x => x.Name);
        foreach (var property in properties)
        {
            OnPropertyChanged(property);
        }
    }

    public void RefreshChapterName(bool force = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var syncContext = scope.ServiceProvider.GetRequiredService<TaskScheduler>();

        _ = Task.Run(async () =>
            {
                try
                {
                    using var serviceScope = _scopeFactory.CreateScope();
                    var playbackService = serviceScope.ServiceProvider.GetRequiredService<IPlaybackService>();

                    if (Schedule == null || (!force && !playbackService.IsPrepared)) return null;

                    await using var scheduleDbContext = serviceScope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

                    var schedule = await scheduleDbContext.AlarmSchedules
                        .Include(x => x.BibleReadingSchedule)
                        .AsNoTracking()
                        .Where(x => x.Id == Schedule.Id)
                        .FirstOrDefaultAsync();

                    if (schedule != null)
                    {
                        await using var mediaDbContext = serviceScope.ServiceProvider.GetRequiredService<MediaDbContext>();

                        var bookName = await mediaDbContext.BibleBook
                            .Where(x => x.BibleTranslation.Code == schedule.BibleReadingSchedule.PublicationCode
                                        && x.BibleTranslation.Language.Code ==
                                        schedule.BibleReadingSchedule.LanguageCode
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
                    _logger.Error(e, "An error happened in RefreshChapterName task under list item.");
                }

                return null;
            })
            .ContinueWith((x) =>
            {
                try
                {
                    if (!x.IsCompleted || x.Result == null) return;

                    SubTitle = $"{x.Result.Item1} {x.Result.Item2}";
                    OnPropertyChanged(nameof(SubTitle));
                }
                catch (Exception e)
                {
                    _logger.Error(e, "An error happened in RefreshChapterName continue with task under list item.");
                }
            }, syncContext);
    }

    public int CompareTo(object obj)
    {
        return ScheduleId.CompareTo(((ScheduleListItem)obj).ScheduleId);
    }

    public void Receive(TrackChangedMessage message)
    {
        if (message.Value == Schedule.Id)
        {
            RefreshChapterName();
        }
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<TrackChangedMessage>(this);
    }
}