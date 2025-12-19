#nullable enable

namespace Bible.Alarm.Services.UI.Interfaces;

public interface IScheduleItemStateService : IDisposable
{
    void SetScheduleItemBusyToFalse(int? scheduleId);
    void HideHomePageOverlay();
}
