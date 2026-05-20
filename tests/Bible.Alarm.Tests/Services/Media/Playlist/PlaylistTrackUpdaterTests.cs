#nullable enable

using Bible.Alarm.Services.Media.Playlist;
using Bible.Alarm.Services.Media.PlaylistServiceHelpers;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Tests;

public sealed class PlaylistTrackUpdaterTests
{
    [Fact]
    public void UpdateMusicTrack_updates_codes_when_repeat_false_and_codes_present()
    {
        var schedule = new AlarmSchedule
        {
            Music = new AlarmMusic { Repeat = false, TrackCode = "1", SectionCode = "m1", PublicationCode = "iam" },
        };

        PlaylistTrackUpdater.UpdateMusicTrack(schedule, "2");

        Assert.Equal("2", schedule.Music!.TrackCode);
        Assert.Equal("m1", schedule.Music.SectionCode);

        PlaylistTrackUpdater.UpdateMusicTrack(schedule, "3", "mx");

        Assert.Equal("3", schedule.Music.TrackCode);
        Assert.Equal("mx", schedule.Music.SectionCode);
    }

    [Fact]
    public void UpdateMusicTrack_no_op_when_repeat_or_blank_track()
    {
        var schedule = new AlarmSchedule
        {
            Music = new AlarmMusic { Repeat = true, TrackCode = "1" },
        };

        PlaylistTrackUpdater.UpdateMusicTrack(schedule, "9");
        Assert.Equal("1", schedule.Music!.TrackCode);

        schedule.Music!.Repeat = false;
        PlaylistTrackUpdater.UpdateMusicTrack(schedule, " ");
        PlaylistTrackUpdater.UpdateMusicTrack(schedule, null);

        Assert.Equal("1", schedule.Music!.TrackCode);
    }

    [Fact]
    public void UpdateBiblePublicationTrack_writes_metadata_and_sets_section_null_when_white_space()
    {
        var bib = new BiblePublicationSchedule { SectionCode = "old", PublicationCode = "old-pub", TrackCode = "99" };
        var schedule = new AlarmSchedule { BiblePublicationSchedule = bib };
        var meta = new TrackMetadata
        {
            SectionCode = "   ",
            TrackCode = "7",
            PublicationCode = "nwt",
            FinishedDuration = TimeSpan.FromMinutes(3),
            IsBibleContent = true,
            LookUpPath = "stub",
        };

        PlaylistTrackUpdater.UpdateBiblePublicationTrack(schedule, meta);

        Assert.Null(bib.SectionCode);
        Assert.Equal("7", bib.TrackCode);
        Assert.Equal("nwt", bib.PublicationCode);
        Assert.Equal(TimeSpan.FromMinutes(3), bib.FinishedDuration);
    }

    [Fact]
    public void UpdateBiblePublicationTrack_throws_when_schedule_missing_publication_schedule()
    {
        var schedule = new AlarmSchedule { BiblePublicationSchedule = null };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PlaylistTrackUpdater.UpdateBiblePublicationTrack(schedule,
                new TrackMetadata { TrackCode = "1", PublicationCode = "x", LookUpPath = "s", IsBibleContent = true }));

        Assert.Contains("BiblePublicationSchedule is null", ex.Message);
    }

    [Fact]
    public void UpdateBiblePublicationTrackForFinished_overwrites_from_navigation_result_or_throws_when_next_null()
    {
        var bib = new BiblePublicationSchedule();
        var schedule = new AlarmSchedule { Id = 2, BiblePublicationSchedule = bib };
        var meta = new TrackMetadata { PublicationCode = "irrelevant-for-path", TrackCode = "x", LookUpPath = "l" };

        var next = new TrackNavigationResult(
            "next-pub",
            new BiblePublicationSection { SectionCode = "sc" },
            new BiblePublicationTrack { TrackCode = "22" });

        PlaylistTrackUpdater.UpdateBiblePublicationTrackForFinished(schedule, meta, next);

        Assert.Equal("sc", bib.SectionCode);
        Assert.Equal("22", bib.TrackCode);
        Assert.Equal("next-pub", bib.PublicationCode);
        Assert.Equal(TimeSpan.Zero, bib.FinishedDuration);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PlaylistTrackUpdater.UpdateBiblePublicationTrackForFinished(schedule, meta, null));

        Assert.Contains("Next track is null", ex.Message);
    }

    [Fact]
    public void UpdateMusicTrackForFinished_matches_UpdateMusicTrack_rules()
    {
        var schedule = new AlarmSchedule
        {
            Music = new AlarmMusic { Repeat = false, TrackCode = "1" },
        };

        PlaylistTrackUpdater.UpdateMusicTrackForFinished(schedule, "88");
        Assert.Equal("88", schedule.Music!.TrackCode);

        PlaylistTrackUpdater.UpdateMusicTrackForFinished(schedule, "99", "disc-2");
        Assert.Equal("99", schedule.Music!.TrackCode);
        Assert.Equal("disc-2", schedule.Music.SectionCode);
    }

    [Fact]
    public void UpdateScheduleForPlayedTrackInternal_routes_music_versus_bible_by_play_type()
    {
        var musicSchedule = new AlarmSchedule
        {
            Music = new AlarmMusic { Repeat = false, TrackCode = "0" },
        };
        PlaylistTrackUpdater.UpdateScheduleForPlayedTrackInternal(
            musicSchedule,
            new TrackMetadata { IsBibleContent = false, LookUpPath = "m", TrackCode = "music", PublicationCode = "iam" },
            "5");

        Assert.Equal("5", musicSchedule.Music!.TrackCode);

        var bibleSched = new AlarmSchedule { BiblePublicationSchedule = new BiblePublicationSchedule { TrackCode = "1" } };
        var bibleMeta = new TrackMetadata
        {
            IsBibleContent = true,
            PublicationCode = "nwt",
            TrackCode = "10",
            SectionCode = "40",
            LookUpPath = "b",
            FinishedDuration = TimeSpan.FromSeconds(2),
        };
        PlaylistTrackUpdater.UpdateScheduleForPlayedTrackInternal(bibleSched, bibleMeta, nextTrackCode: null);

        Assert.Equal("10", bibleSched.BiblePublicationSchedule!.TrackCode);
        Assert.Equal("40", bibleSched.BiblePublicationSchedule.SectionCode);
    }
}
