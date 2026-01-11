#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Services.Schedule.Interfaces;

/// <summary>
/// Service for interacting with BiblePublicationSchedule database operations.
/// Abstracts database access from other services.
/// </summary>
public interface IBiblePublicationScheduleService : IDisposable
{
    Task<List<BiblePublicationSchedule>> GetAllBiblePublicationSchedulesAsync(CancellationToken cancellationToken = default);
    Task<List<BiblePublicationSchedule>> GetBiblePublicationSchedulesAsync(Expression<Func<BiblePublicationSchedule, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<BiblePublicationSchedule?> GetBiblePublicationScheduleByIdAsync(int biblePublicationScheduleId, CancellationToken cancellationToken = default);
    Task<BiblePublicationSchedule?> GetBiblePublicationScheduleByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<BiblePublicationSchedule> AddBiblePublicationScheduleAsync(BiblePublicationSchedule biblePublicationSchedule, CancellationToken cancellationToken = default);
    Task<BiblePublicationSchedule> UpdateBiblePublicationScheduleAsync(BiblePublicationSchedule biblePublicationSchedule, CancellationToken cancellationToken = default);
    Task DeleteBiblePublicationScheduleAsync(int biblePublicationScheduleId, CancellationToken cancellationToken = default);
    Task<bool> BiblePublicationScheduleExistsAsync(int biblePublicationScheduleId, CancellationToken cancellationToken = default);
}

