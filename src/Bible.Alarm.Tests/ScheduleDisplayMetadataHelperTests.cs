#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Services.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class ScheduleDisplayMetadataHelperTests
{
    [Fact]
    public void BuildScheduleTitle_music_only_schedule_uses_name_and_optional_symbol()
    {
        var plain = new ScheduleStateItem { Name = "Morning", MusicEnabled = false };
        Assert.Equal("Morning", ScheduleDisplayMetadataHelper.BuildScheduleTitle(plain));

        var withMusic = new ScheduleStateItem { Name = "Morning", MusicEnabled = true };
        Assert.Equal("Morning ♫", ScheduleDisplayMetadataHelper.BuildScheduleTitle(withMusic, musicSymbol: "♫"));

        var unnamed = new ScheduleStateItem { Name = "", MusicEnabled = false };
        Assert.Equal(AppConstants.Media.ScheduleUiUnnamedPlaceholder, ScheduleDisplayMetadataHelper.BuildScheduleTitle(unnamed));

        Assert.Equal("♫", ScheduleDisplayMetadataHelper.BuildScheduleTitle(
            new ScheduleStateItem { Name = "", MusicEnabled = true }, musicSymbol: "♫"));
    }

    [Fact]
    public void BuildScheduleTitle_bible_category_uses_section_and_track_code()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 1,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationSectionName = "Genesis",
            BiblePublicationTrackCode = "1",
        };

        Assert.Equal("Genesis 1", ScheduleDisplayMetadataHelper.BuildScheduleTitle(item));
    }

    [Fact]
    public void BuildScheduleTitle_bible_prefers_track_title_when_not_bible_category_shape()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 1,
            BiblePublicationCategoryName = "Videos",
            BiblePublicationTrackTitle = "Episode One",
            BiblePublicationName = "Series",
        };

        Assert.Equal("Episode One", ScheduleDisplayMetadataHelper.BuildScheduleTitle(item));
    }

    [Fact]
    public void BuildScheduleSubtitle_music_only_includes_status_and_time()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = null,
            IsEnabled = true,
            Hour = 8,
            Minute = 5,
        };

        var subtitle = ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(item);
        Assert.Contains(AppConstants.Media.ScheduleUiStatusEnabled, subtitle);
        Assert.Contains("08:05", subtitle);
    }

    [Fact]
    public void BuildScheduleSubtitle_bible_joins_distinct_parts_and_section_when_sectioned_non_bible_category()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 3,
            Name = "Custom",
            BiblePublicationCategoryName = "Music",
            BiblePublicationLanguageName = "English",
            BiblePublicationName = "IAM",
            BiblePublicationSectionName = "Disc 1",
            BiblePublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
        };

        var subtitle = ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(item);
        Assert.Contains("Custom", subtitle);
        Assert.Contains("English", subtitle);
        Assert.Contains("IAM", subtitle);
        Assert.Contains("Disc 1", subtitle);
    }
}
