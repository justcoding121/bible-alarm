#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Stores.Reducers.Services;

namespace Bible.Alarm.Tests;

public sealed class DisplayNamePreservationHelperTests
{
    [Fact]
    public void PreserveBiblePublicationDisplayNames_copies_category_when_action_missing()
    {
        var action = new ScheduleStateItem { Id = 1, BiblePublicationCategoryName = "" };
        var existing = new ScheduleStateItem
        {
            BiblePublicationCategoryId = 9,
            BiblePublicationCategoryName = "Music",
        };

        DisplayNamePreservationHelper.PreserveBiblePublicationDisplayNames(action, existing);

        Assert.Equal(9, action.BiblePublicationCategoryId);
        Assert.Equal("Music", action.BiblePublicationCategoryName);
    }

    [Fact]
    public void PreserveBiblePublicationDisplayNames_copies_language_and_publication_names_when_blank_on_action()
    {
        var action = new ScheduleStateItem
        {
            BiblePublicationLanguageName = "",
            BiblePublicationName = "",
        };
        var existing = new ScheduleStateItem
        {
            BiblePublicationLanguageName = "English",
            BiblePublicationName = "New World Translation",
        };

        DisplayNamePreservationHelper.PreserveBiblePublicationDisplayNames(action, existing);

        Assert.Equal("English", action.BiblePublicationLanguageName);
        Assert.Equal("New World Translation", action.BiblePublicationName);
    }

    [Fact]
    public void PreserveBiblePublicationDisplayNames_copies_modal_counts_when_action_missing()
    {
        var action = new ScheduleStateItem();
        var existing = new ScheduleStateItem
        {
            BiblePublicationModalItemCount = 3,
            BiblePublicationSectionModalItemCount = 7,
            BiblePublicationTrackModalItemCount = 11,
        };

        DisplayNamePreservationHelper.PreserveBiblePublicationDisplayNames(action, existing);

        Assert.Equal(3, action.BiblePublicationModalItemCount);
        Assert.Equal(7, action.BiblePublicationSectionModalItemCount);
        Assert.Equal(11, action.BiblePublicationTrackModalItemCount);
    }

    [Fact]
    public void PreserveBiblePublicationDisplayNames_preserves_section_name_when_structure_and_code_match()
    {
        var action = new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationSectionCode = "40",
            BiblePublicationSectionName = "",
        };
        var existing = new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.BiblePublicationCodeNwt,
            BiblePublicationSectionCode = "40",
            BiblePublicationSectionName = "Matthew",
        };

        DisplayNamePreservationHelper.PreserveBiblePublicationDisplayNames(action, existing);

        Assert.Equal("Matthew", action.BiblePublicationSectionName);
    }

    [Fact]
    public void PreserveBiblePublicationDisplayNames_preserves_track_title_when_flat_pub_and_track_code_matches()
    {
        var action = new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.MediatorPublicationCodeVODBibleTeachings,
            BiblePublicationTrackCode = "12",
            BiblePublicationTrackTitle = "",
        };
        var existing = new ScheduleStateItem
        {
            BiblePublicationCode = AppConstants.Media.MediatorPublicationCodeVODBibleTeachings,
            BiblePublicationTrackCode = "12",
            BiblePublicationTrackTitle = "Drama Title",
        };

        DisplayNamePreservationHelper.PreserveBiblePublicationDisplayNames(action, existing);

        Assert.Equal("Drama Title", action.BiblePublicationTrackTitle);
    }

    [Fact]
    public void PreserveMusicDisplayNames_copies_strings_track_and_modal_counts()
    {
        var action = new ScheduleStateItem
        {
            MusicLanguageName = "",
            MusicLanguageDirection = "",
            MusicPublicationName = "",
            MusicTrackName = "",
        };
        var existing = new ScheduleStateItem
        {
            MusicLanguageName = "English",
            MusicLanguageDirection = "ltr",
            MusicPublicationName = "Sing Out Joyfully",
            MusicTrackName = "Song 1",
            MusicPublicationModalItemCount = 2,
            MusicSectionModalItemCount = 4,
        };

        DisplayNamePreservationHelper.PreserveMusicDisplayNames(action, existing);

        Assert.Equal("English", action.MusicLanguageName);
        Assert.Equal("ltr", action.MusicLanguageDirection);
        Assert.Equal("Sing Out Joyfully", action.MusicPublicationName);
        Assert.Equal("Song 1", action.MusicTrackName);
        Assert.Equal(2, action.MusicPublicationModalItemCount);
        Assert.Equal(4, action.MusicSectionModalItemCount);
    }

    [Fact]
    public void PreserveMusicDisplayNames_preserves_section_name_for_sectioned_music_publication()
    {
        var action = new ScheduleStateItem
        {
            MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            MusicSectionName = "",
        };
        var existing = new ScheduleStateItem
        {
            MusicPublicationCode = AppConstants.Media.MelodyMusicPublicationCodeIam,
            MusicSectionName = "Disc 1",
        };

        DisplayNamePreservationHelper.PreserveMusicDisplayNames(action, existing);

        Assert.Equal("Disc 1", action.MusicSectionName);
    }

    [Fact]
    public void PreserveDisplayNamesFromExisting_invokes_bible_and_music_preservation()
    {
        var action = new ScheduleStateItem
        {
            BiblePublicationLanguageName = "",
            MusicTrackName = "",
        };
        var existing = new ScheduleStateItem
        {
            BiblePublicationLanguageName = "Español",
            MusicTrackName = "Track",
        };

        DisplayNamePreservationHelper.PreserveDisplayNamesFromExisting(action, existing);

        Assert.Equal("Español", action.BiblePublicationLanguageName);
        Assert.Equal("Track", action.MusicTrackName);
    }
}
