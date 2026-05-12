#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Stores.Mapping;
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
}
