#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Models.Schedule;

namespace Bible.Alarm.Shared.Services.Interfaces;

/// <summary>
/// Service for interacting with BibleReadingSchedule database operations.
/// Abstracts database access from other services.
/// </summary>
public interface IBibleReadingScheduleService : IDisposable
{
    Task<List<BibleReadingSchedule>> GetAllBibleReadingSchedulesAsync(CancellationToken cancellationToken = default);
    Task<List<BibleReadingSchedule>> GetBibleReadingSchedulesAsync(Expression<Func<BibleReadingSchedule, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<BibleReadingSchedule?> GetBibleReadingScheduleByIdAsync(int bibleReadingScheduleId, CancellationToken cancellationToken = default);
    Task<BibleReadingSchedule?> GetBibleReadingScheduleByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<BibleReadingSchedule> AddBibleReadingScheduleAsync(BibleReadingSchedule bibleReadingSchedule, CancellationToken cancellationToken = default);
    Task<BibleReadingSchedule> UpdateBibleReadingScheduleAsync(BibleReadingSchedule bibleReadingSchedule, CancellationToken cancellationToken = default);
    Task DeleteBibleReadingScheduleAsync(int bibleReadingScheduleId, CancellationToken cancellationToken = default);
    Task<bool> BibleReadingScheduleExistsAsync(int bibleReadingScheduleId, CancellationToken cancellationToken = default);
}

