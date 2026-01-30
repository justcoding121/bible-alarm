#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
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

        // Using centralized sorting helper from Bible.Alarm.Shared.Helpers.PublicationSortHelper

        // Optimize: Load publications with sections in one call to get both publication info and sections
        // For new schedules, prefer "nwt" with English "E", then fallback to first available language with a sectioned publication
        const string DefaultLanguageCode = "E";
        const string PreferredPublicationCode = "nwt";
        string? bibleLanguageCode = null;
        string? biblePublicationCode = null;
        BiblePublication? selectedBible = null;
        
        // Try English "E" with "nwt" first (for new schedules)
        if (bibleLanguages.ContainsKey(DefaultLanguageCode))
        {
            var englishPublications = await biblePublicationService.GetByLanguageCodeAsync(DefaultLanguageCode);
            if (englishPublications != null && englishPublications.ContainsKey(PreferredPublicationCode))
            {
                // Try "nwt" first
                if (PublicationTypeHelper.HasSectionStructure(PreferredPublicationCode))
                {
                    var biblePub = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                        DefaultLanguageCode, PreferredPublicationCode);
                    if (biblePub != null && biblePub.Sections != null && biblePub.Sections.Count > 0)
                    {
                        bibleLanguageCode = DefaultLanguageCode;
                        biblePublicationCode = PreferredPublicationCode;
                        selectedBible = biblePub;
                    }
                }
            }
            
            // If "nwt" not available, try other English publications
            if (selectedBible == null && englishPublications != null)
            {
                // Sort publications by priority: nwt first, then bi12, then others
                var sortedPublications = PublicationSortHelper.SortByPriority(englishPublications, pub => pub.Name);
                
                foreach (var pub in sortedPublications)
                {
                    if (PublicationTypeHelper.HasSectionStructure(pub.Key))
                    {
                        // Load with sections in one call - this includes Category
                        var biblePub = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                            DefaultLanguageCode, pub.Key);
                        if (biblePub != null && biblePub.Sections != null && biblePub.Sections.Count > 0)
                        {
                            bibleLanguageCode = DefaultLanguageCode;
                            biblePublicationCode = pub.Key;
                            selectedBible = biblePub;
                            break;
                        }
                    }
                }
            }
        }
        
        // Fallback: find any language with a sectioned publication
        if (selectedBible == null)
        {
            foreach (var lang in bibleLanguages)
            {
                var publications = await biblePublicationService.GetByLanguageCodeAsync(lang.Key);
                if (publications == null || publications.Count == 0)
                {
                    continue;
                }
                
                // Sort publications by priority: nwt first, then bi12, then others
                var sortedPublications = PublicationSortHelper.SortByPriority(publications, pub => pub.Name);
                
                foreach (var pub in sortedPublications)
                {
                    if (PublicationTypeHelper.HasSectionStructure(pub.Key))
                    {
                        // Load with sections in one call - this includes Category
                        var biblePub = await biblePublicationService.GetByLanguageAndCodeWithSectionsAsync(
                            lang.Key, pub.Key);
                        if (biblePub != null && biblePub.Sections != null && biblePub.Sections.Count > 0)
                        {
                            bibleLanguageCode = lang.Key;
                            biblePublicationCode = pub.Key;
                            selectedBible = biblePub;
                            break;
                        }
                    }
                }
                
                if (selectedBible != null)
                {
                    break;
                }
            }
        }

        if (selectedBible == null || bibleLanguageCode == null || biblePublicationCode == null)
        {
            throw new InvalidOperationException("No sectioned Bible publication found in database for sample schedule");
        }

        // Get first available melody music from database that has tracks
        var melodyQueryStart = DateTime.UtcNow;
        var melodyReleases = await melodyMusicService.GetAllAsync();
        Log.Information("[PERF] GetSampleSchedule: Melody releases query took {ElapsedMs}ms", (DateTime.UtcNow - melodyQueryStart).TotalMilliseconds);

        if (melodyReleases == null || melodyReleases.Count == 0)
        {
            throw new InvalidOperationException("No melody music found in database");
        }

        // Find a melody music publication that has tracks
        string? melodyPublicationCode = null;
        foreach (var melody in melodyReleases)
        {
            var musicWithTracks = await melodyMusicService.GetByCodeWithTracksAsync(melody.Key);
            if (musicWithTracks?.Tracks != null && musicWithTracks.Tracks.Count > 0)
            {
                melodyPublicationCode = melody.Key;
                break;
            }
        }

        if (melodyPublicationCode == null)
        {
            throw new InvalidOperationException("No melody music with tracks found in database");
        }

        // Create sample schedule disabled by default - user must explicitly enable it
        var sample = new AlarmSchedule
        {
            IsEnabled = false,
            MusicEnabled = false,
            NotificationEnabled = false, // Disabled by default - user must explicitly enable tap-to-play
            DaysOfWeek = DaysOfWeek.All,
            Name = $"{(isNew ? "New schedule" : "Schedule Name")}",
            Hour = 6,
            Minute = 0,
            Music = new AlarmMusic
            {
                MusicType = MusicType.Music,
                PublicationCode = melodyPublicationCode,
                LanguageCode = null
            },
            BiblePublicationSchedule = new BiblePublicationSchedule
            {
                TrackNumber = 1,
                LanguageCode = bibleLanguageCode,
                PublicationCode = biblePublicationCode,
                SectionCode = "1" // Will be updated below with a random section
            }
        };

        // Use the already-loaded bible publication (no additional DB call needed)
        // selectedBible is guaranteed to be non-null here due to the check above
        if (selectedBible.Sections == null || selectedBible.Sections.Count == 0)
        {
            throw new InvalidOperationException($"No sections found for Bible publication {biblePublicationCode}");
        }

        // Use Random.Shared for thread-safe random number generation
        // Safe for non-cryptographic use (selecting sample sections/tracks)
        // Only select sections that have tracks
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

        // Use SectionCode directly to match media index db
        sample.BiblePublicationSchedule.SectionCode = section.SectionCode;
        
        // Get the first track of the selected section (book)
        // We already verified the section has tracks above
        var firstTrack = section.Tracks!.OrderBy(t => t.Number).First();
        sample.BiblePublicationSchedule.TrackNumber = firstTrack.Number;

        if (sample.Music == null)
        {
            throw new InvalidOperationException("Music is null in sample schedule");
        }

        var musicQueryStartTime = DateTime.UtcNow;
        var music = await melodyMusicService.GetByCodeWithTracksAsync(sample.Music.PublicationCode);
        var musicQueryElapsed = (DateTime.UtcNow - musicQueryStartTime).TotalMilliseconds;
        Log.Information("[PERF] GetSampleSchedule: Music tracks query took {ElapsedMs}ms", musicQueryElapsed);

        if (music == null || music.Publication == null)
        {
            throw new InvalidOperationException("Melody music not found for sample schedule");
        }

        // Melody music is now sectioned (e.g., iam has sections like "iam-1", "iam-2")
        // Select a random section, then a random track from that section
        if (music.Publication.Sections == null || music.Publication.Sections.Count == 0)
        {
            // Fallback: if no sections, try direct tracks (for backward compatibility)
            if (music.Tracks == null || music.Tracks.Count == 0)
            {
                throw new InvalidOperationException($"No sections or tracks found for melody music publication {sample.Music.PublicationCode}");
            }
            
            var track = music.Tracks[Random.Shared.Next(music.Tracks.Count)];
            sample.Music.TrackNumber = track.Number;
        }
        else
        {
            // Select a random section
            var randomSection = music.Publication.Sections[Random.Shared.Next(music.Publication.Sections.Count)];
            
            if (randomSection.Tracks == null || randomSection.Tracks.Count == 0)
            {
                throw new InvalidOperationException($"No tracks found in section {randomSection.SectionCode} for melody music publication {sample.Music.PublicationCode}");
            }
            
            // Set the section code for the selected section
            sample.Music.SectionCode = randomSection.SectionCode;
            
            // Select a random track from the selected section
            var randomTrack = randomSection.Tracks[Random.Shared.Next(randomSection.Tracks.Count)];
            sample.Music.TrackNumber = randomTrack.Number;
        }

        var totalElapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        Log.Information("[PERF] GetSampleSchedule: Completed in {ElapsedMs}ms", totalElapsed);

        return sample;
    }
}
