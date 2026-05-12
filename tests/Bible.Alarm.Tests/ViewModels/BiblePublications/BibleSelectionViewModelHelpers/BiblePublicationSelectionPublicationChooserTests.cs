#nullable enable

using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionPublicationChooserTests
{
    private static LanguageListViewItemModel Lang(string code) =>
        new(new Language { LanguageCode = code, Direction = "ltr" }, displayName: code);

    [Fact]
    public async Task ChooseAsync_returns_empty_tuple_when_publications_empty()
    {
        var resolver = new BiblePublicationSelectionSectionTrackResolver(
            new IdleCatalogMediaService(),
            new MediaTestScopeFactory(
                new DbContextOptionsBuilder<Bible.Alarm.Shared.Database.MediaDbContext>()
                    .UseSqlite("Data Source=:memory:")
                    .Options),
            biblePublicationService: null,
            languageContentService: null);
        var sut = new BiblePublicationSelectionPublicationChooser(null, null, resolver);

        var result = await sut.ChooseAsync([], Lang("E"));

        Assert.Null(result.PublicationCode);
        Assert.Null(result.Publication);
        Assert.False(result.PublicationWithoutLanguage);
    }

    [Fact]
    public async Task ChooseAsync_without_service_returns_first_publication_tuple()
    {
        var resolver = new BiblePublicationSelectionSectionTrackResolver(
            new IdleCatalogMediaService(),
            new MediaTestScopeFactory(
                new DbContextOptionsBuilder<Bible.Alarm.Shared.Database.MediaDbContext>()
                    .UseSqlite("Data Source=:memory:")
                    .Options),
            biblePublicationService: null,
            languageContentService: null);
        var sut = new BiblePublicationSelectionPublicationChooser(null, null, resolver);
        var pub = new BiblePublication { PublicationCode = "nwt", LanguageId = 1 };
        var dict = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase) { ["nwt"] = pub };

        var result = await sut.ChooseAsync(dict, Lang("E"));

        Assert.Equal("nwt", result.PublicationCode);
        Assert.Same(pub, result.Publication);
        Assert.False(result.PublicationWithoutLanguage);
    }

    [Fact]
    public async Task ChooseAsync_selects_publication_without_language_before_hitting_database()
    {
        var resolver = new BiblePublicationSelectionSectionTrackResolver(
            new IdleCatalogMediaService(),
            new MediaTestScopeFactory(
                new DbContextOptionsBuilder<Bible.Alarm.Shared.Database.MediaDbContext>()
                    .UseSqlite("Data Source=:memory:")
                    .Options),
            biblePublicationService: null,
            languageContentService: null);
        var sut = new BiblePublicationSelectionPublicationChooser(new ThrowingBiblePublicationService(), null, resolver);
        var noLangPub = new BiblePublication { PublicationCode = "iam", LanguageId = null };
        var dict = new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase) { ["iam"] = noLangPub };

        var result = await sut.ChooseAsync(dict, Lang("E"));

        Assert.Equal("iam", result.PublicationCode);
        Assert.Same(noLangPub, result.Publication);
        Assert.True(result.PublicationWithoutLanguage);
    }

    private sealed class ThrowingBiblePublicationService : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public void InvalidatePublicationCaches(string languageCode, string publicationCode) =>
            throw new InvalidOperationException("unexpected");

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");

        public Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");

        public Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, bool filterIsMusicWhenMusicCategory = false, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(string languageCode, string categoryCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unexpected");
    }
}
