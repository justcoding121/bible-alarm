#nullable enable

using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
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
}
