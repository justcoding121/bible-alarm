#nullable enable
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Models.Schedule;

namespace Bible.Alarm.Shared.Services.Schedule.Interfaces;

public interface IAlarmScheduleService : IDisposable
{
    Task<List<AlarmSchedule>> GetAllSchedulesAsync(bool includeMusic = true, bool includeBiblePublication = true, CancellationToken cancellationToken = default);
    Task<List<AlarmSchedule>> GetSchedulesAsync(Expression<Func<AlarmSchedule, bool>>? predicate = null, bool includeMusic = true, bool includeBiblePublication = true, CancellationToken cancellationToken = default);
    Task<AlarmSchedule?> GetScheduleByIdAsync(int scheduleId, bool includeMusic = true, bool includeBiblePublication = true, CancellationToken cancellationToken = default);
    Task<AlarmSchedule?> GetFirstScheduleOrDefaultAsync(bool includeMusic = true, bool includeBiblePublication = true, CancellationToken cancellationToken = default);
    Task<AlarmSchedule> AddScheduleAsync(AlarmSchedule schedule, CancellationToken cancellationToken = default);
    Task<AlarmSchedule> UpdateScheduleAsync(AlarmSchedule schedule, CancellationToken cancellationToken = default);
    Task<AlarmSchedule> UpdateScheduleByIdAsync(int scheduleId, Action<AlarmSchedule> updateAction, CancellationToken cancellationToken = default);
    Task DeleteScheduleAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<bool> ScheduleExistsAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<bool> AnySchedulesExistAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<AlarmMusic?> GetMusicByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<BiblePublicationSchedule?> GetBiblePublicationByScheduleIdAsync(int scheduleId, CancellationToken cancellationToken = default);
}

