#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Schedule;

/// <summary>
/// Service for interacting with BiblePublicationSchedule database operations.
/// Abstracts database access from other services.
/// </summary>
public sealed class BiblePublicationScheduleService(IServiceScopeFactory scopeFactory, ILogger logger) : IBiblePublicationScheduleService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<List<BiblePublicationSchedule>> GetAllBiblePublicationSchedulesAsync(CancellationToken cancellationToken = default) => await GetBiblePublicationSchedulesAsync(null, cancellationToken);

    public async Task<List<BiblePublicationSchedule>> GetBiblePublicationSchedulesAsync(Expression<Func<BiblePublicationSchedule, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var query = dbContext.BiblePublicationSchedules.AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<BiblePublicationSchedule?> GetBiblePublicationScheduleByIdAsync(int biblePublicationScheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.BiblePublicationSchedules
            .FirstOrDefaultAsync(x => x.Id == biblePublicationScheduleId, cancellationToken);
    }

    public async Task<BiblePublicationSchedule?> GetBiblePublicationScheduleByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.BiblePublicationSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AlarmScheduleId == scheduleId, cancellationToken);
    }

    public async Task<BiblePublicationSchedule> AddBiblePublicationScheduleAsync(BiblePublicationSchedule biblePublicationSchedule, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        await dbContext.BiblePublicationSchedules.AddAsync(biblePublicationSchedule, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetBiblePublicationScheduleByIdAsync(biblePublicationSchedule.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to reload bible reading schedule {biblePublicationSchedule.Id} after adding");
    }

    public async Task<BiblePublicationSchedule> UpdateBiblePublicationScheduleAsync(BiblePublicationSchedule biblePublicationSchedule, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        dbContext.BiblePublicationSchedules.Update(biblePublicationSchedule);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetBiblePublicationScheduleByIdAsync(biblePublicationSchedule.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to reload bible reading schedule {biblePublicationSchedule.Id} after updating");
    }

    public async Task DeleteBiblePublicationScheduleAsync(int biblePublicationScheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var biblePublicationSchedule = await dbContext.BiblePublicationSchedules.FindAsync([biblePublicationScheduleId], cancellationToken);
        if (biblePublicationSchedule != null)
        {
            dbContext.BiblePublicationSchedules.Remove(biblePublicationSchedule);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> BiblePublicationScheduleExistsAsync(int biblePublicationScheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.BiblePublicationSchedules.AnyAsync(x => x.Id == biblePublicationScheduleId, cancellationToken);
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during disposal
            logger.Warning(ex, "Error during cancellation token source disposal in BiblePublicationScheduleService");
        }

        // IServiceScopeFactory is a singleton, so don't dispose it
    }
}

