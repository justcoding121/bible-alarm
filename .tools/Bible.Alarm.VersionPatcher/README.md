# Bible Alarm Version Patcher

A modularized console application for automatically incrementing version numbers across all platform-specific manifest files in the Bible Alarm project.

## Architecture

The application has been refactored to use dependency injection and follows a clean architecture pattern with clear separation of concerns.

### Project Structure

```
.tools/Bible.Alarm.VersionPatcher/
├── Services/
│   ├── Contracts/           # Service interfaces
│   │   ├── IVersionPatchingService.cs
│   │   ├── IPlatformVersionPatcher.cs
│   │   ├── IVersionService.cs
│   │   └── IFileService.cs
│   └── Infrastructure/     # Service implementations
│       ├── VersionPatchingService.cs
│       ├── AndroidVersionPatcher.cs
│       ├── IOSVersionPatcher.cs
│       ├── WindowsVersionPatcher.cs
│       ├── VersionService.cs
│       ├── FileService.cs
│       └── ServiceCollectionExtensions.cs
└── Program.cs              # Main entry point
```

## Services

### Core Services

- **IVersionPatchingService**: Main orchestrator that coordinates version patching across all platforms
- **IVersionService**: Handles version number increment logic
- **IFileService**: Handles file I/O operations

### Platform-Specific Services

- **AndroidVersionPatcher**: Patches AndroidManifest.xml files
- **IOSVersionPatcher**: Patches Info.plist files
- **WindowsVersionPatcher**: Patches Package.appxmanifest files

## Key Features

### Dependency Injection
- Uses Microsoft.Extensions.DependencyInjection for simple IoC container
- All services are registered as singletons for optimal performance
- Easy to test and mock individual services
- Simple ServiceCollection approach (no HostBuilder overhead)

### Modular Design
- Each service has a single responsibility
- Clear interfaces make the code testable and maintainable
- Platform-specific patchers can be easily added or modified

### Error Handling
- Comprehensive error handling in each patcher
- Detailed logging and error reporting
- Graceful failure handling with proper exit codes

### Version Logic
- Consistent version increment logic across all platforms
- Handles major.minor version format (e.g., 1.2 -> 1.3)
- Special handling for version rollover (99 -> 0 with major increment)
- Platform-specific version code handling (Android)

## Usage

Run **before** building the app (e.g. in CI before `dotnet publish`, or locally before release builds):

```bash
dotnet run --project .tools/Bible.Alarm.VersionPatcher/Bible.Alarm.VersionPatcher.csproj --configuration Release
```

The application will:
1. Initialize the dependency injection container
2. Load all platform-specific patchers
3. Execute version patching for each platform in parallel (manifests and csproj)
4. Report success/failure for each platform
5. Exit with appropriate status code

All version-sensitive manifests and project files are updated so a single run is sufficient before build; no extra version bump steps should be run in the pipeline.

## Supported Platforms

### Android (MAUI uses csproj for version)
- **File**: `src/Bible.Alarm/Bible.Alarm.csproj`
- **Attributes**: `ApplicationVersion` (version code), `ApplicationDisplayVersion` (version name)
- **Logic**: Increments both; shared with other platforms for display

### iOS
- **File**: `src/Bible.Alarm/Platforms/iOS/Info.plist`
- **Key**: `CFBundleVersion`
- **Logic**: Increments version number

### Windows
- **File**: `src/Bible.Alarm/Platforms/Windows/Package.appxmanifest`
- **Attribute**: `Version`
- **Logic**: Increments major.minor version (sets build and revision to 0). Microsoft Store requires the revision (4th component) to be 0 (e.g. `2.1.1.0` is valid; `2.1.0.1` is rejected).

## Version Increment Logic

The version increment follows this pattern:
- **Current**: `1.2` → **New**: `1.3`
- **Current**: `1.99` → **New**: `2.0`
- **Current**: `99.99` → **New**: `100.0`

For Android, the version code is simply incremented by 1.

## Benefits of Modularization

1. **Testability**: Each service can be unit tested independently
2. **Maintainability**: Clear separation of concerns makes code easier to maintain
3. **Extensibility**: New platforms can be easily added by implementing `IPlatformVersionPatcher`
4. **Reusability**: Services can be reused in other applications
5. **Error Handling**: Better error isolation and handling per platform
6. **Performance**: Parallel execution of platform patchers
7. **Logging**: Comprehensive logging for debugging and monitoring

## Migration from Legacy Code

The original monolithic `Program.cs` has been broken down into:

- **VersionService**: Contains the version increment logic
- **FileService**: Contains file I/O operations
- **AndroidVersionPatcher**: Contains Android-specific patching logic
- **IOSVersionPatcher**: Contains iOS-specific patching logic
- **WindowsVersionPatcher**: Contains Windows-specific patching logic
- **VersionPatchingService**: Orchestrates the entire patching process

## Error Handling

The application includes comprehensive error handling:
- File existence checks before processing
- XML parsing error handling
- Version format validation
- Platform-specific error reporting
- Proper exit codes for CI/CD integration

## Configuration

The application uses the shared `DirectoryHelper.IndexDirectory` for determining the project root path. All file paths are constructed relative to this directory.
