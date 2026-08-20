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

public sealed class MusicDisplayNamePopulatorTests
{
    private static AlarmSchedule BaseAlarmSchedule() =>
        new()
        {
            Id = 99,
            Name = "Evening",
            IsEnabled = true,
            Hour = 20,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
        };

    private static AlarmSchedule WithMusic(AlarmMusic music)
    {
        var schedule = BaseAlarmSchedule();
        schedule.Music = music;
        music.AlarmScheduleId = schedule.Id;
        music.AlarmSchedule = schedule;
        return schedule;
    }

    [Fact]
    public void Populate_WhenMusicMissing_ReturnsEarly()
    {
        var schedule = BaseAlarmSchedule();
        schedule.Music = null;

        var state = new ScheduleStateItem();

        MusicDisplayNamePopulator.Populate(schedule, state, LookupTestData.EmptyLookup());

        Assert.Null(state.MusicLanguageName);
        Assert.Null(state.MusicPublicationName);
    }

    [Fact]
    public void Populate_MelodyUsesEnglishLanguageRowWhenDefaultLanguagePresentInLookup()
    {
        var music = new AlarmMusic
        {
            PublicationCode = "iam",
            LanguageCode = null,
            SectionCode = null,
            TrackCode = "1",
            Repeat = false,
        };

        var schedule = WithMusic(music);

        var lookup = LookupTestData.EmptyLookup() with
        {
            VocalLanguages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                [AppConstants.Media.DefaultLanguageCode] = new Language
                {
                    LanguageCode = AppConstants.Media.DefaultLanguageCode,
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                },
            },
        };

        var languageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AppConstants.Media.DefaultLanguageCode] = "English",
        };

        var state = new ScheduleStateItem();

        MusicDisplayNamePopulator.Populate(schedule, state, lookup, languageNames);

        Assert.Equal("English", state.MusicLanguageName);
        Assert.Equal(AppConstants.Media.TextDirectionLeftToRight, state.MusicLanguageDirection);
    }

    [Fact]
    public void Populate_MelodyMapsPublicationAndFlatTracksFromLookup()
    {
        var music = new AlarmMusic
        {
            PublicationCode = "iam",
            LanguageCode = "",
            TrackCode = "5",
            Repeat = false,
        };

        var schedule = WithMusic(music);

        var melodyPublication = new BiblePublication
        {
            Name = "Kingdom Melodies",
            PublicationCode = "iam",
            LanguageId = null,
            IsVideo = false,
            IsMusic = true,
        };

        var vocalLanguages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
        {
            [AppConstants.Media.DefaultLanguageCode] = new Language
            {
                LanguageCode = AppConstants.Media.DefaultLanguageCode,
                Direction = AppConstants.Media.TextDirectionLeftToRight,
            },
        };

        var melodyTracks = new SortedDictionary<int, MusicTrack>
        {
            [1] = new MusicTrack { TrackCode = "5", Title = "Fifth Song" },
        };

        var lookup = LookupTestData.EmptyLookup() with
        {
            VocalLanguages = vocalLanguages,
            MelodyReleases = new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase)
            {
                ["iam"] = new MelodyMusic { Publication = melodyPublication },
            },
            MelodyTracksFlat = new Dictionary<string, SortedDictionary<int, MusicTrack>>(StringComparer.OrdinalIgnoreCase)
            {
                ["iam"] = melodyTracks,
            },
        };

        var state = new ScheduleStateItem();

        MusicDisplayNamePopulator.Populate(schedule, state, lookup);

        Assert.Equal("Kingdom Melodies", state.MusicPublicationName);
        Assert.Equal("Fifth Song", state.MusicTrackName);
    }

    [Fact]
    public void Populate_VocalUsesLanguageCodeWhenLanguageMissingFromLookup()
    {
        var music = new AlarmMusic
        {
            PublicationCode = "song",
            LanguageCode = "ZZ",
            TrackCode = "1",
            Repeat = false,
        };

        var schedule = WithMusic(music);

        var state = new ScheduleStateItem();

        MusicDisplayNamePopulator.Populate(schedule, state, LookupTestData.EmptyLookup());

        Assert.Equal("ZZ", state.MusicLanguageName);
        Assert.Equal(AppConstants.Media.TextDirectionLeftToRight, state.MusicLanguageDirection);
    }

    [Fact]
    public void Populate_VocalMapsReleaseNameAndTrackTitleFromLookup()
    {
        var music = new AlarmMusic
        {
            PublicationCode = "sjjc",
            LanguageCode = "E",
            TrackCode = "2",
            Repeat = false,
        };
        var schedule = WithMusic(music);
        var lookup = LookupTestData.EmptyLookup() with
        {
            VocalLanguages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase)
            {
                ["E"] = new Language
                {
                    LanguageCode = "E",
                    Direction = AppConstants.Media.TextDirectionLeftToRight,
                },
            },
            VocalReleases = new Dictionary<(string LanguageCode, string PublicationCode), VocalMusic>(
                PublicationLookupKeyComparers.LanguagePublication.Instance)
            {
                [("E", "sjjc")] = new VocalMusic
                {
                    Publication = new BiblePublication { Name = "Sing Out Joyfully", PublicationCode = "sjjc" },
                },
            },
            VocalTracks = new Dictionary<(string LanguageCode, string PublicationCode), SortedDictionary<int, MusicTrack>>(
                PublicationLookupKeyComparers.LanguagePublication.Instance)
            {
                [("E", "sjjc")] = new SortedDictionary<int, MusicTrack>
                {
                    [2] = new MusicTrack { TrackCode = "2", Title = "Jehovah Is Our King" },
                },
            },
        };
        var languageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["E"] = "English",
        };

        var state = new ScheduleStateItem();
        MusicDisplayNamePopulator.Populate(schedule, state, lookup, languageNames);

        Assert.Equal("English", state.MusicLanguageName);
        Assert.Equal("Sing Out Joyfully", state.MusicPublicationName);
        Assert.Equal("Jehovah Is Our King", state.MusicTrackName);
    }

    [Fact]
    public void Populate_MelodyPrefersSectionedTracksOverFlat()
    {
        var music = new AlarmMusic
        {
            PublicationCode = "iam",
            LanguageCode = null,
            SectionCode = "iam-2",
            TrackCode = "4",
            Repeat = false,
        };
        var schedule = WithMusic(music);
        var lookup = LookupTestData.EmptyLookup() with
        {
            MelodyTracksBySection = new Dictionary<(string PublicationCode, string SectionCode), SortedDictionary<int, MusicTrack>>(
                PublicationLookupKeyComparers.PublicationSection.Instance)
            {
                [("iam", "iam-2")] = new SortedDictionary<int, MusicTrack>
                {
                    [4] = new MusicTrack { TrackCode = "4", Title = "Sectioned melody" },
                },
            },
            MelodyTracksFlat = new Dictionary<string, SortedDictionary<int, MusicTrack>>(StringComparer.OrdinalIgnoreCase)
            {
                ["iam"] = new SortedDictionary<int, MusicTrack>
                {
                    [4] = new MusicTrack { TrackCode = "4", Title = "Flat melody" },
                },
            },
        };

        var state = new ScheduleStateItem();
        MusicDisplayNamePopulator.Populate(schedule, state, lookup);

        Assert.Equal("Sectioned melody", state.MusicTrackName);
    }
}
