#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Schedule;

/// <summary>
/// Service for interacting with BibleReadingSchedule database operations.
/// Abstracts database access from other services.
/// </summary>
public class BibleReadingScheduleService(IServiceScopeFactory scopeFactory, ILogger logger) : IBibleReadingScheduleService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<List<BibleReadingSchedule>> GetAllBibleReadingSchedulesAsync(CancellationToken cancellationToken = default)
    {
        return await GetBibleReadingSchedulesAsync(null, cancellationToken);
    }

    public async Task<List<BibleReadingSchedule>> GetBibleReadingSchedulesAsync(Expression<Func<BibleReadingSchedule, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var query = dbContext.BibleReadingSchedules.AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<BibleReadingSchedule?> GetBibleReadingScheduleByIdAsync(int bibleReadingScheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.BibleReadingSchedules
            .FirstOrDefaultAsync(x => x.Id == bibleReadingScheduleId, cancellationToken);
    }

    public async Task<BibleReadingSchedule?> GetBibleReadingScheduleByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.BibleReadingSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AlarmScheduleId == scheduleId, cancellationToken);
    }

    public async Task<BibleReadingSchedule> AddBibleReadingScheduleAsync(BibleReadingSchedule bibleReadingSchedule, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        await dbContext.BibleReadingSchedules.AddAsync(bibleReadingSchedule, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetBibleReadingScheduleByIdAsync(bibleReadingSchedule.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to reload bible reading schedule {bibleReadingSchedule.Id} after adding");
    }

    public async Task<BibleReadingSchedule> UpdateBibleReadingScheduleAsync(BibleReadingSchedule bibleReadingSchedule, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        dbContext.BibleReadingSchedules.Update(bibleReadingSchedule);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetBibleReadingScheduleByIdAsync(bibleReadingSchedule.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to reload bible reading schedule {bibleReadingSchedule.Id} after updating");
    }

    public async Task DeleteBibleReadingScheduleAsync(int bibleReadingScheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var bibleReadingSchedule = await dbContext.BibleReadingSchedules.FindAsync(new object[] { bibleReadingScheduleId }, cancellationToken);
        if (bibleReadingSchedule != null)
        {
            dbContext.BibleReadingSchedules.Remove(bibleReadingSchedule);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> BibleReadingScheduleExistsAsync(int bibleReadingScheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.BibleReadingSchedules.AnyAsync(x => x.Id == bibleReadingScheduleId, cancellationToken);
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
            logger.Warning(ex, "Error during cancellation token source disposal in BibleReadingScheduleService");
        }

        // IServiceScopeFactory is a singleton, so don't dispose it
    }
}

