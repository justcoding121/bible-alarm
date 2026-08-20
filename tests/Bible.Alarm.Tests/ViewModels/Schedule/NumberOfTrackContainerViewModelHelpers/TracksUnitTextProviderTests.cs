#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainerViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class TracksUnitTextProviderTests
{
    [Fact]
    public void GetTracksUnit_music_category_returns_track()
    {
        Assert.Equal(TracksUnitTextProvider.TracksUnit.Track,
            TracksUnitTextProvider.GetTracksUnit(AppConstants.Media.BiblePublicationCategoryMusic));
    }

    [Fact]
    public void GetTracksUnit_dramas_category_name_is_case_insensitive()
    {
        Assert.Equal(TracksUnitTextProvider.TracksUnit.Episode,
            TracksUnitTextProvider.GetTracksUnit("dramas"));
    }

    [Fact]
    public void GetTracksUnit_other_category_defaults_to_chapter()
    {
        Assert.Equal(TracksUnitTextProvider.TracksUnit.Chapter,
            TracksUnitTextProvider.GetTracksUnit("Bible"));
    }

    [Fact]
    public void GetUnitTextLowerCase_music_lowercases_track_labels()
    {
        var (singular, plural) = TracksUnitTextProvider.GetUnitTextLowerCase(AppConstants.Media.BiblePublicationCategoryMusic);

        Assert.Equal(AppConstants.Media.PublicationUiTrackSingular.ToLowerInvariant(), singular);
        Assert.Equal(AppConstants.Media.PublicationUiTrackPlural.ToLowerInvariant(), plural);
    }

    [Fact]
    public void GetSelectedTracksText_zero_returns_plural_only()
    {
        var (_, plural) = TracksUnitTextProvider.GetUnitTextTitleCase(AppConstants.Media.BiblePublicationCategoryMusic);

        Assert.Equal(plural, TracksUnitTextProvider.GetSelectedTracksText(AppConstants.Media.BiblePublicationCategoryMusic, 0));
    }

    [Fact]
    public void GetSelectedTracksText_one_uses_singular_unit()
    {
        Assert.Contains(
            AppConstants.Media.PublicationUiEpisodeSingular,
            TracksUnitTextProvider.GetSelectedTracksText(AppConstants.Media.BiblePublicationCategoryDramas, 1));
    }

    [Fact]
    public void GetTrackLabelText_appends_play_each_time_suffix()
    {
        var text = TracksUnitTextProvider.GetTrackLabelText(AppConstants.Media.BiblePublicationCategoryMusic);

        Assert.EndsWith("each time", text, StringComparison.Ordinal);
        Assert.Contains(AppConstants.Media.PublicationUiTrackPlural, text, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTracksLabelText_modal_header_and_restart_use_category_plural()
    {
        var category = AppConstants.Media.BiblePublicationCategoryDramas;

        Assert.Contains("Number of", TracksUnitTextProvider.GetTracksLabelText(category));
        Assert.Contains("Select Number of", TracksUnitTextProvider.GetModalHeaderText(category));
        Assert.Contains("Restart incomplete", TracksUnitTextProvider.GetRestartLabelText(category));
    }

    [Fact]
    public void GetUnitTextTitleCase_chapter_branch_for_non_music_non_drama()
    {
        var (singular, plural) = TracksUnitTextProvider.GetUnitTextTitleCase("Bible");

        Assert.Equal(AppConstants.Media.PublicationUiChapterSingular, singular);
        Assert.Equal(AppConstants.Media.PublicationUiChapterPlural, plural);
    }
}
