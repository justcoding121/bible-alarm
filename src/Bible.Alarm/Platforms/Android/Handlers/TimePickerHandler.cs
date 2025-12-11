using Android.Content.Res;
using Microsoft.Maui.Platform;

namespace Bible.Alarm.Platforms.Android.Handlers;

public class TimePickerHandler : Microsoft.Maui.Handlers.TimePickerHandler
{
    protected override MauiTimePicker CreatePlatformView()
    {
        var view = base.CreatePlatformView();
        // Remove the underline on Android
        view.BackgroundTintList = ColorStateList.ValueOf(global::Android.Graphics.Color.Transparent);
        return view;
    }

    protected override void ConnectHandler(MauiTimePicker platformView)
    {
        base.ConnectHandler(platformView);
        // Ensure underline is removed
        platformView.BackgroundTintList = ColorStateList.ValueOf(global::Android.Graphics.Color.Transparent);
    }
}

