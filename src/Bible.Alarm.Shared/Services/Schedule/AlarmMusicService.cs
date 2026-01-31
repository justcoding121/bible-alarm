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
/// Service for interacting with AlarmMusic database operations.
/// Abstracts database access from other services.
/// </summary>
public sealed class AlarmMusicService(IServiceScopeFactory scopeFactory, ILogger logger) : IAlarmMusicService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<List<AlarmMusic>> GetAllMusicAsync(CancellationToken cancellationToken = default) => await GetMusicAsync(null, cancellationToken);

    public async Task<List<AlarmMusic>> GetMusicAsync(Expression<Func<AlarmMusic, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var query = dbContext.AlarmMusic.AsQueryable();

        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<AlarmMusic?> GetMusicByIdAsync(int musicId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.AlarmMusic
            .FirstOrDefaultAsync(x => x.Id == musicId, cancellationToken);
    }

    public async Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.AlarmMusic
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AlarmScheduleId == scheduleId, cancellationToken);
    }

    public async Task<AlarmMusic> AddMusicAsync(AlarmMusic music, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        await dbContext.AlarmMusic.AddAsync(music, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Avoid an extra DB roundtrip: return the inserted model.
        return music;
    }

    public async Task<AlarmMusic> UpdateMusicAsync(AlarmMusic music, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        dbContext.AlarmMusic.Update(music);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Avoid an extra DB roundtrip: return the updated model.
        return music;
    }

    public async Task DeleteMusicAsync(int musicId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var music = await dbContext.AlarmMusic.FindAsync([musicId], cancellationToken);
        if (music != null)
        {
            dbContext.AlarmMusic.Remove(music);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> MusicExistsAsync(int musicId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.AlarmMusic.AnyAsync(x => x.Id == musicId, cancellationToken);
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
            logger.Warning(ex, "Error during cancellation token source disposal in AlarmMusicService");
        }

        // IServiceScopeFactory is a singleton, so don't dispose it
    }
}

