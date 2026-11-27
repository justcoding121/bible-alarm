#if UITEST
#nullable enable

using Android.Content;
using Android.Media;
using Android.OS;
using Bible.Alarm.Platforms.Android.Services.Audio;
using Bible.Alarm.Platforms.Android.Services.BroadcastReceivers;
using Bible.Alarm.Services.UiTest;
using Serilog;

namespace Bible.Alarm.Platforms.Android.UiTest;

/// <summary>
/// Debug-only entry points so UI tests can fire an alarm, simulate unplugging audio, and read state.
/// Store builds do not compile this receiver.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[global::Android.App.IntentFilter(new[]
{
    FireAlarmAction,
    BecomingNoisyAction,
    DumpStateAction
})]
public class UiTestHookReceiver : BroadcastReceiver
{
    public const string FireAlarmAction = "com.jthomas.info.Bible.Alarm.UITEST_FIRE_ALARM";
    public const string BecomingNoisyAction = "com.jthomas.info.Bible.Alarm.UITEST_BECOMING_NOISY";
    public const string DumpStateAction = "com.jthomas.info.Bible.Alarm.UITEST_DUMP_STATE";

    private static readonly ILogger logger = Log.ForContext<UiTestHookReceiver>();

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent?.Action == null)
        {
            return;
        }

        try
        {
            switch (intent.Action)
            {
                case FireAlarmAction:
                    FireAlarm(context, intent);
                    break;
                case BecomingNoisyAction:
                    new AudioNoisyReceiver().OnReceive(context, new Intent(AudioManager.ActionAudioBecomingNoisy));
                    break;
                case DumpStateAction:
                    var json = UiTestStateDump.ToJson();
                    global::Android.Util.Log.Info(UiTestStateDump.LogTag, json);
                    break;
                default:
                    logger.Warning("Ignored UI test action {Action}", intent.Action);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "UI test hook {Action} failed", intent.Action);
        }
    }

    private static void FireAlarm(Context context, Intent intent)
    {
        var scheduleId = intent.GetStringExtra("ScheduleId");
        if (string.IsNullOrEmpty(scheduleId))
        {
            var numeric = intent.GetLongExtra("scheduleId", intent.GetIntExtra("scheduleId", 0));
            if (numeric > 0)
            {
                scheduleId = numeric.ToString();
            }
        }

        if (string.IsNullOrEmpty(scheduleId))
        {
            logger.Warning("UI test fire-alarm broadcast had no schedule id");
            return;
        }

        var alarm = new Intent(context, typeof(AlarmRingerReceiver));
        alarm.PutExtra("ScheduleId", scheduleId);
        alarm.PutExtra("IsAlarm", true);
        new AlarmRingerReceiver().OnReceive(context, alarm);
    }
}
#endif
