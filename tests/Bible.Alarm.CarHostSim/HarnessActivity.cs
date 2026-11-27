using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using MediaAudioAttributes = Android.Media.AudioAttributes;

namespace Bible.Alarm.CarHostSim;

/// <summary>
/// Binds to Bible Alarm as a car host and prints one JSON line per command to logcat tag CarHostSim.
/// </summary>
[Activity(Exported = true, LaunchMode = global::Android.Content.PM.LaunchMode.SingleTop, Name = "com.google.android.projection.gearhead.HarnessActivity")]
[IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { Intent.CategoryLauncher })]
public class HarnessActivity : Activity
{
    private const string Tag = "CarHostSim";
    private const string TargetPackage = "com.jthomas.info.Bible.Alarm";
    private const string TargetService = "bible.alarm.platforms.android.services.media3.BibleAlarmMediaLibraryService";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // An empty activity is paused and killed before the background browse finishes.
        SetContentView(new global::Android.Widget.TextView(this) { Text = "CarHostSim" });
        Run(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Run(intent);
    }

    private void Run(Intent? intent)
    {
        var command = intent?.GetStringExtra("cmd") ?? "state";
        var argument = intent?.GetStringExtra("arg");
        var started = SystemClock.ElapsedRealtime();
        Android.Util.Log.Info(Tag, "start " + command);
        // Media3 completes session futures on the main looper. Blocking Get() here deadlocks the car host.
        _ = Task.Run(() =>
        {
            try
            {
                Android.Util.Log.Info(Tag, Execute(command, argument, started));
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error(Tag, ex.ToString());
                Android.Util.Log.Info(Tag, "{\"cmd\":\"" + command + "\",\"ok\":false,\"error\":\"" + Escape(ex.Message) + "\"}");
            }
        });
    }

    private string Execute(string command, string? argument, long started)
    {
        if (command == "focus")
        {
            return Focus(argument, started);
        }

        var component = new ComponentName(TargetPackage, TargetService);
        var token = new SessionToken(this, component);
        var built = new MediaBrowser.Builder(this, token).BuildAsync()?.Get();
        if (built is not MediaBrowser browser)
        {
            return Result(command, false, started, 0, "browser was not created");
        }

        try
        {
            return command switch
            {
                "root" => Root(browser, started),
                "children" => Children(browser, argument ?? "__ID_ROOT__", started),
                "play" => Play(browser, argument, started),
                "pause" => Transport(browser, static player => player.Pause(), "pause", started),
                "next" => Transport(browser, static player => player.SeekToNext(), "next", started),
                "state" => State(browser, started),
                _ => Result(command, false, started, 0, "unknown command")
            };
        }
        finally
        {
            browser.Release();
        }
    }

    private static string Root(MediaBrowser browser, long started)
    {
        var root = browser.GetLibraryRoot(null)?.Get() as LibraryResult;
        var ok = root?.ResultCode == LibraryResult.ResultSuccess;
        return Result("root", ok, started, ok ? 1 : 0, root?.ResultCode.ToString());
    }

    private static string Children(MediaBrowser browser, string parentId, long started)
    {
        var list = browser.GetChildren(parentId, 0, 50, null)?.Get() as LibraryResult;
        var count = list?.Value switch
        {
            Java.Util.AbstractCollection collection => collection.Size(),
            System.Collections.ICollection items => items.Count,
            _ => 0
        };
        var ok = list?.ResultCode == LibraryResult.ResultSuccess;
        return Result("children", ok && count > 0, started, count, ok ? null : list?.ResultCode.ToString());
    }

    private static string Play(MediaBrowser browser, string? mediaId, long started)
    {
        if (!string.IsNullOrEmpty(mediaId))
        {
            var item = new MediaItem.Builder().SetMediaId(mediaId)!.Build();
            browser.SetMediaItem(item);
        }

        browser.Prepare();
        browser.Play();
        return Result("play", true, started, 1, null);
    }

    private static string Transport(MediaBrowser browser, Action<MediaBrowser> action, string command, long started)
    {
        action(browser);
        return Result(command, true, started, 1, null);
    }

    private static string State(MediaBrowser browser, long started)
    {
        return Result("state", true, started, 1, browser.PlaybackState.ToString());
    }

    private string Focus(string? argument, long started)
    {
        var manager = (AudioManager?)GetSystemService(AudioService);
        if (manager == null)
        {
            return Result("focus", false, started, 0, "no audio manager");
        }

        var focus = argument switch
        {
            "transient" => AudioFocus.GainTransient,
            "duck" => AudioFocus.GainTransientMayDuck,
            "release" => AudioFocus.None,
            _ => AudioFocus.Gain
        };
        if (focus == AudioFocus.None)
        {
            manager.AbandonAudioFocus(null);
            return Result("focus", true, started, 0, "released");
        }

        var attributes = new MediaAudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Music)!
            .Build();
        var request = new AudioFocusRequestClass.Builder(focus)
            .SetAudioAttributes(attributes)!
            .Build();
        var granted = manager.RequestAudioFocus(request!);
        PlayTone();
        return Result("focus", granted == AudioFocusRequest.Granted, started, 1, granted.ToString());
    }

    private void PlayTone()
    {
        var player = MediaPlayer.Create(this, Android.Provider.Settings.System.DefaultNotificationUri);
        player?.Start();
    }

    private static string Result(string command, bool ok, long started, int items, string? error)
    {
        var elapsed = SystemClock.ElapsedRealtime() - started;
        var errorText = error == null ? "null" : "\"" + Escape(error) + "\"";
        return "{\"cmd\":\"" + command + "\",\"ok\":" + (ok ? "true" : "false") + ",\"elapsedMs\":" + elapsed + ",\"items\":" + items + ",\"error\":" + errorText + "}";
    }

    private static string Escape(string? value) =>
        (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "'", StringComparison.Ordinal);
}
