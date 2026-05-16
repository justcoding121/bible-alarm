#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class ScheduleSaveServiceTests
{
    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<ScheduleMappingProfile>(), NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    [Fact]
    public async Task PrepareModelForSaveAsync_clears_music_when_existing_schedule_was_not_music_updated()
    {
        var sut = new ScheduleSaveService(TestLogging.CreateLogger(), CreateMapper());
        var current = new ScheduleStateItem
        {
            Id = 10,
            BiblePublicationCode = "nwt",
            BiblePublicationIsMusic = false,
            MusicEnabled = true,
            MusicPublicationCode = "mel-pub",
            MusicTrackCode = "12",
            NumberOfTracksToPlay = 3,
            AlwaysPlayFromStart = true,
        };

        var model = await sut.PrepareModelForSaveAsync(current, isNewSchedule: false, musicUpdated: false);

        Assert.Null(model.Music);
    }

    [Fact]
    public async Task PrepareModelForSaveAsync_populates_music_when_music_updated()
    {
        var sut = new ScheduleSaveService(TestLogging.CreateLogger(), CreateMapper());
        var current = new ScheduleStateItem
        {
            Id = 3,
            Name = "With music",
            BiblePublicationCode = "nwt",
            MusicEnabled = true,
            MusicPublicationCode = "sjjm",
            MusicLanguageCode = "E",
            MusicTrackCode = "5",
            MusicRepeat = true,
        };

        var model = await sut.PrepareModelForSaveAsync(current, isNewSchedule: false, musicUpdated: true);

        Assert.NotNull(model.Music);
        Assert.Equal("5", model.Music!.TrackCode);
        Assert.Equal("sjjm", model.Music.PublicationCode);
        Assert.Equal("E", model.Music.LanguageCode);
        Assert.True(model.Music.Repeat);
    }

    [Fact]
    public async Task PrepareModelForSaveAsync_clears_music_for_music_category_publication()
    {
        var sut = new ScheduleSaveService(TestLogging.CreateLogger(), CreateMapper());
        var current = new ScheduleStateItem
        {
            Id = 4,
            BiblePublicationCode = AppConstants.Media.MediatorCategoryKeyChildrenSongs,
            BiblePublicationIsMusic = true,
            MusicEnabled = true,
            MusicPublicationCode = "sjjm",
            MusicTrackCode = "1",
        };

        var model = await sut.PrepareModelForSaveAsync(current, isNewSchedule: false, musicUpdated: true);

        Assert.False(model.MusicEnabled);
        Assert.Null(model.Music);
    }

    [Fact]
    public async Task PrepareModelForSaveAsync_defaults_empty_bible_publication_code_to_nwt()
    {
        var sut = new ScheduleSaveService(TestLogging.CreateLogger(), CreateMapper());
        var current = new ScheduleStateItem
        {
            Id = 5,
            BiblePublicationScheduleId = 100,
            BiblePublicationCategoryName = "Bible",
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = string.Empty,
            BiblePublicationTrackCode = "1",
        };

        var model = await sut.PrepareModelForSaveAsync(current, isNewSchedule: true, musicUpdated: false);

        Assert.NotNull(model.BiblePublicationSchedule);
        Assert.Equal(AppConstants.Media.BiblePublicationCodeNwt, model.BiblePublicationSchedule!.PublicationCode);
    }

    [Fact]
    public void PrepareScheduleStateItem_preserves_display_names_from_current_schedule()
    {
        var sut = new ScheduleSaveService(TestLogging.CreateLogger(), CreateMapper());
        var current = new ScheduleStateItem
        {
            Id = 6,
            BiblePublicationCategoryId = 2,
            BiblePublicationCategoryName = "Bible",
            BiblePublicationLanguageName = "English",
            BiblePublicationName = "New World Translation",
            MusicTrackName = "Song 1",
            NumberOfTracksToPlay = 2,
            AlwaysPlayFromStart = false,
        };
        var model = new AlarmSchedule
        {
            Id = 6,
            Name = "Morning",
            MusicEnabled = false,
        };

        var item = sut.PrepareScheduleStateItem(model, current, musicUpdated: false);

        Assert.Equal("English", item.BiblePublicationLanguageName);
        Assert.Equal("New World Translation", item.BiblePublicationName);
        Assert.Equal("Song 1", item.MusicTrackName);
        Assert.Equal(2, item.NumberOfTracksToPlay);
    }
}
