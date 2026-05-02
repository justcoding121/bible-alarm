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

    [Fact]
    public void BuildScheduleTitle_bible_category_uses_section_only_when_track_code_blank()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 1,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationSectionName = "Genesis",
            BiblePublicationTrackCode = "",
        };

        Assert.Equal("Genesis", ScheduleDisplayMetadataHelper.BuildScheduleTitle(item));
    }

    [Fact]
    public void BuildScheduleTitle_bible_resolved_title_appends_music_symbol_when_enabled()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 1,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationSectionName = "Genesis",
            BiblePublicationTrackCode = "1",
            MusicEnabled = true,
        };

        Assert.Equal("Genesis 1 ♫", ScheduleDisplayMetadataHelper.BuildScheduleTitle(item, musicSymbol: "♫"));
    }

    [Fact]
    public void BuildScheduleTitle_falls_back_to_schedule_name_when_bible_metadata_missing()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 2,
            BiblePublicationCategoryName = "",
            BiblePublicationCode = "",
            Name = "Six AM",
        };

        Assert.Equal("Six AM", ScheduleDisplayMetadataHelper.BuildScheduleTitle(item));
    }

    [Fact]
    public void BuildScheduleSubtitle_bible_category_omits_section_row_even_when_section_populated()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 5,
            Name = "Morning",
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationLanguageName = "English",
            BiblePublicationName = "New World Translation",
            BiblePublicationSectionName = "Matthew",
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
        };

        var subtitle = ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(item);
        Assert.Contains("Morning", subtitle);
        Assert.Contains("English", subtitle);
        Assert.DoesNotContain("Matthew", subtitle, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildScheduleSubtitle_skips_category_when_same_as_schedule_name_after_trim()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 8,
            Name = "videos",
            BiblePublicationCategoryName = "Videos",
            BiblePublicationLanguageName = "English",
        };

        var subtitle = ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(item);
        Assert.Contains("videos", subtitle);
        Assert.Contains("English", subtitle);
        Assert.DoesNotContain("Videos", subtitle);
    }

    [Fact]
    public void BuildScheduleSubtitle_disabled_bible_falls_back_to_status_when_no_parts()
    {
        var item = new ScheduleStateItem
        {
            BiblePublicationScheduleId = 9,
            Name = "",
            BiblePublicationCategoryName = "",
            BiblePublicationCode = "",
            IsEnabled = false,
            Hour = 22,
            Minute = 30,
        };

        var subtitle = ScheduleDisplayMetadataHelper.BuildScheduleSubtitle(item);
        Assert.Contains(AppConstants.Media.ScheduleUiStatusDisabled, subtitle);
        Assert.Contains("10:30", subtitle);
    }
}
