using UIKit;

namespace Bible.Alarm.Platforms.iOS.Handlers;

public class TimePickerHandler : Microsoft.Maui.Handlers.TimePickerHandler
{
    protected override void ConnectHandler(Microsoft.Maui.Platform.MauiTimePicker platformView)
    {
        base.ConnectHandler(platformView);
        // Remove native background so the MAUI Border background shows through
        platformView.BackgroundColor = UIColor.Clear;
        platformView.BorderStyle = UITextBorderStyle.None;
    }
}
