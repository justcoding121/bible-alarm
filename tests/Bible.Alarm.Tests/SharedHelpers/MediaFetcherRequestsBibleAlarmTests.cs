#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Media.Helpers.SectionFetcherHelpers;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class BuildMediatorPublicationRequestBibleAlarmTests
{
    [Fact]
    public async Task Record_holds_all_fields_and_deconstructs()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var language = new Language { LanguageCode = "W35-MED", Direction = AppConstants.Media.TextDirectionLeftToRight };
            List<BiblePublicationTrack> tracks = [];
            using var cts = new CancellationTokenSource();

            var sut = new BuildMediatorPublicationRequest(db, "W35-PUB", "Display", language, tracks, cts.Token);

            Assert.Same(db, sut.Db);
            Assert.Equal("W35-PUB", sut.PublicationCodeForDb);
            Assert.Equal("Display", sut.PublicationName);
            Assert.Same(language, sut.Language);
            Assert.Same(tracks, sut.Tracks);
            Assert.Equal(cts.Token, sut.CancellationToken);

            sut.Deconstruct(out var dbOut, out var code, out var name, out var lang, out var trackList, out var ct);
            Assert.Same(db, dbOut);
            Assert.Equal("W35-PUB", code);
            Assert.Equal("Display", name);
            Assert.Same(language, lang);
            Assert.Same(tracks, trackList);
            Assert.Equal(cts.Token, ct);
        });
    }
}

public sealed class SaveSectionTracksPersistenceRequestBibleAlarmTests
{
    [Fact]
    public async Task Record_holds_section_identity_and_token()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var publication = new BiblePublication { PublicationCode = "W35-SSTP", Name = "Pub" };
            var section = new BiblePublicationSection { SectionCode = "sec-1", Name = "Sec", BiblePublication = publication };
            using var cts = new CancellationTokenSource();

            var sut = new SaveSectionTracksPersistenceRequest(db, section, "sec-1", "W35-SSTP", "E", cts.Token);

            Assert.Same(db, sut.Db);
            Assert.Same(section, sut.Section);
            Assert.Equal("sec-1", sut.SectionCode);
            Assert.Equal("W35-SSTP", sut.PublicationCode);
            Assert.Equal("E", sut.LanguageCode);
            Assert.Equal(cts.Token, sut.CancellationToken);
        });
    }
}

public sealed class FetchEnglishSectionsRequestBibleAlarmTests
{
    [Fact]
    public async Task Record_holds_fetch_parameters()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            List<string> codes = ["gen"];
            using var cts = new CancellationTokenSource();

            var sut = new FetchEnglishSectionsRequest(db, "NWT", "E", "Bible", codes, true, cts.Token);

            Assert.Same(db, sut.Db);
            Assert.Equal("NWT", sut.NormalizedPublicationCode);
            Assert.Equal("E", sut.NormalizedLanguageCode);
            Assert.Equal("Bible", sut.CategoryName);
            Assert.Same(codes, sut.SectionCodes);
            Assert.True(sut.IsVideo);
            Assert.Equal(cts.Token, sut.CancellationToken);
        });
    }
}

public sealed class MediatorTrackParseContextBibleAlarmTests
{
    [Fact]
    public void Record_supports_defaults_and_with_expressions()
    {
        var baseline = new MediatorTrackParseContext("E", "40");
        var video = baseline with
        {
            IsVideo = true,
            TrackNumber = 2,
            AllowAudioDescriptionTitles = true,
            OmitTrackFromUrlParams = true,
            UseDocidParam = true,
        };

        Assert.Equal("E", baseline.NormalizedLanguageCode);
        Assert.Equal("40", baseline.SectionCode);
        Assert.False(baseline.IsVideo);
        Assert.False(baseline.AllowAudioDescriptionTitles);
        Assert.False(baseline.OmitTrackFromUrlParams);
        Assert.True(video.IsVideo);
        Assert.Equal(2, video.TrackNumber);
        Assert.True(video.AllowAudioDescriptionTitles);
        Assert.True(video.OmitTrackFromUrlParams);
        Assert.True(video.UseDocidParam);
    }
}

public sealed class FetchEnglishPublicationTracksRequestBibleAlarmTests
{
    [Fact]
    public async Task Record_holds_language_category_and_flags()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var language = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            var category = new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryMusic };
            using var cts = new CancellationTokenSource();

            var sut = new FetchEnglishPublicationTracksRequest(
                db, "NWT", "E", language, category, "Music", false, cts.Token);

            Assert.Same(db, sut.Db);
            Assert.Equal("NWT", sut.NormalizedPublicationCode);
            Assert.Equal("E", sut.NormalizedLanguageCode);
            Assert.Same(language, sut.Language);
            Assert.Same(category, sut.Category);
            Assert.Equal("Music", sut.CategoryName);
            Assert.False(sut.IsVideo);
            Assert.Equal(cts.Token, sut.CancellationToken);

            var equal = new FetchEnglishPublicationTracksRequest(
                db, "NWT", "E", language, category, "Music", false, cts.Token);
            Assert.Equal(sut, equal);
        });
    }
}

