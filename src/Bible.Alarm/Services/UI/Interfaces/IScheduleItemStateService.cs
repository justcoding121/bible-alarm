namespace Bible.Alarm.Services.UI.Interfaces;

public interface IScheduleItemStateService
{
    void SetScheduleItemBusyToFalse(int? scheduleId);
    void HideHomePageOverlay();
}
