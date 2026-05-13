#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediatorVideoPublicationCategoryKeyMapperTests
{
    [Fact]
    public void TryGetCategoryKey_returns_false_for_unmapped_publication_code()
    {
        Assert.False(MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey("not-a-mediator-video", out _));
    }

    [Fact]
    public void TryGetCategoryKey_returns_dramas_good_news_mediator_key()
    {
        Assert.True(MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            out var key));

        Assert.Equal(AppConstants.Media.BiblePublicationCodeDramasGoodNews, key);
    }

    [Fact]
    public void TryGetCategoryKey_accepts_upper_cased_normalized_code()
    {
        Assert.True(MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews.ToUpperInvariant(),
            out var key));

        Assert.Equal(AppConstants.Media.BiblePublicationCodeDramasGoodNews, key);
    }

    [Fact]
    public void TryGetCategoryKey_throws_when_code_is_whitespace()
    {
        Assert.Throws<ArgumentException>(() => MediatorVideoPublicationCategoryKeyMapper.TryGetCategoryKey(" ", out _));
    }
}
