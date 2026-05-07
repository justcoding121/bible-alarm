#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Shared.Tests;

public sealed class AlarmScheduleTests
{
    private static AlarmSchedule Create(
        WeekDays days = WeekDays.Monday,
        int hour = 9,
        int minute = 30,
        int second = 0,
        int id = 10)
        => new()
        {
            Id = id,
            Name = "Test",
            IsEnabled = true,
            Hour = hour,
            Minute = minute,
            Second = second,
            DaysOfWeek = days,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 0,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
            LatestAlarmNotificationId = 0,
        };

    [Fact]
    public void Meridian_IsAm_WhenHourBeforeNoonElsePm()
    {
        Assert.Equal(Meridian.Am, Create(hour: 0).Meridian);
        Assert.Equal(Meridian.Am, Create(hour: 11).Meridian);
        Assert.Equal(Meridian.Pm, Create(hour: 12).Meridian);
        Assert.Equal(Meridian.Pm, Create(hour: 23).Meridian);
    }

    [Theory]
    [InlineData(0, 12)] // midnight displays as 12 in 12-hour form
    [InlineData(1, 1)]
    [InlineData(11, 11)]
    [InlineData(12, 12)] // noon
    [InlineData(13, 1)]
    [InlineData(23, 11)]
    public void MeridianHour_Maps24hToClockFace(int hour24, int expectedMeridianHour)
        => Assert.Equal(expectedMeridianHour, Create(hour: hour24).MeridianHour);

    [Fact]
    public void TimeText_UsesMeridianHourPadded_WithMinutePadded()
    {
        Assert.Equal("12:00", Create(hour: 0, minute: 0).TimeText);
        Assert.Equal("01:07", Create(hour: 13, minute: 7).TimeText);
    }

    [Fact]
    public void CronExpression_Encodes_Second_MinuteHour_AndSortedWeekdayList()
    {
        var sut = Create(WeekDays.Monday | WeekDays.Wednesday, hour: 14, minute: 5, second: 30);

        Assert.Equal("30 5 14 ? * 2,4", sut.CronExpression);
    }

    [Fact]
    public void NextFireDate_AfterAnchor_IsStrictlyLater()
    {
        var sut = Create(WeekDays.All, hour: 8, minute: 0);
        var after = new DateTimeOffset(2030, 6, 1, 12, 0, 0, TimeSpan.Zero);

        var next = sut.NextFireDate(after);

        Assert.True(next > after);
    }

