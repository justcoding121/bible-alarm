#nullable enable

using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;

namespace Bible.Alarm.Tests;

public sealed class MusicStateTrackerTests
{
    [Fact]
    public void DetectChanges_returns_false_tuple_when_schedule_null()
    {
        var sut = new MusicStateTracker();

        Assert.Equal((false, false, false, false, false), sut.DetectChanges(null));
    }

    [Fact]
    public void DetectChanges_returns_expected_flags_when_comparing_to_prior_update_snapshot()
    {
        var sut = new MusicStateTracker();

        sut.UpdateFromSchedule(new ScheduleStateItem
        {
            MusicLanguageCode = "E",
            MusicPublicationCode = "a",
            MusicSectionCode = "1",
            MusicTrackCode = "10",
            MusicRepeat = false,
        });

        var deltas = sut.DetectChanges(new ScheduleStateItem
        {
            MusicLanguageCode = "MY",
            MusicPublicationCode = "a",
            MusicSectionCode = "1",
            MusicTrackCode = "11",
            MusicRepeat = true,
        });

        Assert.True(deltas.languageCodeChanged);
        Assert.False(deltas.publicationCodeChanged);
        Assert.False(deltas.sectionCodeChanged);
        Assert.True(deltas.trackCodeChanged);
        Assert.True(deltas.repeatChanged);
    }

    [Fact]
    public void InitializeFromSchedule_populates_exposed_music_and_direction_fields_but_not_publication_section_names()
    {
        var sut = new MusicStateTracker();

        sut.InitializeFromSchedule(new ScheduleStateItem
        {
            MusicTrackCode = "t",
            MusicSectionCode = "s",
            MusicPublicationCode = "p",
            MusicLanguageCode = "E",
            MusicRepeat = null,
            MusicEnabled = false,
            BiblePublicationLanguageDirection = "ltr",
            MusicLanguageDirection = "rtl",
        });

        Assert.Equal("t", sut.LastScheduleMusicTrackCode);
        Assert.Equal("s", sut.LastMusicSectionCode);
        Assert.Equal("p", sut.LastScheduleMusicPublicationCode);
        Assert.Equal("E", sut.LastScheduleMusicLanguageCode);
        Assert.False(sut.LastScheduleMusicRepeat);
        Assert.False(sut.LastMusicEnabled);
        Assert.Equal("ltr", sut.LastBibleLanguageDirection);
        Assert.Equal("rtl", sut.LastMusicLanguageDirection);

        sut.UpdateFromSchedule(new ScheduleStateItem { MusicPublicationName = "Song book", MusicSectionName = "Chap" });

        Assert.True(sut.HasMusicPublicationNameChanged(new ScheduleStateItem { MusicPublicationName = "Other" }));
        Assert.False(sut.HasMusicPublicationNameChanged(new ScheduleStateItem { MusicPublicationName = "Song book" }));
        Assert.True(sut.HasMusicSectionNameChanged(new ScheduleStateItem { MusicSectionName = "Different" }));
    }

    [Fact]
    public void HasMusicEnabledChanged_false_when_previous_schedule_was_null_or_current_matches_cache()
    {
        var sut = new MusicStateTracker();

        Assert.False(sut.HasMusicEnabledChanged(null));

        sut.UpdateMusicEnabled(false);
        Assert.False(sut.HasMusicEnabledChanged(new ScheduleStateItem { MusicEnabled = false }));
        Assert.True(sut.HasMusicEnabledChanged(new ScheduleStateItem { MusicEnabled = true }));
    }

    [Fact]
    public void Direction_change_helpers_reflect_manual_updates_without_schedule_comparison_on_null_schedule()
    {
        var sut = new MusicStateTracker();

        sut.UpdateMusicLanguageDirection("rtl");
        Assert.False(sut.HasMusicLanguageDirectionChanged(null));
        Assert.False(sut.HasMusicLanguageDirectionChanged(new ScheduleStateItem { MusicLanguageDirection = "rtl" }));
        sut.UpdateMusicLanguageDirection("ltr");
        Assert.True(sut.HasMusicLanguageDirectionChanged(new ScheduleStateItem { MusicLanguageDirection = "rtl" }));

        sut.UpdateBibleLanguageDirection("rtl");
        Assert.False(sut.HasBibleLanguageDirectionChanged(null));
        Assert.False(sut.HasBibleLanguageDirectionChanged(new ScheduleStateItem { BiblePublicationLanguageDirection = "rtl" }));
        sut.UpdateBibleLanguageDirection("ltr");
        Assert.True(sut.HasBibleLanguageDirectionChanged(new ScheduleStateItem { BiblePublicationLanguageDirection = "rtl" }));
    }

    [Fact]
    public void ShouldTriggerDefaultMusicForNullPublication_resets_when_schedule_id_changes()
    {
        var sut = new MusicStateTracker();

        Assert.True(sut.ShouldTriggerDefaultMusicForNullPublication(10));
        sut.RecordDefaultMusicTriggered(10);
        Assert.False(sut.ShouldTriggerDefaultMusicForNullPublication(10));
        Assert.True(sut.ShouldTriggerDefaultMusicForNullPublication(11));
    }

    [Fact]
    public void InitializeFromSchedule_no_ops_when_schedule_null()
    {
        var sut = new MusicStateTracker();

        sut.InitializeFromSchedule(null);

        Assert.Null(sut.LastScheduleMusicTrackCode);
    }

    [Fact]
    public void UpdateFromSchedule_no_ops_when_schedule_null()
    {
        var sut = new MusicStateTracker();
        sut.UpdateFromSchedule(new ScheduleStateItem { MusicTrackCode = "1" });

        sut.UpdateFromSchedule(null);

        Assert.Equal("1", sut.LastScheduleMusicTrackCode);
    }

    [Fact]
    public void HasMusicPublicationNameChanged_returns_false_when_schedule_null()
    {
        var sut = new MusicStateTracker();
        sut.UpdateFromSchedule(new ScheduleStateItem { MusicPublicationName = "A" });

        Assert.False(sut.HasMusicPublicationNameChanged(null));
    }

    [Fact]
    public void HasMusicSectionNameChanged_returns_false_when_schedule_null()
    {
        var sut = new MusicStateTracker();
        sut.UpdateFromSchedule(new ScheduleStateItem { MusicSectionName = "S" });

        Assert.False(sut.HasMusicSectionNameChanged(null));
    }
}
