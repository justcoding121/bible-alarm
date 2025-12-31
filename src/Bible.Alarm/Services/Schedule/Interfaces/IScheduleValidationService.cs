#nullable enable
using Bible.Alarm.Shared.Models.Enums;

namespace Bible.Alarm.Services.Schedule.Interfaces;

public interface IScheduleValidationService
{
    Task<bool> ValidateDaysOfWeekAsync(DaysOfWeek daysOfWeek);
}