    [Fact]
    public void NextFireDate_ThrowsInvalidOperation_WhenWeekdayMaskEmpty()
    {
        var sut = Create((WeekDays)0);

        Assert.Throws<InvalidOperationException>(() => sut.NextFireDate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NextFireDate_ThrowsInvalidOperation_WhenHourOutOfRange()
    {
        var sut = Create(WeekDays.Monday);
        sut.Hour = 24;

        Assert.Throws<InvalidOperationException>(() => sut.NextFireDate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CompareAndEquality_UseId_WhenNonZeroElseReference()
    {
        var a = Create(id: 1);
        var b = Create(id: 2);

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(a < b);
        Assert.False(a == b);

        var aRef = Create(id: 0);
        var bSameRef = aRef;

        Assert.True(aRef == bSameRef);

        var aDistinct = Create(id: 0);
        var bDistinct = Create(id: 0);
        Assert.False(aDistinct.Equals(bDistinct));
    }

    [Fact]
    public void CompareTo_Object_Null_ReturnsGreater()
        => Assert.Equal(1, Create().CompareTo(null));

    [Fact]
    public void CompareTo_Object_NotAlarmSchedule_TreatsAsNull()
        => Assert.Equal(1, Create().CompareTo(new object()));

    [Fact]
    public void CompareTo_AlarmScheduleNull_ReturnsGreater()
        => Assert.Equal(1, Create().CompareTo((AlarmSchedule?)null));

    [Fact]
    public void GetHashCode_Matches_OnSameNonZero_Id()
    {
        var a = Create(id: 55, hour: 1);
        var b = Create(id: 55, hour: 22);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Comparison_operators_require_non_null_operands()
    {
        AlarmSchedule? n = null;
        var s = Create();
        Assert.False(s < n);
        Assert.False(n < s);
        Assert.False(s > n);
        Assert.False(n > s);
        Assert.False(s <= n);
        Assert.False(n <= s);
        Assert.False(s >= n);
        Assert.False(n >= s);
    }

    [Fact]
    public void NextFireDate_ThrowsInvalidOperation_WhenMinuteOutOfRange()
    {
        var sut = Create(WeekDays.Monday);
        sut.Minute = 60;

        Assert.Throws<InvalidOperationException>(() => sut.NextFireDate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void NextFireDate_WithoutAnchor_ReturnsTimeAfterUtcNow()
    {
        var sut = Create(WeekDays.All, hour: 12, minute: 0, second: 0);
        var before = DateTimeOffset.UtcNow;

        var next = sut.NextFireDate();

        Assert.True(next > before);
    }

    [Fact]
    public async Task GetSampleSchedule_PreferredEnglishNwtAndMelodyWithSections_BuildsLinksAsync()
    {
        var bible = BuildNwtWithSingleGenesisTrack();
        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(SampleLanguagesEnglish()),
            GetSectioned: (_, code, _) => Task.FromResult(
                code.Equals(AppConstants.Media.BiblePublicationCodeNwt, StringComparison.OrdinalIgnoreCase) ? bible : null),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)));

        var melodyMusic = BuildMelodyPublicationWithSections(
            publicationCode: AppConstants.Media.MelodyMusicPublicationCodeIam,
            trackCode: "77");
        using var melodySvc = SampleScheduleMelodyServiceFake.ForReleases(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
        {
            [AppConstants.Media.MelodyMusicPublicationCodeIam] = melodyMusic,
        });

        var schedule = await AlarmSchedule.GetSampleSchedule(
            isNew: true,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc);

        Assert.Contains(AppConstants.Media.ScheduleUiSampleNameNew, schedule.Name, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(schedule.BiblePublicationSchedule);
        Assert.Equal(AppConstants.Media.DefaultLanguageCode, schedule.BiblePublicationSchedule.LanguageCode);
        Assert.Equal(AppConstants.Media.BiblePublicationCodeNwt, schedule.BiblePublicationSchedule.PublicationCode);
        Assert.Equal(AppConstants.Media.BiblePublicationGenesisBookNumber, schedule.BiblePublicationSchedule.SectionCode);
        Assert.False(string.IsNullOrEmpty(schedule.BiblePublicationSchedule.TrackCode));

        Assert.NotNull(schedule.Music);
        Assert.Equal(AppConstants.Media.MelodyMusicPublicationCodeIam, schedule.Music.PublicationCode);
        Assert.False(string.IsNullOrEmpty(schedule.Music.TrackCode));
    }

    [Fact]
    public async Task GetSampleSchedule_Throws_When_NoDistinctBibleLanguages()
    {
        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(new Dictionary<string, Language>()),
            GetSectioned: (_, _, _) => Task.FromResult<BiblePublication?>(null),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)));

        using var melodySvc = SampleScheduleMelodyServiceFake.ForReleases(new Dictionary<string, MelodyMusic>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => AlarmSchedule.GetSampleSchedule(
            isNew: false,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc));

        Assert.Equal(AppConstants.SampleScheduleDiagnostics.NoBiblePublicationsInDatabaseMessage, ex.Message);
    }

    [Fact]
    public async Task GetSampleSchedule_Throws_When_NoMelody_ReleasesAsync()
    {
        var bible = BuildNwtWithSingleGenesisTrack();
        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(SampleLanguagesEnglish()),
            GetSectioned: (_, _, _) => Task.FromResult<BiblePublication?>(bible),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)));

        using var melodySvc = SampleScheduleMelodyServiceFake.ForReleases(new Dictionary<string, MelodyMusic>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => AlarmSchedule.GetSampleSchedule(
            isNew: false,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc));

