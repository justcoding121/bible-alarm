#nullable enable

using AutoMapper;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Selectors;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ApplicationSelectorsTests
{
    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<ScheduleMappingProfile>(), NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static ScheduleStateItem Row(int id) =>
        new()
        {
            Id = id,
            Name = $"N{id}",
            IsEnabled = true,
            Hour = 7,
            Minute = 10,
            Second = 0,
            DaysOfWeek = WeekDays.Tuesday,
            NotificationEnabled = true,
            MusicEnabled = true,
        };

    [Fact]
    public void GetCurrentMusicEntity_returns_music_when_music_id_set_without_publication_code()
    {
        var schedule = Row(3);
        schedule.MusicId = 99;
        var state = new ApplicationState([], currentSchedule: schedule);
        var mapper = CreateMapper();

        var music = ApplicationSelectors.GetCurrentMusicEntity(state, mapper);

        Assert.NotNull(music);
        Assert.Equal(99, music!.Id);
    }

    [Fact]
    public void GetCurrentBiblePublicationEntity_returns_child_when_only_bible_publication_row_id_set()
    {
        var schedule = Row(4);
        schedule.BiblePublicationScheduleId = 100;
        schedule.BiblePublicationCode = "nwt";

        var state = new ApplicationState([], currentSchedule: schedule);
        var mapper = CreateMapper();

        var bible = ApplicationSelectors.GetCurrentBiblePublicationEntity(state, mapper);

        Assert.NotNull(bible);
        Assert.Equal("nwt", bible!.PublicationCode);
    }

    [Fact]
    public void GetAllSchedulesEntities_returns_empty_when_schedules_property_null()
    {
        var state = new ApplicationState([]);
        state.Schedules = null!;
        var mapper = CreateMapper();

        Assert.Empty(ApplicationSelectors.GetAllSchedulesEntities(state, mapper));
    }

    [Fact]
    public void GetScheduleByIdEntity_returns_null_when_schedules_property_null()
    {
        var state = new ApplicationState([]);
        state.Schedules = null!;
        var mapper = CreateMapper();

        Assert.Null(ApplicationSelectors.GetScheduleByIdEntity(state, 1, mapper));
    }

    [Fact]
    public void GetCurrentScheduleEntity_returns_null_when_no_current()
    {
        var state = new ApplicationState([]);
        var mapper = CreateMapper();

        Assert.Null(ApplicationSelectors.GetCurrentScheduleEntity(state, mapper));
    }

    [Fact]
    public void GetCurrentScheduleEntity_maps_current_when_present()
    {
        var current = Row(9);
        var state = new ApplicationState([], currentSchedule: current);
        var mapper = CreateMapper();

        var entity = ApplicationSelectors.GetCurrentScheduleEntity(state, mapper);

        Assert.NotNull(entity);
        Assert.Equal(9, entity!.Id);
        Assert.Equal("N9", entity.Name);
    }

    [Fact]
    public void GetCurrentMusicEntity_returns_null_when_current_schedule_null()
    {
        var state = new ApplicationState([]);
        var mapper = CreateMapper();

        Assert.Null(ApplicationSelectors.GetCurrentMusicEntity(state, mapper));
    }

    [Fact]
    public void GetCurrentBiblePublicationEntity_returns_null_when_current_schedule_null()
    {
        var state = new ApplicationState([]);
        var mapper = CreateMapper();

        Assert.Null(ApplicationSelectors.GetCurrentBiblePublicationEntity(state, mapper));
    }

    [Fact]
    public void GetCurrentMusicEntity_returns_null_when_flattened_music_absent()
    {
        var state = new ApplicationState([], currentSchedule: Row(1));
        var mapper = CreateMapper();

        Assert.Null(ApplicationSelectors.GetCurrentMusicEntity(state, mapper));
    }

    [Fact]
    public void GetCurrentMusicEntity_returns_music_when_publication_code_set()
    {
        var schedule = Row(3);
        schedule.MusicPublicationCode = "iam";
        schedule.MusicTrackCode = "5";
        var state = new ApplicationState([], currentSchedule: schedule);
        var mapper = CreateMapper();

        var music = ApplicationSelectors.GetCurrentMusicEntity(state, mapper);

        Assert.NotNull(music);
        Assert.Equal("iam", music!.PublicationCode);
        Assert.Equal("5", music.TrackCode);
    }

    [Fact]
    public void GetCurrentBiblePublicationEntity_returns_null_when_flattened_absent()
    {
        var state = new ApplicationState([], currentSchedule: Row(1));
        var mapper = CreateMapper();

        Assert.Null(ApplicationSelectors.GetCurrentBiblePublicationEntity(state, mapper));
    }

    [Fact]
    public void GetCurrentBiblePublicationEntity_returns_child_when_linked_row_present()
    {
        var schedule = Row(4);
        schedule.BiblePublicationScheduleId = 100;
        schedule.BiblePublicationLanguageCode = "E";
        schedule.BiblePublicationCode = "nwt";

        var state = new ApplicationState([], currentSchedule: schedule);
        var mapper = CreateMapper();

        var bible = ApplicationSelectors.GetCurrentBiblePublicationEntity(state, mapper);

        Assert.NotNull(bible);
        Assert.Equal("E", bible!.LanguageCode);
        Assert.Equal("nwt", bible.PublicationCode);
    }

    [Fact]
    public void GetAllSchedulesEntities_returns_empty_when_no_schedules()
    {
        var state = new ApplicationState([]);
        var mapper = CreateMapper();

        Assert.Empty(ApplicationSelectors.GetAllSchedulesEntities(state, mapper));
    }

    [Fact]
    public void GetAllSchedulesEntities_projects_each_schedule()
    {
        var set = new ObservableHashSet<ScheduleStateItem> { Row(1), Row(2) };
        var state = new ApplicationState(set);
        var mapper = CreateMapper();

        var list = ApplicationSelectors.GetAllSchedulesEntities(state, mapper);

        Assert.Equal([1, 2], list.OrderBy(a => a.Id).Select(a => a.Id));
    }

    [Fact]
    public void GetScheduleByIdEntity_returns_match_or_null()
    {
        var set = new ObservableHashSet<ScheduleStateItem> { Row(10), Row(20) };
        var state = new ApplicationState(set);
        var mapper = CreateMapper();

        Assert.Null(ApplicationSelectors.GetScheduleByIdEntity(state, 999, mapper));
        var found = ApplicationSelectors.GetScheduleByIdEntity(state, 10, mapper);
        Assert.NotNull(found);
        Assert.Equal(10, found!.Id);
    }
}
