#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class MediaCacheSetupServiceTests
{
    private sealed class StubMediaCacheService : IMediaCacheService
    {
        public List<int> SetupAlarmCalls { get; } = [];
        public Exception? ThrowFromSetupAlarm;

        public Task<bool> SetupAlarmCacheAsync(int alarmScheduleId)
        {
            SetupAlarmCalls.Add(alarmScheduleId);
            if (ThrowFromSetupAlarm is not null)
            {
                throw ThrowFromSetupAlarm;
            }

            return Task.FromResult(true);
        }

        public Task<bool> ExistsAsync(string lookUpPath, int scheduleId) => Task.FromResult(false);

        public string GetCacheFileName(string lookUpPath) => string.Empty;

        public string GetCacheFilePath(string lookUpPath, int scheduleId) => string.Empty;

        public Task CleanUpAsync() => Task.CompletedTask;

        public Task<string?> ResolveTrackUriAsync(PlayItem playItem, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<bool> CacheTrackAsync(PlayItem playItem, int scheduleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteScheduleCacheAsync(int scheduleId) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class StubScopeFactory : IServiceScopeFactory
    {
        public required StubMediaCacheService Cache { get; init; }

        public IServiceScope CreateScope() => new Scope(Cache);

        private sealed class Scope(StubMediaCacheService cache) : IServiceScope
        {
            private sealed class Provider(StubMediaCacheService cache) : IServiceProvider
            {
                public object? GetService(Type serviceType) =>
                    serviceType == typeof(IMediaCacheService) ? cache : null;
            }

            public IServiceProvider ServiceProvider { get; } = new Provider(cache);

            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public async Task SetupAlarmCacheAsync_forwards_to_media_cache_service()
    {
        var logger = TestLogging.CreateLogger();
        var cache = new StubMediaCacheService();
        var factory = new StubScopeFactory { Cache = cache };
        var sut = new MediaCacheSetupService(logger, factory);

        await sut.SetupAlarmCacheAsync(44);

        Assert.Equal([44], cache.SetupAlarmCalls);
    }

    [Fact]
    public async Task SetupAlarmCacheAsync_swallows_cache_errors()
    {
        var logger = TestLogging.CreateLogger();
        var cache = new StubMediaCacheService { ThrowFromSetupAlarm = new InvalidOperationException("cache failed") };
        var factory = new StubScopeFactory { Cache = cache };
        var sut = new MediaCacheSetupService(logger, factory);

        await sut.SetupAlarmCacheAsync(55);

        Assert.Equal([55], cache.SetupAlarmCalls);
    }
}
