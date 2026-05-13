#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MusicCategoryPublicationQueryGateTests
{
    [Fact]
    public void AppliesMusicOnlyFilter_is_false_when_require_flag_disabled_even_for_music_category()
    {
        Assert.False(MusicCategoryPublicationQueryGate.AppliesMusicOnlyFilter(
            AppConstants.Media.BiblePublicationCategoryMusic,
            requireIsMusicForMusicCategory: false));
    }

    [Fact]
    public void AppliesMusicOnlyFilter_is_false_when_category_not_music_even_when_flag_enabled()
    {
        Assert.False(MusicCategoryPublicationQueryGate.AppliesMusicOnlyFilter(
            categoryName: "Other",
            requireIsMusicForMusicCategory: true));
    }

    [Fact]
    public void AppliesMusicOnlyFilter_matches_music_category_case_insensitively_when_flag_enabled()
    {
        Assert.True(MusicCategoryPublicationQueryGate.AppliesMusicOnlyFilter(
            AppConstants.Media.BiblePublicationCategoryMusic.ToUpperInvariant(),
            requireIsMusicForMusicCategory: true));
    }
}
