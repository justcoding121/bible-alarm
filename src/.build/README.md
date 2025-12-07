# Build Scripts

This folder contains build automation scripts for the Bible Alarm project.

## Individual Platform Scripts

### Android Script

#### `build-android-fast.ps1`
PowerShell script for fast Android debug builds with optimized settings.

**Usage:**
```powershell
cd src\.build
.\build-android-fast.ps1
```

### iOS Script

#### `build-ios-fast.ps1`
PowerShell script for fast iOS debug builds with optimized settings.

**Usage:**
```powershell
cd src\.build
.\build-ios-fast.ps1
```

### Windows Script

#### `build-windows-fast.ps1`
PowerShell script for fast Windows debug builds with optimized settings.

**Usage:**
```powershell
cd src\.build
.\build-windows-fast.ps1
```

## Multi-Platform Script

### `build-all-fast.ps1`
PowerShell script to build all platforms or a specific platform.

**Usage:**
```powershell
cd src\.build
.\build-all-fast.ps1 [-Platform platform]
```

**Platforms:**
- `all` (default) - Build all platforms
- `android` - Build Android only
- `ios` - Build iOS only
- `windows` - Build Windows only

## Build Optimizations

### Android Optimizations
- `AndroidLinkMode=None` - Disable code linking
- `AndroidEnableProguard=false` - Disable ProGuard/R8
- `AndroidEnableAssemblyCompression=false` - Disable assembly compression
- `AndroidAotEnable=false` - Disable AOT compilation
- `AndroidUseSharedRuntime=true` - Enable shared runtime
- `AndroidEnableMultiDex=false` - Disable MultiDex for smaller projects
- `AndroidDexTool=d8` - Use modern D8 DEX compiler
- `AndroidUseAapt2=true` - Use AAPT2 for resource processing

### iOS Optimizations
- `MtouchLink=None` - Disable linking for faster builds
- `MtouchDebug=true` - Enable debug mode
- `MtouchUseLlvm=false` - Disable LLVM compilation
- `MtouchInterpreter=false` - Disable interpreter mode
- `MtouchAot=false` - Disable AOT compilation
- `MtouchEnableBitcode=false` - Disable bitcode for faster builds
- `MtouchEnableIncrementalBuilds=true` - Enable incremental builds
- `MtouchFastDev=true` - Enable fast development mode
- `MtouchUseSGen=true` - Use SGen garbage collector
- `MtouchSGenConcurrent=true` - Enable concurrent SGen

### Windows Optimizations
- `UseWinUI=true` - Enable WinUI 3
- `WindowsAppSDKSelfContained=false` - Use framework-dependent deployment
- `WindowsPackageType=None` - Disable packaging for faster builds
- `WindowsAppSDKFrameworkPackageReference=false` - Use local SDK
- `EnableWindowsTargeting=true` - Enable Windows targeting
- `UseWindowsAppSDK=true` - Use Windows App SDK
- `WindowsAppSDKDeploymentManagerAutoInitialize=false` - Disable auto-initialization

## Notes

- These optimizations are only applied for Debug configuration
- Release builds use standard optimization settings
- Build time improvements: 
  - Android: ~2-3 minutes for debug builds
  - iOS: ~3-5 minutes for debug builds (depending on Mac connectivity)
  - Windows: ~1-2 minutes for debug builds
- All optimizations are automatically applied when building from Visual Studio or `dotnet build`
