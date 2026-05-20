#nullable enable

using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Services.Schedule;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Bible.Alarm.Tests;

public sealed class VideoLocalizedNameFetcherBibleAlarmTests
{
    private static VideoLocalizedNameFetcher CreateSut(HttpMessageHandler handler, ILogger? logger = null) =>
        new(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) }, logger ?? TestLogging.CreateLogger());

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private static string CategoryNameJson(string name)
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        return "{\"" + cat + "\":{\"" + nm + "\":\"" + name + "\"}}";
    }

    [Fact]
    public void Constructor_Throws_When_HttpClient_Is_Null()
        => Assert.Throws<ArgumentNullException>(() => new VideoLocalizedNameFetcher(null!, TestLogging.CreateLogger()));

    [Fact]
    public void Constructor_Throws_When_Logger_Is_Null()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(handler);
        Assert.Throws<ArgumentNullException>(() => new VideoLocalizedNameFetcher(http, null!));
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_Code_Is_Not_AMediatorMapped_VideoPublication()
    {
        using var handler = new StubHandler(_ =>
            throw new InvalidOperationException("HTTP must not run for unmapped publication codes."));
        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            normalizedPublicationCode: "not-mapped-to-mediator",
            normalizedLanguageCode: "E");

        Assert.Null(result);
    }

    [Fact]
    public async Task Fetch_returns_null_when_normalized_language_code_is_whitespace_without_calling_http()
    {
        using var handler = new StubHandler(_ =>
            throw new InvalidOperationException("HTTP must not run when language segment is invalid."));
        var sut = CreateSut(handler);

        Assert.Null(await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: " "));
    }

    [Fact]
    public async Task Fetch_Returns_Localized_Name_When_Response_Has_Category_Name()
    {
        var expectedSuffix = $"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/E/"
            + AppConstants.Media.BiblePublicationCodeDramasGoodNews;

        using var handler = new StubHandler(request =>
        {
            Assert.True(
                request.RequestUri?.AbsoluteUri.Contains(expectedSuffix, StringComparison.OrdinalIgnoreCase) == true,
                $"Unexpected request URI: {request.RequestUri}");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CategoryNameJson("Dramas localized title")),
            };
        });

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E");

        Assert.Equal("Dramas localized title", result);
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_All_Http_Attempts_Fail()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E");

        Assert.Null(result);
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_Category_Name_Property_Is_Absent_Or_Empty()
    {
        var catKey = AppConstants.Media.PubMediaJson.Category;

        using var handlerMissingName = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"" + catKey + "\":{}}"),
        });

        var sutMissing = CreateSut(handlerMissingName);

        Assert.Null(await sutMissing.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E"));

        using var handlerEmpty = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CategoryNameJson("")),
        });

        var sutEmpty = CreateSut(handlerEmpty);

        Assert.Null(await sutEmpty.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E"));
    }

    [Fact]
    public async Task Fetch_Returns_Localized_Name_With_Decoded_Html_And_NBSP()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CategoryNameJson("Video&nbsp;Titles&nbsp;A")),
        });

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E");

        Assert.Equal("Video Titles A", result);
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_Http_Handler_Throws_Non_Transient_Exception()
    {
        using var handler = new StubHandler(_ => throw new InvalidOperationException("simulated Transport failure"));

        var sut = CreateSut(handler);

        Assert.Null(await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E"));
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_Response_Is_Not_Valid_Json()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json-at-all"),
        });

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E");

        Assert.Null(result);
    }
}

