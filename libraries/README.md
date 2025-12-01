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

### Next Steps

1. Add the project to the solution
2. Reference it instead of the NuGet package
3. Fix the notification bug in `Services/MediaControlsService.android.cs` or `Views/MediaManager.android.cs`
4. Remove the 800ms timer workaround from `AndroidPlayerNotificationService.cs`

### License

CommunityToolkit.Maui.MediaElement is licensed under the MIT License. See the LICENSE file in the CommunityToolkit.Maui.MediaElement folder for details.

**MIT License Requirements Met:**
- ✅ LICENSE file included
- ✅ Copyright notice preserved in source files
- ✅ Original license terms maintained

