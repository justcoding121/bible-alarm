#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Services.Schedule.Interfaces;

public interface IGeneralSettingsService : IDisposable
{
    Task<GeneralSettings?> GetGeneralSettingAsync(string key, CancellationToken cancellationToken = default);
    Task SetGeneralSettingAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<bool> GeneralSettingExistsAsync(string key, CancellationToken cancellationToken = default);
}