public sealed class FetchEnglishPublicationSectionsRequestBibleAlarmTests
{
    [Fact]
    public async Task Record_holds_optional_language_and_section_codes()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var language = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };
            List<string> codes = ["mat"];
            using var cts = new CancellationTokenSource();

            var sut = new FetchEnglishPublicationSectionsRequest(
                db, "NWT", "E", language, "Bible", false, codes, cts.Token);

            Assert.Same(db, sut.Db);
            Assert.Equal("NWT", sut.NormalizedPublicationCode);
            Assert.Equal("E", sut.NormalizedLanguageCode);
            Assert.Same(language, sut.Language);
            Assert.Equal("Bible", sut.CategoryName);
            Assert.False(sut.IsVideo);
            Assert.Same(codes, sut.SectionCodes);
            Assert.Equal(cts.Token, sut.CancellationToken);

            var equal = new FetchEnglishPublicationSectionsRequest(
                db, "NWT", "E", language, "Bible", false, codes, cts.Token);
            Assert.Equal(sut, equal);

            var withoutLanguage = new FetchEnglishPublicationSectionsRequest(
                db, "nwt", "E", null, "Bible", true, codes, CancellationToken.None);
            Assert.Null(withoutLanguage.Language);
            Assert.True(withoutLanguage.IsVideo);
        });
    }
}

public sealed class FetchPublicationSectionsRequestBibleAlarmTests
{
    private sealed class NoOpProgress(CancellationToken token) : IFetchProgress
    {
        public CancellationToken CancellationToken => token;
        public void UpdateProgress(double progress) { }
        public void UpdateProgressText(string text) { }
        public void SetIsVisible(bool isVisible) { }
    }

    [Fact]
    public async Task Init_sets_required_fields_and_optional_progress()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var publication = new BiblePublication { PublicationCode = "W35-FPSR", Name = "Pub" };
            List<string> codes = ["a"];
            using var cts = new CancellationTokenSource();
            IFetchProgress progress = new NoOpProgress(cts.Token);

            var sut = new FetchPublicationSectionsRequest
            {
                Db = db,
                NormalizedPublicationCode = "nwt",
                NormalizedLanguageCode = "e",
                PublicationCodeForDb = "W35-FPSR",
                EnglishPublication = publication,
                SectionCodes = codes,
                CancellationToken = cts.Token,
                Progress = progress,
            };

            Assert.Same(db, sut.Db);
            Assert.Same(publication, sut.EnglishPublication);
            Assert.Same(codes, sut.SectionCodes);
            Assert.Same(progress, sut.Progress);
        });
    }
}

public sealed class BuildEnglishPublicationRequestBibleAlarmTests
{
    [Fact]
    public async Task Record_holds_builder_flags_and_sections()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            List<BiblePublicationSection> sections = [];
            using var cts = new CancellationTokenSource();

            var sut = new BuildEnglishPublicationRequest(
                db, "nwt", "Name", null, false, true, false, sections, cts.Token);

            Assert.Same(db, sut.Db);
            Assert.Equal("nwt", sut.NormalizedPublicationCode);
            Assert.Equal("Name", sut.PublicationName);
            Assert.Null(sut.Language);
            Assert.False(sut.IsVideo);
            Assert.True(sut.IsBible);
            Assert.False(sut.PublicationWithoutLanguage);
            Assert.Same(sections, sut.Sections);
            Assert.Equal(cts.Token, sut.CancellationToken);

            sut.Deconstruct(out var dbOut, out var npc, out var pname, out var lang, out var isVideo,
                out var isBible, out var noLang, out var secs, out var ct);
            Assert.Same(db, dbOut);
            Assert.Equal("nwt", npc);
            Assert.Equal("Name", pname);
            Assert.Null(lang);
            Assert.False(isVideo);
            Assert.True(isBible);
            Assert.False(noLang);
            Assert.Same(sections, secs);
            Assert.Equal(cts.Token, ct);
        });
    }
}

public sealed class FetchSectionTracksRequestBibleAlarmTests
{
    [Fact]
    public async Task Init_holds_publication_section_and_replace_flag()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var publication = new BiblePublication { PublicationCode = "W35-FSTR", Name = "Pub" };
            var section = new BiblePublicationSection { SectionCode = "s1", Name = "S", BiblePublication = publication };
            using var cts = new CancellationTokenSource();

            var sut = new FetchSectionTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "nwt",
                NormalizedSectionCode = "s1",
                NormalizedLanguageCode = "e",
                PublicationCodeForDb = "W35-FSTR",
                Publication = publication,
                Section = section,
                CancellationToken = cts.Token,
                ReplaceExisting = true,
            };

            Assert.True(sut.ReplaceExisting);
            Assert.Same(section, sut.Section);
        });
    }
}

public sealed class FetchFlatPublicationTracksRequestBibleAlarmTests
{
    [Fact]
    public async Task Init_covers_format_flags_and_optional_language()
    {
        await MediaFetcherRequestTestDb.RunAsync(async db =>
        {
            var publication = new BiblePublication { PublicationCode = "W35-FLAT", Name = "Flat" };
            var language = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight };

            var sut = new FetchFlatPublicationTracksRequest
            {
                Db = db,
                NormalizedPublicationCode = "flat",
                NormalizedLanguageCode = "E",
                EnglishPublication = publication,
                IsVideo = true,
                IsMusic = false,
                FileFormat = "MP4",
                Language = language,
                CancellationToken = CancellationToken.None,
            };

            Assert.Equal("MP4", sut.FileFormat);
            Assert.True(sut.IsVideo);
            Assert.Same(language, sut.Language);
        });
    }
}

file static class MediaFetcherRequestTestDb
{
    public static async Task RunAsync(Func<MediaDbContext, Task> body)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MediaDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await body(db);
    }
}
