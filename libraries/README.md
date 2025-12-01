# Libraries

This folder contains forked/embedded third-party library source code.

## CommunityToolkit.Maui.MediaElement

**Status**: ✅ Source code copied (49 files)  
**License**: ✅ MIT License file included

### Directory Structure

```
libraries/
└── CommunityToolkit.Maui.MediaElement/
    ├── LICENSE (MIT License)
    ├── CommunityToolkit.Maui.MediaElement.csproj
    ├── Converters/
    ├── Extensions/
    ├── Handlers/
    ├── Interfaces/
    ├── MediaSource/
    ├── Platforms/
    │   └── Android/
    ├── Primitives/
    ├── Services/
    └── Views/
```

### Why We're Forking

We're including the MediaElement 7.0.0 source code to fix the Android 14+ notification body tap bug directly in the source, eliminating the need for the 800ms timer workaround.

### Fixes Applied

#### ✅ Android 14+ Notification Body Tap Fix (Commit: 804a177)

**Problem**: On Android 14+ (especially Pixel 7a), tapping the notification body did not bring the app to foreground.

**Solution**: Implemented the recommended Android 14+ approach using `MediaSession.SetSessionActivity()`:

1. **MediaManager.android.cs**:
   - Added `SetSessionActivity()` call to set a PendingIntent that brings the app to foreground when notification body is tapped
   - Uses explicit Intent targeting MainActivity with `CLEAR_TOP | SINGLE_TOP` flags for 100% reliability
   - Stores the PendingIntent in a static property for fallback use
   - Added proper error handling with logging

2. **MediaControlsService.android.cs**:
   - Added fallback `SetContentIntent()` call using the stored PendingIntent from MediaManager
   - This bypasses view interception and ensures notification body taps work for Android 14+

**Key Implementation Details**:
- Uses `Platform.CurrentActivity` (not `AppContext`) for creating the PendingIntent - this is critical for reliability
- Uses `PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent` for proper intent handling
- The fix follows Android's recommended approach for Android 14+ notification body taps

#### ✅ Removed Timer Workaround (Commit: d18747e)

Removed the 800ms timer workaround from `AndroidPlayerNotificationService.cs` that was continuously reapplying the notification ContentIntent. This hack is no longer needed with the proper fix in place.

### Next Steps

1. ✅ Add the project to the solution
2. ✅ Reference it instead of the NuGet package
3. ✅ Fix the notification bug in `Services/MediaControlsService.android.cs` and `Views/MediaManager.android.cs`
4. ✅ Remove the 800ms timer workaround from `AndroidPlayerNotificationService.cs`

### License

CommunityToolkit.Maui.MediaElement is licensed under the MIT License. See the LICENSE file in the CommunityToolkit.Maui.MediaElement folder for details.

**MIT License Requirements Met:**
- ✅ LICENSE file included
- ✅ Copyright notice preserved in source files
- ✅ Original license terms maintained

