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

We're including the MediaElement 7.0.0 source code to:
1. Fix critical bugs that affect Android functionality
2. Add headless playback support for Android Auto integration
3. Eliminate workarounds by fixing issues directly in the source

### Fixes Applied

#### ✅ Bug Fixes

1. **Issue #2976 - Android 14+ Notification Body Tap Does Nothing**
   - **Problem**: On Android 14+ (especially Pixel 7a), tapping the notification body did not bring the app to foreground
   - **Solution**: Implemented the recommended Android 14+ approach in `MediaControlsService.android.cs`
   - Added fallback `SetContentIntent()` call using the stored PendingIntent from MediaManager
   - Uses `Platform.CurrentActivity` (not `AppContext`) for creating the PendingIntent - critical for reliability
   - Uses `PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent` for proper intent handling
   - See: https://github.com/CommunityToolkit/Maui/issues/2976

2. **Issue #2989 - Missing `OnAudioSessionIdChanged` method causes AbstractMethodError crash on Android**
   - **Problem**: Crash caused by a missing `OnAudioSessionIdChanged` method implementation
   - **Solution**: Added the missing method implementation to prevent AbstractMethodError when audio session IDs change
   - See: https://github.com/CommunityToolkit/Maui/issues/2989

#### ✅ Android Auto Headless Playback Support

**Commit**: [55b4e8365f96b11db69fb52dc69f20bab32822ef](https://github.com/justcoding121/bible-alarm/commit/55b4e8365f96b11db69fb52dc69f20bab32822ef)

**Problem**: MediaElement requires a UI view (TextureView/SurfaceView) to function, which prevents headless audio playback needed for Android Auto.

**Solution**: Implemented comprehensive headless playback support:

- **Added `AndroidViewType.None` enum value** - Enables headless mode where no UI view is created
- **Modified `MediaElementHandler.android.cs`** - Updated `CreatePlatformView()` to return `null` in headless mode, allowing MediaElement to work without a parent view
- **Enhanced `MediaManager.android.cs`** - Implemented shared ExoPlayer instance management and ensured ExoPlayer works without a video surface for audio-only playback
- **Updated `MediaElementService.cs`** - Added manual handler creation for Android headless mode, ensuring the handler exists before setting media source
- **Modified notification service** - Updated to work with Application.Context for headless scenarios where CurrentActivity may be null

These changes allow the MediaElement to function properly in Android Auto scenarios where the UI may not be visible but audio playback must continue. The implementation ensures that ExoPlayer can operate in headless mode without requiring a TextureView or SurfaceView, which is critical for background audio playback in Android Auto.

### License

CommunityToolkit.Maui.MediaElement is licensed under the MIT License. See the LICENSE file in the CommunityToolkit.Maui.MediaElement folder for details.

**MIT License Requirements Met:**
- ✅ LICENSE file included
- ✅ Copyright notice preserved in source files
- ✅ Original license terms maintained

