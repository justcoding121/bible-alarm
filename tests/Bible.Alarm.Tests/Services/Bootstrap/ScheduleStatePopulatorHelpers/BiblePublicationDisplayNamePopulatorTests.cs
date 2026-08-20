#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationDisplayNamePopulatorTests
{
    private static AlarmSchedule BaseAlarmSchedule() =>
        new()
        {
            Id = 42,
            Name = "Morning",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
        };

    private static AlarmSchedule WithBibleSchedule(BiblePublicationSchedule bible)
    {
        var schedule = BaseAlarmSchedule();
        schedule.BiblePublicationSchedule = bible;
        bible.AlarmScheduleId = schedule.Id;
        bible.AlarmSchedule = schedule;
        return schedule;
    }

    [Fact]
    public void Populate_WhenBiblePublicationScheduleMissing_ReturnsWithoutChangingDisplayFields()
    {
        var schedule = BaseAlarmSchedule();
        schedule.BiblePublicationSchedule = null;

        var state = new ScheduleStateItem();

        BiblePublicationDisplayNamePopulator.Populate(
            schedule,
            state,
            LookupTestData.EmptyLookup(),
            languagesDict: new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            });

        Assert.Null(state.BiblePublicationLanguageName);
        Assert.Null(state.BiblePublicationName);
    }

    [Fact]
    public void Populate_WhenLanguagesDictMissing_LeavesLanguageFieldsUnset()
    {
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "nwt",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
            FinishedDuration = TimeSpan.Zero,
        };

        var schedule = WithBibleSchedule(bible);
        var state = new ScheduleStateItem();

        BiblePublicationDisplayNamePopulator.Populate(schedule, state, LookupTestData.EmptyLookup(), languagesDict: null);

        Assert.Null(state.BiblePublicationLanguageName);
    }

    [Fact]
    public void Populate_WhenLanguageUnknown_SetsCodeAsDisplayNameAndLtrDirection()
    {
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "nwt",
            LanguageCode = "ZZ",
            SectionCode = "40",
            TrackCode = "1",
            FinishedDuration = TimeSpan.Zero,
        };

        var schedule = WithBibleSchedule(bible);

        var languagesDict = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
        {
            ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionRightToLeft },
        };

        var state = new ScheduleStateItem();

        BiblePublicationDisplayNamePopulator.Populate(schedule, state, LookupTestData.EmptyLookup(), languagesDict);

        Assert.Equal("ZZ", state.BiblePublicationLanguageName);
        Assert.Equal(AppConstants.Media.TextDirectionLeftToRight, state.BiblePublicationLanguageDirection);
    }

    [Fact]
    public void Populate_WhenLanguageKnown_AppliesDirectionFromLookupDictionary()
    {
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "nwt",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
            FinishedDuration = TimeSpan.Zero,
        };

        var schedule = WithBibleSchedule(bible);

        var languagesDict = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
        {
            ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionRightToLeft },
        };

        var languageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["E"] = "English",
        };

        var state = new ScheduleStateItem();

        BiblePublicationDisplayNamePopulator.Populate(schedule, state, LookupTestData.EmptyLookup(), languagesDict, languageNames);

        Assert.Equal("English", state.BiblePublicationLanguageName);
        Assert.Equal(AppConstants.Media.TextDirectionRightToLeft, state.BiblePublicationLanguageDirection);
    }

    [Fact]
    public void Populate_NoLanguagePublication_maps_name_category_and_music_flag()
    {
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "iam",
            LanguageCode = "E",
            SectionCode = "iam-1",
            TrackCode = "3",
        };
        var schedule = WithBibleSchedule(bible);
        var languagesDict = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
        {
            ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
        };
        var languageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["E"] = "English",
        };

        var lookup = LookupTestData.EmptyLookup() with
        {
            NoLanguagePublications = new Dictionary<string, LookupDataLoader.NoLanguagePublicationMeta>(StringComparer.OrdinalIgnoreCase)
            {
                ["iam"] = new LookupDataLoader.NoLanguagePublicationMeta("Kingdom Melodies", 9, "Music", IsMusic: true),
            },
            NoLanguageSections = new Dictionary<(string PublicationCode, string SectionCode), string>(
                PublicationLookupKeyComparers.PublicationSection.Instance)
            {
                [("iam", "iam-1")] = "Disc 1",
            },
            NoLanguageTrackTitles = new Dictionary<(string PublicationCode, string? SectionCode, string TrackCode), string>(
                PublicationLookupKeyComparers.PublicationNullableSectionTrack.Instance)
            {
                [("iam", "iam-1", "3")] = "Melody 3",
            },
        };

        var state = new ScheduleStateItem();
        BiblePublicationDisplayNamePopulator.Populate(schedule, state, lookup, languagesDict, languageNames);

        Assert.Equal("English", state.BiblePublicationLanguageName);
        Assert.Equal("Kingdom Melodies", state.BiblePublicationName);
        Assert.True(state.BiblePublicationIsMusic);
        Assert.Equal(9, state.BiblePublicationCategoryId);
        Assert.Equal("Music", state.BiblePublicationCategoryName);
        Assert.Equal("Disc 1", state.BiblePublicationSectionName);
        Assert.Equal("Melody 3", state.BiblePublicationTrackTitle);
    }

    [Fact]
    public void Populate_LanguageBoundPublication_maps_sectioned_track_and_category()
    {
        var bible = new BiblePublicationSchedule
        {
            PublicationCode = "nwt",
            LanguageCode = "E",
            SectionCode = "40",
            TrackCode = "1",
        };
        var schedule = WithBibleSchedule(bible);
        var category = new Category { CategoryCode = "Bible" };
        var publication = new BiblePublication
        {
            Name = "New World Translation",
            PublicationCode = "nwt",
            IsMusic = false,
            BiblePublicationCategories = [new BiblePublicationCategory { Category = category, CategoryId = 1 }],
        };
        publication.Sections.Add(new BiblePublicationSection
        {
            SectionCode = "40",
            Name = "Matthew",
            Tracks =
            [
                new BiblePublicationTrack { TrackCode = "1", Title = "Matthew 1" },
            ],
        });

        var lookup = LookupTestData.EmptyLookup() with
        {
            Publications = new Dictionary<(string LanguageCode, string PublicationCode), BiblePublication>(
                PublicationLookupKeyComparers.LanguagePublication.Instance)
            {
                [("E", "nwt")] = publication,
            },
            Sections = new Dictionary<(string LanguageCode, string PublicationCode, string SectionCode), string>(
                PublicationLookupKeyComparers.LanguagePublicationSection.Instance)
            {
                [("E", "nwt", "40")] = "Matthew",
            },
        };
        var languagesDict = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
        {
            ["E"] = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
        };

        var state = new ScheduleStateItem();
        BiblePublicationDisplayNamePopulator.Populate(schedule, state, lookup, languagesDict);

        Assert.Equal("New World Translation", state.BiblePublicationName);
        Assert.Equal("Matthew", state.BiblePublicationSectionName);
        Assert.Equal("Matthew 1", state.BiblePublicationTrackTitle);
        Assert.Equal("Bible", state.BiblePublicationCategoryName);
        Assert.False(state.BiblePublicationIsMusic);
    }
}
