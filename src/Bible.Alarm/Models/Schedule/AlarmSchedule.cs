using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Bible.Alarm.Models.Schedule;

[Serializable]
public class AlarmSchedule : IComparable
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }

    //24 hour based
    public int Hour { get; set; }

    public int MeridienHour => Meridien == Meridien.Am ? Hour == 0
            ? 12
            : Hour :
        Hour == 12 ? 12 : Hour % 12;

    public int Minute { get; set; }
    public Meridien Meridien => Hour < 12 ? Meridien.Am : Meridien.Pm;
    public int Second { get; set; }

    public DaysOfWeek DaysOfWeek { get; set; }

    public string TimeText => $"{MeridienHour:D2}:{Minute:D2}";

    public string CronExpression => GetCronExpression();

    public bool NotificationEnabled { get; set; }
    public bool MusicEnabled { get; set; }
    public virtual AlarmMusic Music { get; set; }

    public virtual BibleReadingSchedule BibleReadingSchedule { get; set; }

    public int SnoozeMinutes { get; set; } = 5;

    public int NumberOfChaptersToRead { get; set; } = 3;

    public bool AlwaysPlayFromStart { get; set; } = false;

    //state
    public PlayType CurrentPlayItem { get; set; }

    public long LatestAlarmNotificationId { get; set; }
    public virtual ICollection<AlarmNotification> AlarmNotifications { get; set; }

    public DateTimeOffset NextFireDate()
    {
        ValidateTime();

        var expression = new CronExpression(CronExpression);

        ValidateNextFire(expression);
        return expression.GetNextValidTimeAfter(DateTimeOffset.Now).Value;
    }

    public DateTimeOffset NextFireDate(DateTimeOffset after)
    {
        ValidateTime();

        var expression = new CronExpression(CronExpression);

        ValidateNextFire(expression);
        return expression.GetNextValidTimeAfter(after).Value;
    }

    private string GetCronExpression()
    {
        var days = string.Join(",", DaysOfWeek.ToList().OrderBy(x => x));
        var expression = new CronExpression($"{Second} {Minute} {Hour} ? * {days}");
        return expression.ToString();
    }

    private void ValidateTime()
    {
        if (Minute is < 0 or >= 60) throw new Exception("Invalid minute.");

        if (Hour is < 0 or >= 24) throw new Exception("Invalid hour.");

        if (DaysOfWeek == 0) throw new Exception("DaysOfWeek is empty.");
    }

    private static void ValidateNextFire(CronExpression expression)
    {
        var nextFire = expression.GetNextValidTimeAfter(DateTimeOffset.Now);

        if (nextFire == null) throw new Exception("Invalid alarm time.");
    }

    public int CompareTo(object obj)
    {
        if (obj is not AlarmSchedule other) return 1;
        return Id.CompareTo(other.Id);
    }

    public static async Task<AlarmSchedule> GetSampleSchedule(bool isNew, MediaDbContext mediaDbContext)
    {
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
                PublicationCode = "nwt"
            }
        };

        var bible = await mediaDbContext
            .BibleTranslations
            .Include(x => x.Books)
            .Where(x => x.Code == sample.BibleReadingSchedule.PublicationCode
                        && x.Language.Code
                        == sample.BibleReadingSchedule.LanguageCode)
            .FirstOrDefaultAsync();

        if (bible == null)
            throw new InvalidOperationException("Bible translation not found for sample schedule");
        
        var rnd = new Random();
        var book = bible.Books[rnd.Next() % bible.Books.Count];
        if (sample.BibleReadingSchedule == null)
            throw new InvalidOperationException("BibleReadingSchedule is null in sample schedule");
        
        sample.BibleReadingSchedule.BookNumber = book.Number;

        if (sample.Music == null)
            throw new InvalidOperationException("Music is null in sample schedule");
        
        var music = await mediaDbContext
            .MelodyMusic
            .Include(x => x.Tracks)
            .Where(x => x.Code
                        == sample.Music.PublicationCode)
            .FirstOrDefaultAsync();

        if (music == null)
            throw new InvalidOperationException("Melody music not found for sample schedule");

        var track = music.Tracks[rnd.Next() % music.Tracks.Count];
        sample.Music.TrackNumber = track.Number;

        return sample;
    }
}