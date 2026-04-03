#nullable enable
using Bible.Alarm.Services.Schedule.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Serilog;

namespace Bible.Alarm.Services.Schedule;

public sealed class ScheduleValidationService : IScheduleValidationService
{
    private readonly ILogger logger;
    private readonly IToastService toastService;

    public ScheduleValidationService(ILogger logger, IToastService toastService)
    {
        this.logger = logger;
        this.toastService = toastService;
    }

    public async Task<bool> ValidateDaysOfWeekAsync(DaysOfWeek daysOfWeek)
    {
        if (daysOfWeek != 0)
        {
            return true;
        }

        logger.Warning("Validation failed: No days of week selected");
        await toastService.ShowMessage("Select at least one day");
        return false;
    }
}