public sealed class CategoryServiceBibleAlarmTests
{
    private static async Task<(MediaTestScopeFactory Inner, CountingScopeFactory Counting, SqliteConnection Connection)> CreateFactoryAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        var inner = new MediaTestScopeFactory(options);
        return (inner, new CountingScopeFactory(inner), connection);
    }

    private sealed class CountingScopeFactory(MediaTestScopeFactory inner) : IServiceScopeFactory
    {
        private int createScopeCallCount;

        public int CreateScopeCallCount => Volatile.Read(ref createScopeCallCount);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref createScopeCallCount);
            return inner.CreateScope();
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new DivideByZeroException("scope failure");
    }

    private static async Task SeedThreeCategoriesAsync(MediaDbContext db)
    {
        db.Categories.AddRange(
            new Category { CategoryCode = "ZetaCat" },
            new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible },
            new Category { CategoryCode = "AaronCat" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Constructor_Throws_When_ScopeFactory_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new CategoryService(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Is_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CategoryService(new MediaTestScopeFactory(new DbContextOptionsBuilder<MediaDbContext>().Options), null!));
    }

    [Fact]
    public async Task GetAllCategoriesAsync_Orders_Bible_First_Then_CategoryCode_Case_Insensitive()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedThreeCategoriesAsync(seed);
            }

            var sut = new CategoryService(counting, TestLogging.CreateLogger());

            var list = await sut.GetAllCategoriesAsync();

            Assert.Equal(3, list.Count);
            Assert.Equal(AppConstants.Media.BiblePublicationCategoryBible, list[0].CategoryCode, StringComparer.Ordinal);
            Assert.Equal("AaronCat", list[1].CategoryCode, StringComparer.Ordinal);
            Assert.Equal("ZetaCat", list[2].CategoryCode, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task GetAllCategoriesAsync_Hits_Database_Once_Then_Uses_Cache()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedThreeCategoriesAsync(seed);
            }

            var sut = new CategoryService(counting, TestLogging.CreateLogger());

            _ = await sut.GetAllCategoriesAsync();
            Assert.Equal(1, counting.CreateScopeCallCount);

            _ = await sut.GetAllCategoriesAsync();
            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetAllCategoriesAsync_PreCanceledToken_OnFirstLoad_Wraps_InvalidOperationException()
    {
        var (_, _, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                seed.Categories.Add(new Category { CategoryCode = AppConstants.Media.BiblePublicationCategoryBible });
                await seed.SaveChangesAsync();
            }

            var sut = new CategoryService(new MediaTestScopeFactory(opts), TestLogging.CreateLogger());

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetAllCategoriesAsync(canceled.Token));

            Assert.Equal("Error getting all categories.", ex.Message);
            Assert.NotNull(ex.InnerException);
        }
    }

    [Fact]
    public async Task GetAllCategoriesAsync_AfterPrimingCache_Subsequent_Loads_ReturnIndependentCopies_FromInternalCache()
    {
        var (_, counting, connection) = await CreateFactoryAsync();
        await using (connection)
        {
            var opts = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
            await using (var seed = new MediaDbContext(opts))
            {
                await SeedThreeCategoriesAsync(seed);
            }

            var sut = new CategoryService(counting, TestLogging.CreateLogger());

            _ = await sut.GetAllCategoriesAsync();

            var firstFromCache = await sut.GetAllCategoriesAsync();
            var secondFromCache = await sut.GetAllCategoriesAsync();

            Assert.NotSame(firstFromCache, secondFromCache);

            firstFromCache.Clear();

            Assert.Equal(3, secondFromCache.Count);

            var thirdFromCache = await sut.GetAllCategoriesAsync();
            Assert.Equal(3, thirdFromCache.Count);

            Assert.Equal(1, counting.CreateScopeCallCount);
        }
    }

    [Fact]
    public async Task GetAllCategoriesAsync_Wraps_Scope_Failures_In_InvalidOperationException()
    {
        var sut = new CategoryService(new ThrowingScopeFactory(), TestLogging.CreateLogger());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.GetAllCategoriesAsync());

        Assert.Equal("Error getting all categories.", ex.Message);
        Assert.IsType<DivideByZeroException>(ex.InnerException);
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<MediaDbContext>().UseSqlite(connection).Options;
        using (var init = new MediaDbContext(options))
        {
            init.Database.EnsureCreated();
        }

        var counting = new CountingScopeFactory(new MediaTestScopeFactory(options));
        using (connection)
        {
            var sut = new CategoryService(counting, TestLogging.CreateLogger());
            sut.Dispose();
            sut.Dispose();
        }
    }
}