        Assert.Equal("No melody music found in database", ex.Message);
    }

    [Fact]
    public async Task GetSampleSchedule_Selects_AlternateMelody_WhenPreferredHasNoTracks()
    {
        var bible = BuildNwtWithSingleGenesisTrack();
        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(SampleLanguagesEnglish()),
            GetSectioned: (_, _, _) => Task.FromResult<BiblePublication?>(bible),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)));

        var iamEmptySections = BuildMelodyPublicationWithSections(AppConstants.Media.MelodyMusicPublicationCodeIam, "x");
        iamEmptySections.Publication.Sections.Clear();
        iamEmptySections.Publication.Tracks.Clear();

        var alt = BuildMelodyPublicationWithSections("altMelodyPub", trackCode: "42");
        using var melodySvc = SampleScheduleMelodyServiceFake.WithPreferredNoTracksThenAlt(
            altHasTracksByCode: (publicationCode, _) =>
                Task.FromResult<MelodyMusic?>(
                    publicationCode.Equals(AppConstants.Media.MelodyMusicPublicationCodeIam, StringComparison.OrdinalIgnoreCase)
                        ? iamEmptySections
                        : alt));

        var schedule = await AlarmSchedule.GetSampleSchedule(
            isNew: false,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc);

        Assert.NotNull(schedule.Music);
        Assert.Equal("altMelodyPub", schedule.Music.PublicationCode);
        Assert.Equal("42", schedule.Music.TrackCode);
    }

    [Fact]
    public async Task GetSampleSchedule_UsesPublicationFlatTracks_WhenMelodyHasSectionsEmpty_ListButTracksFilled()
    {
        var bible = BuildNwtWithSingleGenesisTrack();
        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(SampleLanguagesEnglish()),
            GetSectioned: (_, _, _) => Task.FromResult<BiblePublication?>(bible),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)));

        var pub = new BiblePublication
        {
            PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            Name = "Iam flat",
            IsVideo = false,
            IsMusic = true,
            Sections = [],
            Tracks =
            [
                new BiblePublicationTrack
                {
                    TrackCode = "flat-9",
                    Title = "Nine",
                    BiblePublicationId = 0,
                    Publication = null!,
                },
            ],
        };

        foreach (var t in pub.Tracks)
        {
            t.Publication = pub;
        }

        using var melodySvc = SampleScheduleMelodyServiceFake.ForReleases(
            new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.MelodyMusicPublicationCodeIam] = new MelodyMusic { Publication = pub },
            });

        var schedule = await AlarmSchedule.GetSampleSchedule(
            isNew: false,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc);

        Assert.NotNull(schedule.Music);
        Assert.Null(schedule.Music.SectionCode);
        Assert.Equal("flat-9", schedule.Music.TrackCode);
    }

    [Fact]
    public async Task GetSampleSchedule_Selects_FromNonEnglishLanguage_When_DefaultLanguageMissingAsync()
    {
        var bible = BuildNwtWithSingleGenesisTrack();
        var french = new Language
        {
            LanguageCode = "F",
            Direction = AppConstants.Media.TextDirectionLeftToRight,
        };

        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["F"] = french,
            }),
            GetSectioned: (_, code, _) => Task.FromResult(
                code.Equals(AppConstants.Media.BiblePublicationCodeNwt, StringComparison.OrdinalIgnoreCase) ? bible : null),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)
                {
                    [AppConstants.Media.BiblePublicationCodeNwt] = bible,
                }));

        using var melodySvc = SampleScheduleMelodyServiceFake.ForReleases(
            SampleMelodyIamWithSections());

        var schedule = await AlarmSchedule.GetSampleSchedule(
            isNew: false,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc);

        Assert.NotNull(schedule.BiblePublicationSchedule);
        Assert.Equal("F", schedule.BiblePublicationSchedule.LanguageCode);
        Assert.Equal(AppConstants.Media.BiblePublicationCodeNwt, schedule.BiblePublicationSchedule.PublicationCode);
    }

    [Fact]
    public async Task GetSampleSchedule_Throws_When_NoSectionedPublicationInAny_LanguageAsync()
    {
        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["F"] = new Language { LanguageCode = "F", Direction = AppConstants.Media.TextDirectionLeftToRight },
            }),
            GetSectioned: (_, _, _) => Task.FromResult<BiblePublication?>(null),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)));

        using var melodySvc = SampleScheduleMelodyServiceFake.ForReleases(new Dictionary<string, MelodyMusic>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => AlarmSchedule.GetSampleSchedule(
            isNew: false,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc));

        Assert.Equal(AppConstants.SampleScheduleDiagnostics.NoSectionedPublicationForSampleScheduleMessage, ex.Message);
    }

    [Fact]
    public async Task GetSampleSchedule_Uses_First_Release_Key_When_Iam_Not_In_Releases()
    {
        var bible = BuildNwtWithSingleGenesisTrack();
        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(SampleLanguagesEnglish()),
            GetSectioned: (_, _, _) => Task.FromResult<BiblePublication?>(bible),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)));

        var onlyKey = BuildMelodyPublicationWithSections("onlyMelodyPublication", trackCode: "77");
        using var melodySvc = SampleScheduleMelodyServiceFake.ForReleases(
            new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                ["onlyMelodyPublication"] = onlyKey,
            });

        var schedule = await AlarmSchedule.GetSampleSchedule(
            isNew: true,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc);

        Assert.NotNull(schedule.Music);
        Assert.Equal("onlyMelodyPublication", schedule.Music.PublicationCode);
        Assert.False(string.IsNullOrEmpty(schedule.Music.TrackCode));
    }

    [Fact]
    public async Task GetSampleSchedule_Throws_When_No_Release_Has_Tracks_On_TopLevel()
    {
        var bible = BuildNwtWithSingleGenesisTrack();
        using var bibleSvc = new SampleScheduleBibleServiceFake(
            DistinctLanguages: _ => Task.FromResult(SampleLanguagesEnglish()),
            GetSectioned: (_, _, _) => Task.FromResult<BiblePublication?>(bible),
            ByLanguageCode: (_, _) =>
                Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase)));

        var iamEmptyMelodyPublication = new BiblePublication
        {
            PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            Name = "Iam",
            Tracks = [],
            Sections = [],
            IsMusic = true,
            IsVideo = false,
        };
        using var melodySvc = SampleScheduleMelodyServiceFake.ForReleases(
            new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.MelodyMusicPublicationCodeIam] = new MelodyMusic { Publication = iamEmptyMelodyPublication },
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => AlarmSchedule.GetSampleSchedule(
            isNew: false,
            biblePublicationService: bibleSvc,
            melodyMusicService: melodySvc));

        Assert.Equal("No melody music with tracks found in database", ex.Message);
    }

    private static Dictionary<string, Language> SampleLanguagesEnglish() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [AppConstants.Media.DefaultLanguageCode] = new Language
            {
                LanguageCode = AppConstants.Media.DefaultLanguageCode,
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            },
        };

    private static Dictionary<string, MelodyMusic> SampleMelodyIamWithSections()
    {
        var m = BuildMelodyPublicationWithSections(AppConstants.Media.MelodyMusicPublicationCodeIam, "5");
        return new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
        {
            [AppConstants.Media.MelodyMusicPublicationCodeIam] = m,
        };
    }

    private static BiblePublication BuildNwtWithSingleGenesisTrack()
    {
        var bible = new BiblePublication
        {
            PublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            Name = "NWT",
            IsVideo = false,
            IsMusic = false,
            Sections = [],
            Tracks = [],
        };

        var genesisSection = new BiblePublicationSection
        {
            Name = "Genesis",
            SectionCode = AppConstants.Media.BiblePublicationGenesisBookNumber,
            BiblePublication = bible,
            BiblePublicationId = 0,
        };

        genesisSection.Tracks =
        [
            new BiblePublicationTrack
            {
                TrackCode = "genesis-start",
                Title = "Genesis 1",
                BiblePublicationId = 0,
                BiblePublicationSectionId = null,
                Publication = bible,
                Section = genesisSection,
            },
        ];

        bible.Sections.Add(genesisSection);
        return bible;
    }

    private static MelodyMusic BuildMelodyPublicationWithSections(string publicationCode, string trackCode)
    {
        var pub = new BiblePublication
        {
            PublicationCode = publicationCode,
            Name = "Melody",
            IsVideo = false,
            IsMusic = true,
            Sections = [],
            Tracks = [],
        };

        var section = new BiblePublicationSection
        {
            Name = "Sec",
            SectionCode = "sec-a",
            BiblePublication = pub,
            BiblePublicationId = 0,
            Tracks =
            [
                new BiblePublicationTrack
                {
                    TrackCode = trackCode,
                    Title = "T",
                    BiblePublicationId = 0,
                    BiblePublicationSectionId = null,
                    Publication = pub,
                    Section = null,
                },
            ],
        };

        foreach (var t in section.Tracks)
        {
            t.Section = section;
        }

        pub.Sections.Add(section);
        pub.Tracks.AddRange(section.Tracks);
        return new MelodyMusic { Publication = pub };
    }


    private sealed class SampleScheduleBibleServiceFake : IBiblePublicationService
    {
        private readonly Func<CancellationToken, Task<Dictionary<string, Language>>> distinctLanguages;

        private readonly Func<string, string, CancellationToken, Task<BiblePublication?>> getSectioned;

        private readonly Func<string, CancellationToken, Task<Dictionary<string, BiblePublication>>> byLanguageCode;

        internal SampleScheduleBibleServiceFake(
            Func<CancellationToken, Task<Dictionary<string, Language>>> DistinctLanguages,
            Func<string, string, CancellationToken, Task<BiblePublication?>> GetSectioned,
            Func<string, CancellationToken, Task<Dictionary<string, BiblePublication>>> ByLanguageCode)
        {
            this.distinctLanguages = DistinctLanguages;
            getSectioned = GetSectioned;
            byLanguageCode = ByLanguageCode;
        }

        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default)
            => getSectioned(languageCode, publicationCode, cancellationToken);

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<BiblePublication?>(null);

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(
            string languageCode,
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default)
            => byLanguageCode(languageCode, cancellationToken);

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default)
            => distinctLanguages(cancellationToken);

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(string? CategoryCode, bool IsMusic)?>(null);

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<string>());

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    private sealed class SampleScheduleMelodyServiceFake : IMelodyMusicService
    {
        private readonly Dictionary<string, MelodyMusic> releases;

        private readonly Func<string, CancellationToken, Task<MelodyMusic?>>? getWithTracksDelegate;

        private SampleScheduleMelodyServiceFake(Dictionary<string, MelodyMusic> releases, Func<string, CancellationToken, Task<MelodyMusic?>>? getWithTracksDelegate = null)
        {
            this.releases = releases;
            this.getWithTracksDelegate = getWithTracksDelegate;
        }

        internal static SampleScheduleMelodyServiceFake ForReleases(Dictionary<string, MelodyMusic> releases) =>
            new(releases);

        internal static SampleScheduleMelodyServiceFake WithPreferredNoTracksThenAlt(
            Func<string, CancellationToken, Task<MelodyMusic?>> altHasTracksByCode)
        {
            Dictionary<string, MelodyMusic> map = new(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.MelodyMusicPublicationCodeIam] = new MelodyMusic
                {
                    Publication = new BiblePublication
                    {
                        PublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
                        Name = "iam",
                        Tracks = [],
                        Sections = [],
                    },
                },
                ["altMelodyPub"] = new MelodyMusic
                {
                    Publication = new BiblePublication
                    {
                        PublicationCode = "altMelodyPub",
                        Name = "alt",
                        Tracks = [],
                        Sections = [],
                    },
                },
            };

            return new SampleScheduleMelodyServiceFake(map, altHasTracksByCode);
        }

        public void Dispose()
        {
        }

        public Task<MelodyMusic?> GetByCodeWithTracksAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            getWithTracksDelegate != null
                ? getWithTracksDelegate(publicationCode, cancellationToken)
                : releases.TryGetValue(publicationCode, out var m)
                    ? Task.FromResult<MelodyMusic?>(m)
                    : Task.FromResult<MelodyMusic?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(releases);

        public Task<SortedDictionary<int, MusicTrack>> GetTracksByCodeAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetTracksBySectionCodeAsync(string publicationCode, string sectionCode, CancellationToken cancellationToken = default)
            => Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateTrackUrlAsync(string publicationCode, string trackCode, string url, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
