#nullable enable

using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Tests;

public sealed class ScheduleSelectionServiceTests
{
    [Fact]
    public void LoadMusicForSelection_returns_constructed_alarm_when_publication_and_track_codes_present()
    {
        var sut = new ScheduleSelectionService();
        var existing = new AlarmMusic { Id = 99, PublicationCode = "old" };
        var args = new LoadMusicForSelectionArgs(
            ScheduleId: 5,
            IsNewSchedule: false,
            CurrentMusic: existing,
            PublicationCode: "nwt",
            LanguageCode: "E",
            TrackCode: "1",
            Repeat: true);

        var result = sut.LoadMusicForSelection(args);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Id);
        Assert.Equal("nwt", result.PublicationCode);
        Assert.Equal("E", result.LanguageCode);
        Assert.Equal("1", result.TrackCode);
        Assert.True(result.Repeat);
        Assert.Equal(5, result.AlarmScheduleId);
    }

    [Fact]
    public void LoadMusicForSelection_falls_back_to_current_music_when_track_code_missing()
    {
        var sut = new ScheduleSelectionService();
        var existing = new AlarmMusic { Id = 12, PublicationCode = "osg", TrackCode = "3" };
        var args = new LoadMusicForSelectionArgs(
            1,
            false,
            existing,
            PublicationCode: "nwt",
            LanguageCode: "E",
            TrackCode: null,
            Repeat: false);

        var result = sut.LoadMusicForSelection(args);

        Assert.Same(existing, result);
    }

    [Fact]
    public void LoadBiblePublicationForSelection_returns_current_when_new_schedule()
    {
        var sut = new ScheduleSelectionService();
        var current = new BiblePublicationSchedule { PublicationCode = "nwt" };
        var args = new LoadBiblePublicationForSelectionArgs(
            ScheduleId: 1,
            IsNewSchedule: true,
            CurrentBiblePublication: current,
            Codes: new LoadBiblePublicationScheduleCodes("E", "nwt", "1", "10"),
            FinishedDuration: null);

        var result = sut.LoadBiblePublicationForSelection(args);

        Assert.Same(current, result);
    }

    [Fact]
    public void LoadBiblePublicationForSelection_builds_from_codes_when_existing_schedule_and_codes_complete()
    {
        var sut = new ScheduleSelectionService();
        var args = new LoadBiblePublicationForSelectionArgs(
            ScheduleId: 7,
            IsNewSchedule: false,
            CurrentBiblePublication: new BiblePublicationSchedule { Id = 99 },
            Codes: new LoadBiblePublicationScheduleCodes("E", "nwt", "  gen  ", "5"),
            FinishedDuration: TimeSpan.FromMinutes(2));

        var result = sut.LoadBiblePublicationForSelection(args);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Id);
        Assert.Equal("E", result.LanguageCode);
        Assert.Equal("nwt", result.PublicationCode);
        Assert.Equal("gen", result.SectionCode);
        Assert.Equal("5", result.TrackCode);
        Assert.Equal(TimeSpan.FromMinutes(2), result.FinishedDuration);
        Assert.Equal(7, result.AlarmScheduleId);
    }

    [Fact]
    public void LoadBiblePublicationForSelection_falls_back_when_track_code_blank()
    {
        var sut = new ScheduleSelectionService();
        var fallback = new BiblePublicationSchedule { PublicationCode = "bi12" };
        var args = new LoadBiblePublicationForSelectionArgs(
            2,
            false,
            fallback,
            Codes: new LoadBiblePublicationScheduleCodes("E", "nwt", "1", TrackCode: "   "),
            null);

        var result = sut.LoadBiblePublicationForSelection(args);

        Assert.Same(fallback, result);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = new ScheduleSelectionService();
        sut.Dispose();
        sut.Dispose();
    }
}
