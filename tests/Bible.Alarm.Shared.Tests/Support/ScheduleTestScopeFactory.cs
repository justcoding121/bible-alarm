#nullable enable

using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Shared.Tests.Support;

/// <summary>
/// Minimal factory that hands out scoped <see cref="ScheduleDbContext"/> instances backed by one open SQLite connection.
/// </summary>
internal sealed class ScheduleTestScopeFactory : IServiceScopeFactory
{
    private readonly DbContextOptions<ScheduleDbContext> options;

    public ScheduleTestScopeFactory(DbContextOptions<ScheduleDbContext> options)
    {
        this.options = options;
    }

    public IServiceScope CreateScope() => new Scope(options);

    private sealed class Scope(DbContextOptions<ScheduleDbContext> options) : IServiceScope, IDisposable
    {
        private readonly ScheduleDbContext db = new(options);

        public IServiceProvider ServiceProvider => new Provider(db);

        public void Dispose() => db.Dispose();

        private sealed class Provider(ScheduleDbContext db) : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(ScheduleDbContext) ? db : null;
        }
    }
}
