# Media3 1.11.1 spike report

Date: 2026-11-05. Branch: `unify-media-session`. Package: `Xamarin.AndroidX.Media3.*` 1.11.1 (`net10.0-android36.0`).

This is not a device go. No phone, Desktop Head Unit, or killed-process alarm was available in this run, so items 3, 4, and 6 have no logcat. The throwaway `unify-media-spike` branch was not created. The decision below is compile-time evidence only. Device checklist items stay "not run".

## Result

**No-go for the device gate. Continue Option C on compile-time evidence only, because a device was not available and the binding surface required by Option C is present.** Do not treat this file as permission to skip the section 6 device matrix before release.

Fallback in plan section 2.3 was not applied. `SimpleBasePlayer` is bound and subclassable from the metadata dump below, so `ForwardingPlayer` was not chosen.

## Item results

| Item | Result | Evidence |
| --- | --- | --- |
| 1. `MediaLibraryService` + `MediaLibrarySession` | Compile-time pass. Device/trimming not run. | Binding contains `AndroidX.Media3.Session.MediaLibraryService.OnGetSessionFromMediaLibraryService` and nested `MediaLibrarySession` (`NotifyChildrenChanged`, `ClearReplicatedLibraryError`, `NotifySearchResultChanged`). The app compiled Release for `net10.0-android` on 1.11.1. Release APK trimming of a new service was not installed on a device. |
| 2. `SimpleBasePlayer` from C# | Compile-time pass. Runtime `Build()` not executed. | `AndroidX.Media3.Common.SimpleBasePlayer` exposes `GetState`, `InvalidateState`, `HandleSetPlayWhenReady`, `HandleSeek`, `HandleSetMediaItems`, `HandleStop`, `HandlePrepare`, `HandleRelease`. Nested `State.Builder` has `SetPlaybackState`, `SetPlayWhenReady`, `SetPlaylist`, `SetAvailableCommands`, `SetPlayerError`, `SetContentPositionMs`, `Build`. |
| 3. Controller interop (DHU, notification, play-from-id) | Not run | No Desktop Head Unit and no system notification session in this run. |
| 4. Idle last-played metadata and cold Play | Not run | No device. |
| 5. `DefaultMediaNotificationProvider` | Compile-time pass. Custom provider not required yet. | `DefaultMediaNotificationProvider` and `MediaNotification` exist in the 1.11.1 session binding. Channel id and notification id were not set on a device. No custom `MediaNotification.IProvider` was constructed, so `Arg_DlgtTargMeth` was not re-tested. |
| 6. Foreground start rules | Not run | No foreground app, killed-process alarm, or car-connect cold start. `OnForegroundServiceStartNotAllowedException` was not forced. Whether Media3 promotes itself to foreground, and whether a minimal `StartForeground` must share notification id 2, is still unknown. Step 8 must keep `AlarmForegroundService` until playback is running and must handle the listener. |
| 7. Session release / player singleton reattach | Not run | No service destroy/recreate on a device. The production design still uses a process-wide player singleton so early callers do not need the service. |

Go requires items 1–4 and 6 on a device. Those device checks did not pass because they were not run.

## Binding notes used by later steps

- Library `ICallback`: `OnGetChildren`, `OnGetItem`, `OnGetLibraryRoot`, `OnGetSearchResult`, `OnSearch`, `OnSubscribe`, `OnUnsubscribe`.
- Session `ICallback`: `OnConnect`, `OnAddMediaItems`, `OnSetMediaItems`, `OnPlaybackResumption`, `OnCustomCommand`, `OnDisconnected`.
- `LibraryResult`: `OfItem`, `OfItemList`, `OfError`, `OfVoid`.
- `ForwardingPlayer` is bound as `Get*Handler` methods and is the awkward fallback. It was not selected.
- AndroidX Media 1.8 marks `MediaSessionCompat`, `MediaMetadataCompat`, `PlaybackStateCompat`, and `MediaBrowserServiceCompat` obsolete (`CS0618`). Expected until cutover.
