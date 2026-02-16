using Microsoft.Maui.Platform;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Handlers;

public class EntryHandler : Microsoft.Maui.Handlers.EntryHandler
{
    protected override void ConnectHandler(MauiTextField platformView)
    {
        base.ConnectHandler(platformView);
        platformView.BorderStyle = UITextBorderStyle.None;
    }
}
