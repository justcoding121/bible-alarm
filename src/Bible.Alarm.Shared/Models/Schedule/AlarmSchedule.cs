#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

namespace Bible.Alarm.Models.Schedule;

[Serializable]
[Table("AlarmSchedules")]
[Index(nameof(IsEnabled))]
[Index(nameof(Hour), nameof(Minute))]
public class AlarmSchedule : IComparable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public bool IsEnabled { get; set; }

    //24 hour based
    [Required]
    [Range(0, 23)]
    public int Hour { get; set; }

    public int MeridianHour => Meridian == Meridian.Am ? Hour == 0
            ? 12
            : Hour :
        Hour == 12 ? 12 : Hour % 12;

    [Required]
    [Range(0, 59)]
    public int Minute { get; set; }

    public Meridian Meridian => Hour < 12 ? Meridian.Am : Meridian.Pm;

    [Required]
    [Range(0, 59)]
    public int Second { get; set; }

    [Required]
    public DaysOfWeek DaysOfWeek { get; set; }

    public string TimeText => $"{MeridianHour:D2}:{Minute:D2}";

    public string CronExpression => GetCronExpression();

    [Required]
    public bool NotificationEnabled { get; set; }

    [Required]
    public bool MusicEnabled { get; set; }

    public virtual AlarmMusic? Music { get; set; }

    public virtual BibleReadingSchedule? BibleReadingSchedule { get; set; }

    [Required]
    [Range(1, 60)]
    public int SnoozeMinutes { get; set; } = 5;

    [Required]
    [Range(1, 10)]
    public int NumberOfChaptersToRead { get; set; } = 3;

    [Required]
    public bool AlwaysPlayFromStart { get; set; } = false;

    //state
    [Required]
    public PlayType CurrentPlayItem { get; set; }

    [Required]
    public long LatestAlarmNotificationId { get; set; }

    public virtual ICollection<AlarmNotification> AlarmNotifications { get; set; } = new List<AlarmNotification>();

    public DateTimeOffset NextFireDate()
    {
        ValidateTime();

        var expression = new CronExpression(CronExpression);

        ValidateNextFire(expression);
        return expression.GetNextValidTimeAfter(DateTimeOffset.Now)!.Value;
    }

    public DateTimeOffset NextFireDate(DateTimeOffset after)
    {
        ValidateTime();

        var expression = new CronExpression(CronExpression);

        ValidateNextFire(expression);
        return expression.GetNextValidTimeAfter(after)!.Value;
    }

    private string GetCronExpression()
    {
        var days = string.Join(",", DaysOfWeek.ToList().OrderBy(x => x));
        var expression = new CronExpression($"{Second} {Minute} {Hour} ? * {days}");
        return expression.ToString();
    }

    private void ValidateTime()
    {
        if (Minute is < 0 or >= 60)
        {
            throw new Exception("Invalid minute.");
        }

        if (Hour is < 0 or >= 24)
        {
            throw new Exception("Invalid hour.");
        }

        if (DaysOfWeek == 0)
        {
            throw new Exception("DaysOfWeek is empty.");
        }
    }

    private static void ValidateNextFire(CronExpression expression)
    {
        var nextFire = expression.GetNextValidTimeAfter(DateTimeOffset.Now);

        if (nextFire == null)
        {
            throw new Exception("Invalid alarm time.");
        }
    }

    public int CompareTo(object? obj)
    {
        if (obj is not AlarmSchedule other)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }

    public static async Task<AlarmSchedule> GetSampleSchedule(bool isNew, IBibleTranslationService bibleTranslationService, IMelodyMusicService melodyMusicService)
    {
        var startTime = DateTime.UtcNow;
        Log.Information("[PERF] GetSampleSchedule: Started at {StartTime}", startTime);

        // Create sample schedule disabled by default - user must explicitly enable it
        var sample = new AlarmSchedule
        {
            IsEnabled = false,
            MusicEnabled = false,
            NotificationEnabled = true,
            DaysOfWeek = DaysOfWeek.All,
            Name = $"{(isNew ? "New schedule" : "Initial schedule")}",
            Hour = 6,
            Minute = 0,
            Music = new AlarmMusic
            {
                MusicType = MusicType.Melodies,
                PublicationCode = "iam",
                LanguageCode = null
            },
            BibleReadingSchedule = new BibleReadingSchedule
            {
                ChapterNumber = 1,
                LanguageCode = "E",
                PublicationCode = "nwt" // NWT 2013 (not 1984 - use "bi12" for 1984)
            }
        };

        var bibleQueryStartTime = DateTime.UtcNow;
        var bible = await bibleTranslationService.GetByLanguageAndCodeWithBooksAsync(
            sample.BibleReadingSchedule.LanguageCode,
            sample.BibleReadingSchedule.PublicationCode);
        var bibleQueryElapsed = (DateTime.UtcNow - bibleQueryStartTime).TotalMilliseconds;
        Log.Information("[PERF] GetSampleSchedule: Bible query took {ElapsedMs}ms", bibleQueryElapsed);

        if (bible == null)
        {
            throw new InvalidOperationException("Bible translation not found for sample schedule");
        }

        var rnd = new Random();
        var book = bible.Books[rnd.Next() % bible.Books.Count];
        if (sample.BibleReadingSchedule == null)
        {
            throw new InvalidOperationException("BibleReadingSchedule is null in sample schedule");
        }

        sample.BibleReadingSchedule.BookNumber = book.Number;

        if (sample.Music == null)
        {
            throw new InvalidOperationException("Music is null in sample schedule");
        }

        var musicQueryStartTime = DateTime.UtcNow;
        var music = await melodyMusicService.GetByCodeWithTracksAsync(sample.Music.PublicationCode);
        var musicQueryElapsed = (DateTime.UtcNow - musicQueryStartTime).TotalMilliseconds;
        Log.Information("[PERF] GetSampleSchedule: Music query took {ElapsedMs}ms", musicQueryElapsed);

        if (music == null)
        {
            throw new InvalidOperationException("Melody music not found for sample schedule");
        }

        var track = music.Tracks[rnd.Next() % music.Tracks.Count];
        sample.Music.TrackNumber = track.Number;

        var totalElapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        Log.Information("[PERF] GetSampleSchedule: Completed in {ElapsedMs}ms", totalElapsed);

        return sample;
    }
}
