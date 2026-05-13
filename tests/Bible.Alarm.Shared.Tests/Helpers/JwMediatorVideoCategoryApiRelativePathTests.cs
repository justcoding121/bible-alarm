#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class JwMediatorVideoCategoryApiRelativePathTests
{
    [Fact]
    public void Compose_joins_prefix_language_and_category_segments()
    {
        var path = JwMediatorVideoCategoryApiRelativePath.Compose("E", AppConstants.Media.BiblePublicationCodeDramasGoodNews);

        Assert.Equal(
            $"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/E/{AppConstants.Media.BiblePublicationCodeDramasGoodNews}",
            path);
    }

    [Fact]
    public void Compose_throws_when_language_code_is_blank()
    {
        Assert.Throws<ArgumentException>(() =>
            JwMediatorVideoCategoryApiRelativePath.Compose("  ", AppConstants.Media.BiblePublicationCodeDramasGoodNews));
    }

    [Fact]
    public void Compose_throws_when_category_key_is_blank()
    {
        Assert.Throws<ArgumentException>(() => JwMediatorVideoCategoryApiRelativePath.Compose("E", " "));
    }
}