public sealed class GeneralSettingsServiceBibleAlarmTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    private DbContextOptions<ScheduleDbContext> Options =>
        new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseSqlite(connection)
            .Options;

    public Task InitializeAsync()
    {
        connection.Open();
        using var bootstrap = new ScheduleDbContext(Options);
        bootstrap.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    private sealed class ListSink(List<LogEvent> events) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => events.Add(logEvent);
    }

    [Fact]
    public void Constructor_ThrowsWhenScopeFactoryNull()
        => Assert.Throws<ArgumentNullException>(() =>
            new GeneralSettingsService(null!, TestLogging.CreateLogger()));

    [Fact]
    public void Constructor_ThrowsWhenLoggerNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GeneralSettingsService(new ScheduleTestScopeFactory(Options), null!));
    }

    [Fact]
    public async Task SetGetAndExist_Roundtrip()
    {
        using var service = CreateService();

        Assert.False(await service.GeneralSettingExistsAsync("alpha"));

        await service.SetGeneralSettingAsync("alpha", "1");
        Assert.True(await service.GeneralSettingExistsAsync("alpha"));

        var row = await service.GetGeneralSettingAsync("alpha");
        Assert.NotNull(row);
        Assert.Equal("alpha", row!.Key);
        Assert.Equal("1", row.Value);
    }

    [Fact]
    public async Task SetGeneralSetting_UpdatesExistingRow()
    {
        using var service = CreateService();

        await service.SetGeneralSettingAsync("k", "a");
        await service.SetGeneralSettingAsync("k", "b");

        var row = await service.GetGeneralSettingAsync("k");
        Assert.NotNull(row);
        Assert.Equal("b", row!.Value);
    }

    [Fact]
    public async Task GetGeneralSetting_When_KeyMissing_ReturnsNull()
    {
        using var service = CreateService();

        Assert.Null(await service.GetGeneralSettingAsync("__no_such_general_setting__"));
    }

    [Fact]
    public async Task GetGeneralSettingAsync_PreCanceled_PropagatesOperationCanceled()
    {
        using var service = CreateService();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetGeneralSettingAsync("any", canceled.Token));
    }

    [Fact]
    public async Task SetGeneralSettingAsync_PreCanceledOnInsert_PropagatesOperationCanceled()
    {
        using var service = CreateService();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SetGeneralSettingAsync("fresh-key", "v", canceled.Token));
    }

    [Fact]
    public async Task GeneralSettingExistsAsync_PreCanceled_PropagatesOperationCanceled()
    {
        using var service = CreateService();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GeneralSettingExistsAsync("any", canceled.Token));
    }

    [Fact]
    public void Dispose_LogsWarning_When_CancellationTokenCleanupThrows()
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var service = new GeneralSettingsService(new ScheduleTestScopeFactory(Options), logger);

        var field = typeof(GeneralSettingsService).GetField(
            "cancellationTokenSource",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        ((CancellationTokenSource)field!.GetValue(service)!).Dispose();

        Assert.Null(Record.Exception(() => service.Dispose()));

        Assert.Contains(events, log =>
            log.Level == LogEventLevel.Warning
            && log.MessageTemplate.Text.Contains(
                "Error during cancellation token source disposal in GeneralSettingsService",
                StringComparison.Ordinal));

        Assert.Null(Record.Exception(() => service.Dispose()));
    }

    [Fact]
    public void Dispose_IsIdempotent_OnServiceOnly()
    {
        var service = CreateService();
        service.Dispose();
        Assert.Null(Record.Exception(() => service.Dispose()));
    }

    private GeneralSettingsService CreateService() =>
        new(new ScheduleTestScopeFactory(Options), TestLogging.CreateLogger());
}
