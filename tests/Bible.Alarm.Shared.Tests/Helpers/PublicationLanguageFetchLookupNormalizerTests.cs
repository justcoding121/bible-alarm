#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationLanguageFetchLookupNormalizerTests
{
    [Fact]
    public void NormalizeLanguageCodeForPublicationLanguageJoin_trims_and_uppercases_language_code()
    {
        Assert.Equal("EN", PublicationLanguageFetchLookupNormalizer.NormalizeLanguageCodeForPublicationLanguageJoin("  en "));
    }

    [Fact]
    public void NormalizeLanguageCodeForPublicationLanguageJoin_throws_when_language_missing()
    {
        Assert.Throws<ArgumentException>(() =>
            PublicationLanguageFetchLookupNormalizer.NormalizeLanguageCodeForPublicationLanguageJoin("   "));
    }

    [Fact]
    public void ResolvePublicationCodeForDatabaseLookup_returns_canonical_string_when_publication_maps_through_mediator_list()
    {
        var resolved = PublicationLanguageFetchLookupNormalizer.ResolvePublicationCodeForDatabaseLookup(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews);

        Assert.Equal(AppConstants.Media.BiblePublicationCodeDramasGoodNews, resolved);
    }

    [Fact]
    public void ResolvePublicationCodeForDatabaseLookup_returns_original_when_no_canonical_mapping_exists()
    {
        Assert.Equal(
            "custom-pub-code",
            PublicationLanguageFetchLookupNormalizer.ResolvePublicationCodeForDatabaseLookup("custom-pub-code"));
    }
}
