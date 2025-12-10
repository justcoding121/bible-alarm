#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Shared.Services.Interfaces;

/// <summary>
/// Service for interacting with AlarmMusic database operations.
/// Abstracts database access from other services.
/// </summary>
public interface IAlarmMusicService : IDisposable
{
    Task<List<AlarmMusic>> GetAllMusicAsync(CancellationToken cancellationToken = default);
    Task<List<AlarmMusic>> GetMusicAsync(Expression<Func<AlarmMusic, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<AlarmMusic?> GetMusicByIdAsync(int musicId, CancellationToken cancellationToken = default);
    Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<AlarmMusic> AddMusicAsync(AlarmMusic music, CancellationToken cancellationToken = default);
    Task<AlarmMusic> UpdateMusicAsync(AlarmMusic music, CancellationToken cancellationToken = default);
    Task DeleteMusicAsync(int musicId, CancellationToken cancellationToken = default);
    Task<bool> MusicExistsAsync(int musicId, CancellationToken cancellationToken = default);
}

