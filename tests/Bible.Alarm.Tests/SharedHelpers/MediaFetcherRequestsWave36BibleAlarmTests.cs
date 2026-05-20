#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class SeedEnglishByCatalogTypeRequestBibleAlarmTests
{
    [Fact]
    public async Task Record_holds_catalog_branch_fields_and_deconstructs()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var language = new Language { LanguageCode = "W36-SEED", Direction = AppConstants.Media.TextDirectionLeftToRight };
            var category = new Category { CategoryCode = "W36-CAT" };
            using var cts = new CancellationTokenSource();

            var sut = new SeedEnglishByCatalogTypeRequest(
                db,
                CatalogType.Flat,
                "PUB",
                "PUB-DB",
                "pub-norm",
                "E",
                language,
                category,
                "Music",
                true,
                cts.Token);

            Assert.Same(db, sut.Db);
            Assert.Equal(CatalogType.Flat, sut.CatalogType);
            Assert.Equal("PUB", sut.PublicationCode);
            Assert.Equal("PUB-DB", sut.PublicationCodeForDb);
            Assert.Equal("pub-norm", sut.NormalizedPublicationCode);
            Assert.Equal("E", sut.NormalizedLanguageCode);
            Assert.Same(language, sut.Language);
            Assert.Same(category, sut.Category);
            Assert.Equal("Music", sut.CategoryName);
            Assert.True(sut.IsVideo);
            Assert.Equal(cts.Token, sut.CancellationToken);

            sut.Deconstruct(
                out var dbOut,
                out var catalog,
                out var pub,
                out var pubDb,
                out var normPub,
                out var normLang,
                out var langOut,
                out var catOut,
                out var catName,
                out var isVideo,
                out var ct);
            Assert.Same(db, dbOut);
            Assert.Equal(CatalogType.Flat, catalog);
            Assert.Equal(cts.Token, ct);
            Assert.True(isVideo);
            Assert.Equal("Music", catName);
            Assert.Same(catOut, category);
            Assert.Same(langOut, language);
            Assert.Equal("E", normLang);
            Assert.Equal("pub-norm", normPub);
            Assert.Equal("PUB-DB", pubDb);
            Assert.Equal("PUB", pub);
        });
    }
}

public sealed class EnglishPublicationPersistRequestsBibleAlarmTests
{
    [Fact]
    public async Task Update_request_carries_publication_collections_and_flags()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var existing = new BiblePublication { PublicationCode = "W36-UPD", Name = "Existing" };
            List<Category> categories = [];
            List<BiblePublicationSection> sections = [];
            using var cts = new CancellationTokenSource();

            var sut = new EnglishPublicationUpdateRequest
            {
                Db = db,
                ExistingPublication = existing,
                Categories = categories,
                Sections = sections,
                FinalPublicationName = "Updated",
                IsVideo = true,
                IsBible = false,
                PublicationWithoutLanguage = true,
                NormalizedPublicationCode = "w36-upd",
                CancellationToken = cts.Token,
            };

            Assert.Same(db, sut.Db);
            Assert.Same(existing, sut.ExistingPublication);
            Assert.Same(categories, sut.Categories);
            Assert.Same(sections, sut.Sections);
            Assert.Equal("Updated", sut.FinalPublicationName);
            Assert.True(sut.IsVideo);
            Assert.False(sut.IsBible);
            Assert.True(sut.PublicationWithoutLanguage);
            Assert.Equal("w36-upd", sut.NormalizedPublicationCode);
            Assert.Equal(cts.Token, sut.CancellationToken);
        });
    }

    [Fact]
    public async Task Insert_request_supports_null_or_resolved_language()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            List<Category> categories = [];
            List<BiblePublicationSection> sections = [];
            var language = new Language { LanguageCode = "W36-INS", Direction = AppConstants.Media.TextDirectionLeftToRight };

            var withoutLanguage = new EnglishPublicationInsertRequest
            {
                Db = db,
                Categories = categories,
                Sections = sections,
                NormalizedPublicationCode = "ins-none",
                FinalPublicationName = "No lang",
                Language = null,
                LanguageId = null,
                IsVideo = false,
                IsBible = true,
                PublicationWithoutLanguage = true,
                CancellationToken = CancellationToken.None,
            };

            Assert.Null(withoutLanguage.Language);
            Assert.Null(withoutLanguage.LanguageId);
            Assert.True(withoutLanguage.PublicationWithoutLanguage);
            Assert.True(withoutLanguage.IsBible);

            var withLanguage = new EnglishPublicationInsertRequest
            {
                Db = db,
                Categories = categories,
                Sections = sections,
                NormalizedPublicationCode = "ins-lang",
                FinalPublicationName = "With lang",
                Language = language,
                LanguageId = 42,
                IsVideo = true,
                IsBible = false,
                PublicationWithoutLanguage = false,
                CancellationToken = CancellationToken.None,
            };

            Assert.Same(language, withLanguage.Language);
            Assert.Equal(42, withLanguage.LanguageId);
            Assert.False(withLanguage.PublicationWithoutLanguage);
            Assert.True(withLanguage.IsVideo);
        });
    }
}
