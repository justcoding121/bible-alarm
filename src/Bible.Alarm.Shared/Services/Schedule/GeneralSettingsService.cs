#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Models;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Services.Schedule.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Schedule;

/// <summary>
/// Service for interacting with GeneralSettings database operations.
/// Abstracts database access from other services.
/// </summary>
public sealed class GeneralSettingsService(IServiceScopeFactory scopeFactory, ILogger logger) : IGeneralSettingsService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<GeneralSettings?> GetGeneralSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.GeneralSettings
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
    }

    public async Task SetGeneralSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        var setting = await dbContext.GeneralSettings
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        if (setting == null)
        {
            setting = new GeneralSettings { Key = key };
            await dbContext.GeneralSettings.AddAsync(setting, cancellationToken);
        }

        setting.Value = value;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> GeneralSettingExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        return await dbContext.GeneralSettings.AnyAsync(x => x.Key == key, cancellationToken);
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
            logger.Warning(ex, "Error during cancellation token source disposal in GeneralSettingsService");
        }

        // IServiceScopeFactory is a singleton, so don't dispose it
    }
}

