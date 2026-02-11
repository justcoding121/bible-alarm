#nullable enable

using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using AndroidX.Car.App.Model;
using Serilog;
using Object = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers.RowCallbacks;

/// <summary>
/// Click callback for schedule items in the Car App list.
/// </summary>
internal class ScheduleRowClickCallback(MainCarScreen screen, int scheduleId) : Object, IOnClickListener
{
    private static readonly ILogger logger = Log.ForContext<ScheduleRowClickCallback>();

    public void OnClick()
    {
        try
        {
            logger.Information("Schedule {ScheduleId} clicked - starting playback", scheduleId);
            screen.OnScheduleItemClicked(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling schedule click for schedule {ScheduleId}", scheduleId);
        }
    }
}
