#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainer;

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
}
