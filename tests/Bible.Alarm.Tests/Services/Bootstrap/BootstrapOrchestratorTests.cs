#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Bootstrap;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

[CollectionDefinition("BootstrapOrchestrator", DisableParallelization = true)]
public sealed class BootstrapOrchestratorCollection;

[Collection("BootstrapOrchestrator")]
public sealed class BootstrapOrchestratorTests : IDisposable
{
    public BootstrapOrchestratorTests()
    {
        BootstrapOrchestrator.ResetServicesVerifiedForTests();
        BootstrapHelper.ResetBootstrapStateForTests();
    }

    public void Dispose()
    {
        BootstrapOrchestrator.ResetServicesVerifiedForTests();
        BootstrapHelper.ResetBootstrapStateForTests();
    }

    private sealed class RecordingDatabaseBootstrap : IDatabaseBootstrapService
    {
        public int InitializeCalls { get; private set; }

        public Task InitializeAsync()
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFluxorBootstrap : IFluxorBootstrapService
    {
        public int InitializeCalls { get; private set; }

        public Task InitializeAsync()
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingResourceBootstrap : IResourceBootstrapService
    {
        public int CopyCalls { get; private set; }

        public Task CopyResourcesAsync()
        {
            CopyCalls++;
            return Task.CompletedTask;
        }

        public bool WasMediaIndexReplacedThisRun() => false;

        public Task MigrateNonEnglishMediaDataAsync() => Task.CompletedTask;
    }

    private sealed class RecordingScheduleBootstrap : IScheduleBootstrapService
    {
        public int InitializeCalls { get; private set; }

        public Task<bool> SeedAndMigrateAsync() => Task.FromResult(false);

        public Task InitializeAsync()
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, Language>?> LoadLanguagesDictionaryAsync() =>
            Task.FromResult<Dictionary<string, Language>?>(null);

        public Task<List<ScheduleStateItem>> LoadSchedulesListAsync(
            Dictionary<string, Language>? languagesDict,
            Dictionary<string, string>? languageNamesByCode = null) =>
            Task.FromResult(new List<ScheduleStateItem>());
    }

    private sealed class RecordingPlatformBootstrap : IPlatformBootstrapService
    {
        public int InitializeCalls { get; private set; }

        public Task InitializeAsync()
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class IdleLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
    }

    private sealed class IdleCategoryNameService : ICategoryNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) => null;
    }

    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var deps = new BootstrapOrchestratorDeps(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var sut = new BootstrapOrchestrator(deps);

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task VerifyServicesAsync_with_initializeUi_false_invokes_database_bootstrap_once()
    {
        var db = new RecordingDatabaseBootstrap();
        var flux = new RecordingFluxorBootstrap();
        var res = new RecordingResourceBootstrap();
        var sched = new RecordingScheduleBootstrap();
        var plat = new RecordingPlatformBootstrap();
        var lang = new IdleLanguageNameService();
        var cat = new IdleCategoryNameService();

        var sut = new BootstrapOrchestrator(new BootstrapOrchestratorDeps(
            db,
            flux,
            res,
            sched,
            plat,
            lang,
            cat));

        await sut.VerifyServicesAsync(initializeUi: false);

        Assert.Equal(1, db.InitializeCalls);
        Assert.Equal(1, flux.InitializeCalls);
        Assert.Equal(1, res.CopyCalls);
        Assert.Equal(1, sched.InitializeCalls);
        Assert.Equal(1, plat.InitializeCalls);

        await sut.VerifyServicesAsync(initializeUi: false);

        Assert.Equal(1, db.InitializeCalls);
    }

    [Fact]
    public async Task VerifyServicesAsync_initializeUi_true_reloads_schedules_when_already_verified()
    {
        var db = new RecordingDatabaseBootstrap();
        var flux = new RecordingFluxorBootstrap();
        var res = new RecordingResourceBootstrap();
        var sched = new RecordingScheduleBootstrap();
        var plat = new RecordingPlatformBootstrap();

        var sut = new BootstrapOrchestrator(new BootstrapOrchestratorDeps(
            db,
            flux,
            res,
            sched,
            plat,
            new IdleLanguageNameService(),
            new IdleCategoryNameService()));

        await sut.VerifyServicesAsync(initializeUi: false);
        Assert.Equal(1, sched.InitializeCalls);

        await sut.VerifyServicesAsync(initializeUi: true);

        Assert.Equal(2, sched.InitializeCalls);
        Assert.Equal(1, db.InitializeCalls);
    }
}
