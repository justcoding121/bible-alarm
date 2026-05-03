#nullable enable

using Bible.Alarm.Services.Bootstrap;
using Bible.Alarm.Services.Bootstrap.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class BootstrapOrchestratorDepsTests
{
    private sealed class StubDatabaseBootstrap : IDatabaseBootstrapService
    {
        public Task InitializeAsync() => Task.CompletedTask;
    }

    private sealed class StubFluxBootstrap : IFluxorBootstrapService
    {
        public Task InitializeAsync() => Task.CompletedTask;
    }

    private sealed class StubResourceBootstrap : IResourceBootstrapService
    {
        public Task CopyResourcesAsync() => Task.CompletedTask;

        public bool WasMediaIndexReplacedThisRun() => false;

        public Task MigrateNonEnglishMediaDataAsync() => Task.CompletedTask;
    }

    private sealed class StubScheduleBootstrap : IScheduleBootstrapService
    {
        public Task<bool> SeedAndMigrateAsync() => Task.FromResult(false);

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<Dictionary<string, Language>?> LoadLanguagesDictionaryAsync() =>
            Task.FromResult<Dictionary<string, Language>?>(null);

        public Task<List<ScheduleStateItem>> LoadSchedulesListAsync(Dictionary<string, Language>? languagesDict,
            Dictionary<string, string>? languageNamesByCode = null) =>
            Task.FromResult(new List<ScheduleStateItem>());
    }

    private sealed class StubPlatformBootstrap : IPlatformBootstrapService
    {
        public Task InitializeAsync() => Task.CompletedTask;
    }

    private sealed class StubLanguageNames : ILanguageNameService
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

    private sealed class StubCategoryNames : ICategoryNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) => null;
    }

    private static BootstrapOrchestratorDeps CreateDeps() =>
        new(
            new StubDatabaseBootstrap(),
            new StubFluxBootstrap(),
            new StubResourceBootstrap(),
            new StubScheduleBootstrap(),
            new StubPlatformBootstrap(),
            new StubLanguageNames(),
            new StubCategoryNames());

    [Fact]
    public void Structural_equality_requires_same_dependency_instances()
    {
        var database = new StubDatabaseBootstrap();
        var flux = new StubFluxBootstrap();
        var resources = new StubResourceBootstrap();
        var schedules = new StubScheduleBootstrap();
        var platform = new StubPlatformBootstrap();
        var languages = new StubLanguageNames();
        var categories = new StubCategoryNames();

        var lhs = new BootstrapOrchestratorDeps(database, flux, resources, schedules, platform, languages, categories);
        var rhs = new BootstrapOrchestratorDeps(database, flux, resources, schedules, platform, languages, categories);

        Assert.Equal(lhs, rhs);

        Assert.NotEqual(lhs, lhs with { PlatformBootstrapService = new StubPlatformBootstrap() });
        Assert.NotEqual(lhs, lhs with { FluxorBootstrapService = new StubFluxBootstrap() });
    }

    [Fact]
    public void With_replaces_singleton_component_without_mutating_original()
    {
        var original = CreateDeps();

        var next = original with { CategoryNameService = new StubCategoryNames() };

        Assert.NotSame(original.CategoryNameService, next.CategoryNameService);
        Assert.Same(original.DatabaseBootstrapService, next.DatabaseBootstrapService);
    }
}
