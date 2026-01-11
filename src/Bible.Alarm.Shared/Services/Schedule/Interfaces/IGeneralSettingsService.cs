#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Services.Schedule.Interfaces;

/// <summary>
/// Service for interacting with GeneralSettings database operations.
/// Abstracts database access from other services.
/// </summary>
public interface IGeneralSettingsService : IDisposable
{
    Task<GeneralSettings?> GetGeneralSettingAsync(string key, CancellationToken cancellationToken = default);
    Task SetGeneralSettingAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<bool> GeneralSettingExistsAsync(string key, CancellationToken cancellationToken = default);
}

