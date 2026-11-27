#if UITEST
#nullable enable

using AVFoundation;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.iOS.Services.CarPlay;
using Bible.Alarm.Services.UiTest;
using CommunityToolkit.Mvvm.Messaging;
using CoreFoundation;
using Foundation;
using Serilog;

namespace Bible.Alarm.Platforms.iOS.UiTest;

/// <summary>
/// Darwin notification hooks for simulator UI tests. notifyutil cannot carry a payload, so the schedule id is part of the name.
/// Store builds do not compile this type.
/// </summary>
public static class UiTestHooks
{
    public const string Prefix = "com.jthomas.biblealarm.uitest.";

    private static readonly ILogger logger = Log.ForContext(typeof(UiTestHooks));
    private static bool registered;

    public static void Register()
    {
        if (registered)
        {
            return;
        }

        registered = true;
        try
        {
            var center = CFNotificationCenter.GetDarwinNotifyCenter();
            foreach (var command in new[]
            {
                "remote.play", "remote.pause", "remote.next", "remote.previous",
                "interruption.begin", "interruption.end", "route.oldDeviceUnavailable",
                "carplay.connect", "carplay.disconnect", "dumpState"
            })
            {
                Watch(center, command);
            }

            for (var id = 1; id <= 32; id++)
            {
                Watch(center, $"fireAlarm.{id}");
                Watch(center, $"carplay.select.{id}");
            }

            logger.Information("Registered UI test Darwin notification hooks");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to register UI test Darwin notification hooks");
        }
    }

    private static void Watch(CFNotificationCenter center, string command)
    {
        center.AddObserver(Prefix + command, Observe, CFNotificationSuspensionBehavior.DeliverImmediately);
    }

    private static void Observe(string? name, NSDictionary? userInfo)
    {
        _ = userInfo;
        if (string.IsNullOrEmpty(name) || !name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return;
        }

        var command = name[Prefix.Length..];
        try
        {
            Dispatch(command);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "UI test hook {Command} failed", command);
        }
    }

    private static void Dispatch(string command)
    {
        if (command.StartsWith("fireAlarm.", StringComparison.Ordinal) && int.TryParse(command["fireAlarm.".Length..], out var alarmId))
        {
            CarPlayPlaybackHandler.HandleScheduleItemClicked(alarmId, static () => { });
            return;
        }

        if (command.StartsWith("carplay.select.", StringComparison.Ordinal) && int.TryParse(command["carplay.select.".Length..], out var selectedId))
        {
            CarPlayPlaybackHandler.HandleScheduleItemClicked(selectedId, static () => { });
            return;
        }

        switch (command)
        {
            case "remote.play":
                WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                break;
            case "remote.pause":
                WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
                break;
            case "remote.next":
                WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());
                break;
            case "remote.previous":
                WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());
                break;
            case "interruption.begin":
                PostInterruption(AVAudioSessionInterruptionType.Began);
                WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
                break;
            case "interruption.end":
                PostInterruption(AVAudioSessionInterruptionType.Ended);
                WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                break;
            case "route.oldDeviceUnavailable":
                NSNotificationCenter.DefaultCenter.PostNotificationName(AVAudioSession.RouteChangeNotification, AVAudioSession.SharedInstance());
                WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
                break;
            case "carplay.connect":
                var count = CarPlayScheduleHelper.LoadScheduleStateItemsFromState().Count;
                logger.Information("BibleAlarmUiTest carplay-connected schedules={Count}", count);
                break;
            case "carplay.disconnect":
                logger.Information("BibleAlarmUiTest carplay-disconnected");
                break;
            case "dumpState":
                var json = UiTestStateDump.ToJson();
                logger.Information("BibleAlarmUiTest {Json}", json);
                break;
            default:
                logger.Warning("Ignored UI test Darwin notification {Command}", command);
                break;
        }
    }

    private static void PostInterruption(AVAudioSessionInterruptionType type)
    {
        var info = new NSDictionary(AVAudioSession.InterruptionTypeKey, NSNumber.FromInt32((int)type));
        NSNotificationCenter.DefaultCenter.PostNotificationName(AVAudioSession.InterruptionNotification, AVAudioSession.SharedInstance(), info);
    }
}
#endif
