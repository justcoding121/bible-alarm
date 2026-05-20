#nullable enable

using Bible.Alarm.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests.Support;

/// <summary>
/// Minimal factory that hands out scoped <see cref="ScheduleDbContext"/> instances backed by one open SQLite connection.
/// </summary>
internal sealed class ScheduleTestScopeFactory(DbContextOptions<ScheduleDbContext> options) : IServiceScopeFactory
{
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
