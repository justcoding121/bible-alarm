#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

namespace Bible.Alarm.Shared.Models.Schedule;

[Serializable]
[Table("AlarmSchedules")]
[Index(nameof(IsEnabled))]
[Index(nameof(Hour), nameof(Minute))]
public sealed class AlarmSchedule : IComparable, IEquatable<AlarmSchedule>
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

    public int MeridianHour
    {
        get
        {
            if (Meridian == Meridian.Am)
            {
                return Hour == 0 ? 12 : Hour;
            }

            return Hour == 12 ? 12 : Hour % 12;
        }
    }

    [Required]
    [Range(0, 59)]
    public int Minute { get; set; }

    public Meridian Meridian => Hour < 12 ? Meridian.Am : Meridian.Pm;

    [Required]
    [Range(0, 59)]
    public int Second { get; set; }

    [Required]
    public WeekDays DaysOfWeek { get; set; }

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
    // 0 indicates "play indefinitely" (no upper bound within a playback session).
    // When > 0, indicates the number of chapters/episodes to play for a session.
    [Range(0, 21)]
    public int NumberOfTracksToPlay { get; set; } = 0;

    [Required]
    public bool AlwaysPlayFromStart { get; set; } = false;

    //state
    [Required]
    public PlayType CurrentPlayItem { get; set; }

    [Required]
    public long LatestAlarmNotificationId { get; set; }

    /// <summary>
    /// Category code (e.g. "Bible", "Dramas") for schedule content. Null for Music category schedules.
    /// </summary>
    [MaxLength(100)]
    public string? CategoryCode { get; set; }

    /// <summary>
    /// UTC timestamp when this schedule was last played. Null if never played.
    /// Used to sort home page list (recently played first).
    /// </summary>
    public DateTime? LastPlayedAtUtc { get; set; }

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
            throw new InvalidOperationException("Invalid minute.");
        }

        if (Hour is < 0 or >= 24)
        {
            throw new InvalidOperationException("Invalid hour.");
        }

        if (DaysOfWeek == 0)
        {
            throw new InvalidOperationException("DaysOfWeek is empty.");
        }
    }

    private static void ValidateNextFire(CronExpression expression)
    {
        _ = expression.GetNextValidTimeAfter(DateTimeOffset.Now) ?? throw new InvalidOperationException("Invalid alarm time.");
    }

    public int CompareTo(object? obj) => CompareTo(obj as AlarmSchedule);

    public int CompareTo(AlarmSchedule? other)
    {
        if (other is null)
        {
            return 1;
        }

        return Id.CompareTo(other.Id);
    }

    public bool Equals(AlarmSchedule? other) =>
        other is not null &&
        (Id != 0 ? Id == other.Id : ReferenceEquals(this, other));

    public override bool Equals(object? obj) => Equals(obj as AlarmSchedule);

    public override int GetHashCode() =>
        Id != 0 ? Id.GetHashCode() : RuntimeHelpers.GetHashCode(this);

    public static bool operator ==(AlarmSchedule? left, AlarmSchedule? right) =>
        ReferenceEquals(left, right) ||
        left is not null && right is not null && left.Equals(right);

    public static bool operator !=(AlarmSchedule? left, AlarmSchedule? right) => !(left == right);

    public static bool operator <(AlarmSchedule? left, AlarmSchedule? right) =>
        left is not null && right is not null && left.CompareTo(right) < 0;

    public static bool operator >(AlarmSchedule? left, AlarmSchedule? right) =>
        left is not null && right is not null && left.CompareTo(right) > 0;

    public static bool operator <=(AlarmSchedule? left, AlarmSchedule? right) =>
        left is not null && right is not null && left.CompareTo(right) <= 0;

    public static bool operator >=(AlarmSchedule? left, AlarmSchedule? right) =>
        left is not null && right is not null && left.CompareTo(right) >= 0;

    public static async Task<AlarmSchedule> GetSampleSchedule(bool isNew, IBiblePublicationService biblePublicationService, IMelodyMusicService melodyMusicService)
    {
        var startTime = DateTime.UtcNow;
        Log.Debug("[PERF] GetSampleSchedule: Started at {StartTime}", startTime);

        var languagesQueryStart = DateTime.UtcNow;
        var bibleLanguages = await biblePublicationService.GetDistinctLanguagesAsync();
        Log.Debug("[PERF] GetSampleSchedule: Bible languages query took {ElapsedMs}ms", (DateTime.UtcNow - languagesQueryStart).TotalMilliseconds);

        if (bibleLanguages == null || bibleLanguages.Count == 0)
        {
            throw new InvalidOperationException(AppConstants.SampleScheduleDiagnostics.NoBiblePublicationsInDatabaseMessage);
        }

        var biblePick = await SelectSectionedBibleForSampleAsync(biblePublicationService, bibleLanguages);

        var melodyQueryStart = DateTime.UtcNow;
        var melodyReleases = await melodyMusicService.GetAllAsync();
        Log.Debug("[PERF] GetSampleSchedule: Melody releases query took {ElapsedMs}ms", (DateTime.UtcNow - melodyQueryStart).TotalMilliseconds);

        if (melodyReleases == null || melodyReleases.Count == 0)
        {
            throw new InvalidOperationException("No melody music found in database");
        }

        var melodyPublicationCode = await ResolveMelodyPublicationCodeForSampleAsync(melodyMusicService, melodyReleases);

        var sample = CreateSampleScheduleShell(isNew, melodyPublicationCode, biblePick.LanguageCode, biblePick.PublicationCode);
        ApplyRandomBibleSectionAndTrack(sample, biblePick.Publication, biblePick.PublicationCode);
        await ApplyRandomMelodyTrackAsync(sample, melodyMusicService);

        Log.Information("[PERF] GetSampleSchedule: Completed in {ElapsedMs}ms", (DateTime.UtcNow - startTime).TotalMilliseconds);

        return sample;
    }

    private readonly record struct SampleBibleSelection(string LanguageCode, string PublicationCode, BiblePublication Publication);

    private static async Task<SampleBibleSelection?> TrySelectPreferredEnglishNwtAsync(
        IBiblePublicationService biblePublicationService,
        string defaultLanguageCode,
        string preferredPublicationCode)
    {
        if (!PublicationTypeHelper.HasSectionStructure(preferredPublicationCode))
        {
            return null;
        }

        var biblePub = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
            defaultLanguageCode, preferredPublicationCode);
        if (biblePub == null || biblePub.Sections == null || biblePub.Sections.Count == 0)
        {
            return null;
        }

        return new SampleBibleSelection(defaultLanguageCode, preferredPublicationCode, biblePub);
    }

    private static async Task<SampleBibleSelection?> TrySelectEnglishSectionedPublicationAsync(
        IBiblePublicationService biblePublicationService,
        string defaultLanguageCode)
    {
        var englishPublications = await biblePublicationService.GetByLanguageCodeAsync(defaultLanguageCode);
        if (englishPublications == null)
        {
            englishPublications = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase);
        }

        var sortedPublications = PublicationSortHelper.SortByPriority(englishPublications, pub => pub.Name);

        foreach (var publicationCode in sortedPublications.Where(p =>
                     PublicationTypeHelper.HasSectionStructure(p.Key)).Select(p => p.Key))
        {
            var biblePub = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                defaultLanguageCode, publicationCode);
            if (biblePub != null && biblePub.Sections != null && biblePub.Sections.Count > 0)
            {
                return new SampleBibleSelection(defaultLanguageCode, publicationCode, biblePub);
            }
        }

        return null;
    }

    private static async Task<SampleBibleSelection?> TrySelectSectionedBibleFromAnyLanguageAsync(
        IBiblePublicationService biblePublicationService,
        Dictionary<string, Language> bibleLanguages)
    {
        foreach (var languageCode in bibleLanguages.Keys)
        {
            var publications = await biblePublicationService.GetByLanguageCodeAsync(languageCode);
            if (publications == null || publications.Count == 0)
            {
                continue;
            }

            var sortedPublications = PublicationSortHelper.SortByPriority(publications, pub => pub.Name);

            foreach (var publicationCode in sortedPublications.Where(p =>
                         PublicationTypeHelper.HasSectionStructure(p.Key)).Select(p => p.Key))
            {
                var biblePub = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                    languageCode, publicationCode);
                if (biblePub != null && biblePub.Sections != null && biblePub.Sections.Count > 0)
                {
                    return new SampleBibleSelection(languageCode, publicationCode, biblePub);
                }
            }
        }

        return null;
    }

    private static async Task<SampleBibleSelection> SelectSectionedBibleForSampleAsync(
        IBiblePublicationService biblePublicationService,
        Dictionary<string, Language> bibleLanguages)
    {
        const string DefaultLanguageCode = AppConstants.Media.DefaultLanguageCode;
        const string PreferredPublicationCode = AppConstants.Media.BiblePublicationCodeNwt;

        SampleBibleSelection? selected = null;

        if (bibleLanguages.ContainsKey(DefaultLanguageCode))
        {
            selected = await TrySelectPreferredEnglishNwtAsync(
                biblePublicationService, DefaultLanguageCode, PreferredPublicationCode);

            selected ??= await TrySelectEnglishSectionedPublicationAsync(
                biblePublicationService, DefaultLanguageCode);
        }

        selected ??= await TrySelectSectionedBibleFromAnyLanguageAsync(biblePublicationService, bibleLanguages);

        if (selected == null)
        {
            throw new InvalidOperationException(AppConstants.SampleScheduleDiagnostics.NoSectionedPublicationForSampleScheduleMessage);
        }

        return selected.Value;
    }

    private static async Task<string> ResolveMelodyPublicationCodeForSampleAsync(
        IMelodyMusicService melodyMusicService,
        Dictionary<string, MelodyMusic> melodyReleases)
    {
        const string PreferredMelodyPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam;

        string? melodyPublicationCode = melodyReleases.ContainsKey(PreferredMelodyPublicationCode)
            ? PreferredMelodyPublicationCode
            : melodyReleases.Keys.FirstOrDefault();

        if (!string.IsNullOrEmpty(melodyPublicationCode))
        {
            var musicWithTracks = await melodyMusicService.GetByCodeWithTracksAsync(melodyPublicationCode);
            if (musicWithTracks?.Tracks == null || musicWithTracks.Tracks.Count == 0)
            {
                melodyPublicationCode = null;
            }
        }

        if (melodyPublicationCode == null)
        {
            foreach (var melodyKey in melodyReleases.Keys)
            {
                var musicWithTracks = await melodyMusicService.GetByCodeWithTracksAsync(melodyKey);
                if (musicWithTracks?.Tracks != null && musicWithTracks.Tracks.Count > 0)
                {
                    melodyPublicationCode = melodyKey;
                    break;
                }
            }
        }

        if (melodyPublicationCode == null)
        {
            throw new InvalidOperationException("No melody music with tracks found in database");
        }

        return melodyPublicationCode;
    }

    private static AlarmSchedule CreateSampleScheduleShell(
        bool isNew,
        string melodyPublicationCode,
        string bibleLanguageCode,
        string biblePublicationCode)
    {
        return new AlarmSchedule
        {
            IsEnabled = false,
            MusicEnabled = false,
            NotificationEnabled = false,
            DaysOfWeek = WeekDays.All,
            Name = $"{(isNew ? AppConstants.Media.ScheduleUiSampleNameNew : AppConstants.Media.ScheduleUiSampleNamePlaceholder)}",
            Hour = 6,
            Minute = 0,
            Music = new AlarmMusic
            {
                PublicationCode = melodyPublicationCode,
                LanguageCode = null
            },
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                TrackCode = AppConstants.Media.BiblePublicationGenesisBookNumber,
                LanguageCode = bibleLanguageCode,
                PublicationCode = biblePublicationCode,
                SectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber
            }
        };
    }

    private static void ApplyRandomBibleSectionAndTrack(AlarmSchedule sample, BiblePublication selectedBible, string biblePublicationCode)
    {
        if (selectedBible.Sections == null || selectedBible.Sections.Count == 0)
        {
            throw new InvalidOperationException($"No sections found for Bible publication {biblePublicationCode}");
        }

        var sectionsWithTracks = selectedBible.Sections
            .Where(s => s.Tracks != null && s.Tracks.Count > 0)
            .ToList();

        if (sectionsWithTracks.Count == 0)
        {
            throw new InvalidOperationException($"No sections with tracks found for Bible publication {biblePublicationCode}");
        }

        var section = sectionsWithTracks[Random.Shared.Next(sectionsWithTracks.Count)];
        if (sample.BiblePublicationSchedule == null)
        {
            throw new InvalidOperationException("BiblePublicationSchedule is null in sample schedule");
        }

        sample.BiblePublicationSchedule.SectionCode = section.SectionCode;

        var firstTrack = section.Tracks!.OrderBy(t => t, Comparer<BiblePublicationTrack>.Create((a, b) => a.CompareTo(b))).ToList()[0];
        sample.BiblePublicationSchedule.TrackCode = firstTrack.TrackCode;
    }

    private static async Task ApplyRandomMelodyTrackAsync(AlarmSchedule sample, IMelodyMusicService melodyMusicService)
    {
        if (sample.Music == null)
        {
            throw new InvalidOperationException("Music is null in sample schedule");
        }

        var musicQueryStartTime = DateTime.UtcNow;
        var music = await melodyMusicService.GetByCodeWithTracksAsync(sample.Music.PublicationCode);
        Log.Debug("[PERF] GetSampleSchedule: Music tracks query took {ElapsedMs}ms", (DateTime.UtcNow - musicQueryStartTime).TotalMilliseconds);

        if (music == null || music.Publication == null)
        {
            throw new InvalidOperationException("Melody music not found for sample schedule");
        }

        if (music.Publication.Sections == null || music.Publication.Sections.Count == 0)
        {
            if (music.Tracks == null || music.Tracks.Count == 0)
            {
                throw new InvalidOperationException($"No sections or tracks found for melody music publication {sample.Music.PublicationCode}");
            }

            var track = music.Tracks[Random.Shared.Next(music.Tracks.Count)];
            sample.Music.TrackCode = GetTrackCodeFromTrack(track);
            return;
        }

        var randomSection = music.Publication.Sections[Random.Shared.Next(music.Publication.Sections.Count)];

        if (randomSection.Tracks == null || randomSection.Tracks.Count == 0)
        {
            throw new InvalidOperationException($"No tracks found in section {randomSection.SectionCode} for melody music publication {sample.Music.PublicationCode}");
        }

        sample.Music.SectionCode = randomSection.SectionCode;

        var randomTrack = randomSection.Tracks[Random.Shared.Next(randomSection.Tracks.Count)];
        sample.Music.TrackCode = GetTrackCodeFromTrack(randomTrack);
    }

    private static string GetTrackCodeFromTrack(BiblePublicationTrack track) => TrackCodeHelper.GetFromTrack(track);
}
