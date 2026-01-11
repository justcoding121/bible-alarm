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

namespace Bible.Alarm.Shared.Services.Schedule;

/// <summary>
/// Service for interacting with alarm schedule-related database operations.
/// Abstracts database access from other services.
/// </summary>
public sealed class AlarmScheduleService(IServiceScopeFactory scopeFactory) : IAlarmScheduleService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true, bool includeBiblePublication = true, CancellationToken cancellationToken = default) => await GetSchedulesAsync(null, includeMusic, includeBiblePublication, cancellationToken);

    public async Task<List<AlarmSchedule>> GetSchedulesAsync(Expression<Func<AlarmSchedule, bool>>? predicate = null, bool includeMusic = true, bool includeBiblePublication = true, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        // Use AsNoTracking() for read-only queries to improve performance
        var query = dbContext.AlarmSchedules.AsNoTracking().AsQueryable();

        if (includeMusic)
        {
            query = query.Include(x => x.Music);
        }

        if (includeBiblePublication)
        {
            query = query.Include(x => x.BiblePublicationSchedule);
        }

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true, bool includeBiblePublication = true, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        // Apply filter first, then include navigation properties
        // Use AsNoTracking() for read-only queries to improve performance
        var query = dbContext.AlarmSchedules
            .AsNoTracking()
            .Where(x => x.Id == scheduleId);

        if (includeMusic)
        {
            query = query.Include(x => x.Music);
        }

        if (includeBiblePublication)
        {
            query = query.Include(x => x.BiblePublicationSchedule);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true, bool includeBiblePublication = true, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        // Use AsNoTracking() for read-only queries to improve performance
        var query = dbContext.AlarmSchedules.AsNoTracking().AsQueryable();

        if (includeMusic)
        {
            query = query.Include(x => x.Music);
        }

        if (includeBiblePublication)
        {
            query = query.Include(x => x.BiblePublicationSchedule);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        await dbContext.AlarmSchedules.AddAsync(schedule, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetScheduleByIdAsync(schedule.Id, true, true, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to reload schedule {schedule.Id} after adding");
    }

    public async Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        dbContext.AlarmSchedules.Update(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetScheduleByIdAsync(schedule.Id, true, true, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to reload schedule {schedule.Id} after updating");
    }

    public async Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var schedule = await dbContext.AlarmSchedules
            .Include(x => x.Music)
            .Include(x => x.BiblePublicationSchedule)
            .FirstAsync(x => x.Id == scheduleId, cancellationToken);

        updateAction(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Reload with includes to ensure all changes are reflected
        return await GetScheduleByIdAsync(scheduleId, true, true, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to reload schedule {scheduleId} after updating");
    }

    public async Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var schedule = await dbContext.AlarmSchedules.FindAsync([scheduleId], cancellationToken);
        if (schedule != null)
        {
            dbContext.AlarmSchedules.Remove(schedule);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.AlarmSchedules.AnyAsync(x => x.Id == scheduleId, cancellationToken);
    }

    public async Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.AlarmSchedules.AnyAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.AlarmMusic
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AlarmScheduleId == scheduleId, cancellationToken);
    }

    public async Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.BiblePublicationSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AlarmScheduleId == scheduleId, cancellationToken);
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
        catch
        {
            // Ignore errors during disposal
        }

        // IServiceScopeFactory is a singleton, so don't dispose it
    }
}

