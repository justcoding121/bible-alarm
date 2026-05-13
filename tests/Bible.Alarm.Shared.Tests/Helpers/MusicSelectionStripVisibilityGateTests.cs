#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests.Helpers;

public sealed class MusicSelectionStripVisibilityGateTests
{
    [Fact]
    public void ShouldShowMusicSelectionRow_true_for_plain_publication_not_flagged_as_music()
    {
        var visible = MusicSelectionStripVisibilityGate.ShouldShowMusicSelectionRow(
            biblePublicationIsMusic: false,
            biblePublicationCode: AppConstants.Media.BiblePublicationCodeNwt);

        Assert.True(visible);
    }

    [Fact]
    public void ShouldShowMusicSelectionRow_false_when_publication_is_marked_music()
    {
        var visible = MusicSelectionStripVisibilityGate.ShouldShowMusicSelectionRow(
            biblePublicationIsMusic: true,
            biblePublicationCode: AppConstants.Media.BiblePublicationCodeNwt);

        Assert.False(visible);
    }

    [Fact]
    public void ShouldShowMusicSelectionRow_false_when_code_matches_music_flag_list()
    {
        var visible = MusicSelectionStripVisibilityGate.ShouldShowMusicSelectionRow(
            biblePublicationIsMusic: false,
            biblePublicationCode: AppConstants.Media.MediatorCategoryKeyChildrenSongs);

        Assert.False(visible);
    }
}
