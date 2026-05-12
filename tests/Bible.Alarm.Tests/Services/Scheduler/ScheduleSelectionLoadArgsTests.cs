#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Services.Scheduler;

namespace Bible.Alarm.Tests;

public sealed class ScheduleSelectionLoadArgsTests
{
    [Fact]
    public void LoadMusicForSelectionArgs_holds_values()
    {
        var music = new AlarmMusic { Id = 5, PublicationCode = "iam" };
        var sut = new LoadMusicForSelectionArgs(
            ScheduleId: 10,
            IsNewSchedule: false,
            CurrentMusic: music,
            PublicationCode: "p",
            LanguageCode: "L",
            TrackCode: "t",
            Repeat: true);

        Assert.Equal(10, sut.ScheduleId);
        Assert.False(sut.IsNewSchedule);
        Assert.Same(music, sut.CurrentMusic);
        Assert.Equal("p", sut.PublicationCode);
        Assert.Equal("L", sut.LanguageCode);
        Assert.Equal("t", sut.TrackCode);
        Assert.True(sut.Repeat);
    }

    [Fact]
    public void LoadBiblePublicationScheduleCodes_holds_values()
    {
        var sut = new LoadBiblePublicationScheduleCodes("lang", "pub", "sec", "trk");

        Assert.Equal("lang", sut.LanguageCode);
        Assert.Equal("pub", sut.PublicationCode);
        Assert.Equal("sec", sut.SectionCode);
        Assert.Equal("trk", sut.TrackCode);
    }

    [Fact]
    public void LoadBiblePublicationForSelectionArgs_holds_values()
    {
        var codes = new LoadBiblePublicationScheduleCodes("E", "nwt", "40", "1");
        var bible = new BiblePublicationSchedule { Id = 3, LanguageCode = "E" };
        var duration = TimeSpan.FromMinutes(2);
        var sut = new LoadBiblePublicationForSelectionArgs(
            ScheduleId: 99,
            IsNewSchedule: true,
            CurrentBiblePublication: bible,
            Codes: codes,
            FinishedDuration: duration);

        Assert.Equal(99, sut.ScheduleId);
        Assert.True(sut.IsNewSchedule);
        Assert.Same(bible, sut.CurrentBiblePublication);
        Assert.Same(codes, sut.Codes);
        Assert.Equal(duration, sut.FinishedDuration);
    }
}
