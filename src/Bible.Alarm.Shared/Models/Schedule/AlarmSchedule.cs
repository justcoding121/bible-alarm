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

namespace Bible.Alarm.Shared.Models.Schedule;

[Serializable]
[Table("AlarmSchedules")]
[Index(nameof(IsEnabled))]
[Index(nameof(Hour), nameof(Minute))]
public sealed class AlarmSchedule : IComparable
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

    public AlarmMusic? Music { get; set; }

    public BiblePublicationSchedule? BiblePublicationSchedule { get; set; }

    [Required]
    [Range(1, 60)]
    public int SnoozeMinutes { get; set; } = 5;

    [Required]
    [Range(1, 10)]
    public int NumberOfTracksToRead { get; set; } = 3;

    [Required]
    public bool AlwaysPlayFromStart { get; set; } = false;

    //state
    [Required]
    public PlayType CurrentPlayItem { get; set; }

    [Required]
    public long LatestAlarmNotificationId { get; set; }

    public ICollection<AlarmNotification> AlarmNotifications { get; set; } = new List<AlarmNotification>();

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
        var nextFire = expression.GetNextValidTimeAfter(DateTimeOffset.Now) ?? throw new Exception("Invalid alarm time.");
    }

    public int CompareTo(object? obj)
    {
        if (obj is not AlarmSchedule other)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }

    public static async Task<AlarmSchedule> GetSampleSchedule(bool isNew, IBiblePublicationService biblePublicationService, IMelodyMusicService melodyMusicService)
    {
        var startTime = DateTime.UtcNow;
        Log.Information("[PERF] GetSampleSchedule: Started at {StartTime}", startTime);

        // Get first available Bible language and publication from database
        var languagesQueryStart = DateTime.UtcNow;
        var bibleLanguages = await biblePublicationService.GetDistinctLanguagesAsync();
        Log.Information("[PERF] GetSampleSchedule: Bible languages query took {ElapsedMs}ms", (DateTime.UtcNow - languagesQueryStart).TotalMilliseconds);

        if (bibleLanguages == null || bibleLanguages.Count == 0)
        {
            throw new InvalidOperationException("No Bible publications found in database");
        }

        // Use first available language
        var bibleLanguageCode = bibleLanguages.FirstOrDefault().Key;
        var biblePublications = await biblePublicationService.GetByLanguageCodeAsync(bibleLanguageCode);

        if (biblePublications == null || biblePublications.Count == 0)
        {
            throw new InvalidOperationException($"No Bible publications found for language {bibleLanguageCode}");
        }

        // Use first available publication
        var firstBiblePublication = biblePublications.FirstOrDefault();
        var biblePublicationCode = firstBiblePublication.Key;

        // Get first available melody music from database
        var melodyQueryStart = DateTime.UtcNow;
        var melodyReleases = await melodyMusicService.GetAllAsync();
        Log.Information("[PERF] GetSampleSchedule: Melody releases query took {ElapsedMs}ms", (DateTime.UtcNow - melodyQueryStart).TotalMilliseconds);

        if (melodyReleases == null || melodyReleases.Count == 0)
        {
            throw new InvalidOperationException("No melody music found in database");
        }

        var firstMelody = melodyReleases.FirstOrDefault();
        var melodyPublicationCode = firstMelody.Key;

        // Create sample schedule disabled by default - user must explicitly enable it
        var sample = new AlarmSchedule
        {
            IsEnabled = false,
            MusicEnabled = false,
            NotificationEnabled = false, // Disabled by default - user must explicitly enable tap-to-play
            DaysOfWeek = DaysOfWeek.All,
            Name = $"{(isNew ? "New schedule" : "Initial schedule")}",
            Hour = 6,
            Minute = 0,
            Music = new AlarmMusic
            {
                MusicType = MusicType.Melodies,
                PublicationCode = melodyPublicationCode,
                LanguageCode = null
            },
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                TrackNumber = 1,
                LanguageCode = bibleLanguageCode,
                PublicationCode = biblePublicationCode,
                SectionNumber = 1 // Will be updated below with a random section
            }
        };

        var bibleQueryStartTime = DateTime.UtcNow;
        var bible = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
            sample.BiblePublicationSchedule.LanguageCode,
            sample.BiblePublicationSchedule.PublicationCode);
        var bibleQueryElapsed = (DateTime.UtcNow - bibleQueryStartTime).TotalMilliseconds;
        Log.Information("[PERF] GetSampleSchedule: Bible sections query took {ElapsedMs}ms", bibleQueryElapsed);

        if (bible == null)
        {
            throw new InvalidOperationException("Bible publication not found for sample schedule");
        }

        // Use Random.Shared for thread-safe random number generation
        // Safe for non-cryptographic use (selecting sample sections/tracks)
        var section = bible.Sections[Random.Shared.Next(bible.Sections.Count)];
        if (sample.BiblePublicationSchedule == null)
        {
            throw new InvalidOperationException("BiblePublicationSchedule is null in sample schedule");
        }

        sample.BiblePublicationSchedule.SectionNumber = section.Number;

        if (sample.Music == null)
        {
            throw new InvalidOperationException("Music is null in sample schedule");
        }

        var musicQueryStartTime = DateTime.UtcNow;
        var music = await melodyMusicService.GetByCodeWithTracksAsync(sample.Music.PublicationCode);
        var musicQueryElapsed = (DateTime.UtcNow - musicQueryStartTime).TotalMilliseconds;
        Log.Information("[PERF] GetSampleSchedule: Music tracks query took {ElapsedMs}ms", musicQueryElapsed);

        if (music == null)
        {
            throw new InvalidOperationException("Melody music not found for sample schedule");
        }

        var track = music.Tracks[Random.Shared.Next(music.Tracks.Count)];
        sample.Music.TrackNumber = track.Number;

        var totalElapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        Log.Information("[PERF] GetSampleSchedule: Completed in {ElapsedMs}ms", totalElapsed);

        return sample;
    }
}
