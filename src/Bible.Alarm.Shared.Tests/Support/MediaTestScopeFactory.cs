#nullable enable

using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Shared.Tests.Support;

/// <summary>
/// Minimal factory handing out scoped <see cref="MediaDbContext"/> over one SQLite in-memory connection.
/// </summary>
internal sealed class MediaTestScopeFactory : IServiceScopeFactory
{
    private readonly DbContextOptions<MediaDbContext> options;

    public MediaTestScopeFactory(DbContextOptions<MediaDbContext> options)
    {
        this.options = options;
    }

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
