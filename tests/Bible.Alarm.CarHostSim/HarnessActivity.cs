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
    private AudioFocusRequestClass? heldFocus;
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
        // MediaBrowser checks the main looper, and its futures also complete there.
        // Get() on that looper deadlocks, so each future reports through a listener instead.
        new Handler(Looper.MainLooper!).Post(() => Begin(command, argument, started));
    }

    private void Begin(string command, string? argument, long started)
    {
        try
        {
            if (command == "focus")
            {
                Android.Util.Log.Info(Tag, Focus(argument, started));
                return;
            }

            var component = new ComponentName(TargetPackage, TargetService);
            var token = new SessionToken(this, component);
            var future = new MediaBrowser.Builder(this, token).BuildAsync();
            if (future == null)
            {
                Fail(command, started, "browser was not created");
                return;
            }

            WhenComplete(future, () =>
            {
                MediaBrowser? browser = null;
                try
                {
                    browser = future.Get() as MediaBrowser;
                    if (browser == null)
                    {
                        Fail(command, started, "browser was not created");
                        return;
                    }

                    Dispatch(browser, command, argument, started);
                }
                catch (Exception ex)
                {
                    Report(command, started, ex);
                    Release(browser);
                }
            }, command, started);
        }
        catch (Exception ex)
        {
            Report(command, started, ex);
        }
    }

    private void Dispatch(MediaBrowser browser, string command, string? argument, long started)
    {
        switch (command)
        {
            case "root":
                var rootFuture = browser.GetLibraryRoot(null);
                if (rootFuture == null)
                {
                    Finish(browser, Result(command, false, started, 0, "root future was null"));
                    return;
                }

                WhenComplete(rootFuture, () =>
                {
                    var root = rootFuture.Get() as LibraryResult;
                    var ok = root?.ResultCode == LibraryResult.ResultSuccess;
                    Finish(browser, Result("root", ok, started, ok ? 1 : 0, root?.ResultCode.ToString()));
                }, command, started, browser);
                return;
            case "children":
                var childrenFuture = browser.GetChildren(argument ?? "__ID_ROOT__", 0, 50, null);
                if (childrenFuture == null)
                {
                    Finish(browser, Result(command, false, started, 0, "children future was null"));
                    return;
                }

                WhenComplete(childrenFuture, () =>
                {
                    var list = childrenFuture.Get() as LibraryResult;
                    var count = Count(list);
                    var ok = list?.ResultCode == LibraryResult.ResultSuccess && count > 0;
                    Finish(browser, Result("children", ok, started, count, ok ? null : list?.ResultCode.ToString()));
                }, command, started, browser);
                return;
            case "play":
                Finish(browser, Play(browser, argument, started));
                return;
            case "pause":
                Finish(browser, Transport(browser, static player => player.Pause(), "pause", started));
                return;
            case "next":
                Finish(browser, Transport(browser, static player => player.SeekToNext(), "next", started));
                return;
            case "state":
                Finish(browser, State(browser, started));
                return;
            default:
                Finish(browser, Result(command, false, started, 0, "unknown command"));
                return;
        }
    }

    private static void WhenComplete(
        object? future,
        Action completed,
        string command,
        long started,
        MediaBrowser? browser = null)
    {
        // Guava's ListenableFuture is a private runtime dependency of Media3, so the listener is invoked by name.
        var addListener = future?.GetType().GetMethods().FirstOrDefault(method =>
            method.Name == "AddListener" && method.GetParameters().Length == 2);
        if (addListener == null)
        {
            Fail(command, started, "future was not listenable");
            Release(browser);
            return;
        }

        addListener.Invoke(future, new object?[]
        {
            new Java.Lang.Runnable(() =>
            {
                try
                {
                    completed();
                }
                catch (Exception ex)
                {
                    Report(command, started, ex);
                    Release(browser);
                }
            }),
            new DirectExecutor()
        });
    }

    private static int Count(LibraryResult? list)
    {
        return list?.Value switch
        {
            Java.Util.AbstractCollection collection => collection.Size(),
            System.Collections.ICollection items => items.Count,
            _ => 0
        };
    }

    private static void Finish(MediaBrowser browser, string json)
    {
        Android.Util.Log.Info(Tag, json);
        Release(browser);
    }

    private static void Release(MediaBrowser? browser)
    {
        if (browser == null)
        {
            return;
        }

        // The browse listener runs on the future's thread. Release only succeeds on the main looper.
        if (Looper.MyLooper() == Looper.MainLooper)
        {
            ReleaseNow(browser);
            return;
        }

        new Handler(Looper.MainLooper!).Post(() => ReleaseNow(browser));
    }

    private static void ReleaseNow(MediaBrowser browser)
    {
        try
        {
            browser.Release();
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error(Tag, "release failed: " + ex.Message);
        }
    }

    private static void Fail(string command, long started, string error)
    {
        Android.Util.Log.Info(Tag, Result(command, false, started, 0, error));
    }

    private static void Report(string command, long started, Exception ex)
    {
        Android.Util.Log.Error(Tag, ex.ToString());
        Android.Util.Log.Info(Tag, "{\"cmd\":\"" + command + "\",\"ok\":false,\"error\":\"" + Escape(ex.Message) + "\"}");
    }

    private sealed class DirectExecutor : Java.Lang.Object, Java.Util.Concurrent.IExecutor
    {
        public void Execute(Java.Lang.IRunnable? command) => command?.Run();
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
            if (heldFocus != null)
            {
                manager.AbandonAudioFocusRequest(heldFocus);
                heldFocus = null;
            }

            return Result("focus", true, started, 0, "released");
        }

        if (heldFocus != null)
        {
            manager.AbandonAudioFocusRequest(heldFocus);
            heldFocus = null;
        }

        var attributes = new MediaAudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Music)!
            .Build();
        var request = new AudioFocusRequestClass.Builder(focus)
            .SetAudioAttributes(attributes)!
            .Build();
        heldFocus = request;
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
