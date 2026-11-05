# Android playback device checklist

Run on a Release APK. Android 12, 14, and 16 minimum, unless a row says otherwise. Each item is pass or fail with a note. Record the result next to the item. The step 0 run on the unchanged app is the baseline later steps are compared against.

- In-app: play, pause, seek on a streamed track and a cached track, next, previous, finite stop vs indefinite append. Automatic transition onto `bible_alarm_next_dummy` does not call `PlayNextAsync`.
- Exactly one media notification while playing; its buttons, artwork, title work; swipe-away of the app behaves as the baseline.
- Lock screen, wired and Bluetooth headset buttons: play, pause, next, previous.
- Alarm: app killed, screen off, Doze on, battery saver on. Audio starts; exactly one media notification; `AlarmForegroundService` notification disappears only after playback runs; forced refusal of foreground start shows the tap-to-resume notification, never silence.
- Android Auto (Desktop Head Unit and one real head unit): connect with app running; connect with app process dead (cold start, slow bootstrap); browse list of schedules; play an item; artwork and title; pause; next and previous; progress bar moves; connect/disconnect/reconnect; app stays listed when stopped; idle shows last-played; wireless Android Auto quick reconnect.
- Audio focus loss, headphones unplug, phone call mid-playback: pause and resume as baseline.
- Loading and auto-advance: no play/pause button flash in the car during track change.
- Merged manifest assertion script passes.
- iOS and Windows: play, pause, seek, next, previous, schedule an alarm.

## Baseline behavior recorded from code (step 0)

These are the behaviors later steps must copy. They were read from the current source, not from a device.

### Swipe-away / task removed

`MediaControlsService.OnTaskRemoved` clears the player notification manager and cancels all notifications, then raises `TaskRemoved`. `MediaManager.HandleMediaControlsServiceTaskRemoved` calls `Player?.Stop()`. Playback stops when the user swipes the app away. The service does not restart playback.

### `LegacyMediaBrowserService.OnCreate` order

1. `ForegroundServiceOperations.StartForegroundMinimal(this)` first, before anything else.
2. `MediaSessionHelper.Create()` (last-played metadata from preferences, no DI).
3. `base.OnCreate()`.
4. `ForegroundServiceCoordinator.OnAndroidAutoConnected(this)` (replaces the minimal notification).
5. `mediaSessionInitializer.InitializeMediaSession()` then `InitializeBootstrapInBackground()`.
6. `stateSubscriptionManager.SetMediaBrowserService(this)` and `InitializeStateSubscriptionAsync()`.
7. Start `IAndroidAutoDefaultScheduleRotationService` when DI can resolve it.
8. `RefreshDefaultMetadataAfterBootstrap()`.

### `AlarmRingerReceiver.OnReceive` order

1. `MediaSessionHelper.Create()`.
2. `GoAsync()`.
3. Under a process-wide lock, if the intent is an alarm: `ForegroundServiceCoordinator.OnAlarmTriggered(context, scheduleId)`.
4. `MauiAppHolder.CreateAndStore()`, then `MauiProgram.InitializePlatformBootstrap(..., isForeground: false)` on a worker thread, then `WaitForBootstrapAsync()`.
5. `IAndroidAlarmHandler.HandleAsync(scheduleId, isAlarm)`.
6. If it is an alarm, `ISchedulerService.RescheduleNextOccurrenceAsync`.
7. On failure: log and `StopAlarmForegroundServiceIfActive()`.
8. `pendingIntent.Finish()`.
