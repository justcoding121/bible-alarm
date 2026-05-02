#nullable enable

using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests.Support;

internal sealed class MediaTestScopeFactory(DbContextOptions<MediaDbContext> options) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new Scope(options);

    private sealed class Scope(DbContextOptions<MediaDbContext> options) : IServiceScope, IDisposable
    {
        private readonly MediaDbContext db = new(options);

        public IServiceProvider ServiceProvider => new Provider(db);

        public void Dispose() => db.Dispose();

        private sealed class Provider(MediaDbContext db) : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(MediaDbContext) ? db : null;
        }
    }
}
