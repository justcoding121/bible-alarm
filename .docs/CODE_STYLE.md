# Code Style Guidelines

This document outlines the code style rules for the Bible Alarm project. All code should follow these guidelines.

## 1. No Param or Return Comments

**Rule**: Do not include `<param>`, `<returns>`, `<exception>`, or `<typeparam>` XML documentation comments.

**Rationale**: Parameter names and return types are self-documenting. Summary comments provide sufficient context.

**Allowed**:
```csharp
/// <summary>
/// Initializes platform-specific bootstrap.
/// This should be called after MauiApp is created to ensure databases and services are initialized.
/// </summary>
public static void InitializePlatformBootstrap(IServiceProvider services, bool isForeground = false)
```

**Not Allowed**:
```csharp
/// <summary>
/// Initializes platform-specific bootstrap.
/// </summary>
/// <param name="services">The service provider</param>
/// <param name="isForeground">If true, runs bootstrap on a background Task</param>
public static void InitializePlatformBootstrap(IServiceProvider services, bool isForeground = false)
```

## 2. Always Log Exception Statements

**Rule**: All exception handling blocks (`catch` clauses) must include logging statements.

**Rationale**: Exceptions should always be logged for debugging and monitoring purposes.

**Required Pattern**:
```csharp
try
{
    // code
}
catch (Exception ex)
{
    _logger.Error(ex, "Error message describing what operation failed");
    throw; // or handle appropriately
}
```

**Not Allowed**:
```csharp
try
{
    // code
}
catch (Exception ex)
{
    // Silent exception handling without logging
    throw;
}
```

**Special Cases**:
- `OperationCanceledException` may be caught without logging if cancellation is expected
- Re-throwing exceptions should still log before re-throwing
- Use appropriate log levels: `Error` for exceptions, `Warning` for recoverable issues, `Debug` for detailed tracing

## 3. No Code Comments on Same Line as Code

**Rule**: Comments must be on separate lines, never inline with code.

**Rationale**: Inline comments reduce readability and make code harder to scan.

**Allowed**:
```csharp
// Initialize the service
var service = new MyService();

// Check if the value is valid
if (value > 0)
{
    ProcessValue(value);
}
```

**Not Allowed**:
```csharp
var service = new MyService(); // Initialize the service

if (value > 0) // Check if the value is valid
{
    ProcessValue(value);
}
```

**Exception**: XML documentation comments (`///`) are allowed on the same line as declarations:
```csharp
/// <summary>
/// Gets the current value
/// </summary>
public int Value { get; set; }
```

## 4. Max 500 LOC Per Code File

**Rule**: Keep a single code file to **500 lines of code (LOC) max**. If it grows beyond this, split it into cohesive helper classes/services.

**Rationale**: Smaller files are easier to navigate, review, test, and refactor.

**Structure Guidance**:
- Split large classes into helper classes with clear responsibilities (avoid “misc” helpers).
- Use folder structure to reflect dependency hierarchy. Place helpers in `{ClassName}Helpers/` subfolder (e.g. `MusicPublicationSelectionViewModelHelpers/`). Naming: `{ClassName}{Responsibility}Helper.cs` or `{ClassName}{Responsibility}Handler.cs`.
  - Higher-level orchestration/feature classes may depend on helpers in the same folder or subfolders (e.g. `Helpers/`, `Internal/`).
  - Helpers should not depend back on higher-level classes (avoid “upward” dependencies and cyclic relationships across folders).

## Additional Guidelines

### Summary Comments
- Use `<summary>` XML documentation comments for public APIs
- Keep summaries concise and focused on "what" and "why", not "how"
- Include important context about thread safety, side effects, or usage patterns

### Exception Handling
- Always log exceptions with context (what operation failed, relevant parameters)
- Use structured logging with Serilog: `_logger.Error(ex, "Operation failed. Parameter: {Param}", paramValue)`
- Include relevant context in log messages to aid debugging

### Code Comments
- Use comments to explain "why", not "what"
- Prefer self-documenting code over comments
- When comments are needed, place them on the line above the code they describe

## 5. Multi-Platform Test Layout

**Rule**: When adding tests, place them according to which runtime they need:

| Test type            | Location                                            | Built by                                  | Runs in CI job |
| -------------------- | --------------------------------------------------- | ----------------------------------------- | -------------- |
| Cross-platform       | `src/Bible.Alarm.Tests/**` (outside `Platforms/`)   | All three test hosts via `Compile` glob   | All three      |
| Cross-platform (no MAUI) | `src/Bible.Alarm.Shared.Tests/**`               | `dotnet test` host (`net10.0`)            | `test-windows` |
| Windows-only         | `src/Bible.Alarm.Tests/Platforms/Windows/**`        | `Bible.Alarm.Tests` only                  | `test-windows` |
| Android-only         | `tests/Platforms/Android/**`                        | `Bible.Alarm.Tests.Android` only (xharness) | `test-android` |
| iOS-only             | `tests/Platforms/iOS/**`                            | `Bible.Alarm.Tests.iOS` only (xharness)   | `test-ios`     |

**Rationale**: device-test hosts (`Bible.Alarm.Tests.Android`, `Bible.Alarm.Tests.iOS`) include cross-platform tests via a `<Compile Include="..\Bible.Alarm.Tests\**\*.cs" />` glob. They exclude `..\Bible.Alarm.Tests\Platforms\**` so that Windows-only tests do not leak into the Android/iOS APK/.app bundles. Platform-only smoke tests live under `tests/Platforms/{Android,iOS}/` so the host folders stay focused on the test-runner glue.

**Required attributes**:
- Mark Windows-only test classes with `[Trait("Platform", "Windows")]`.
- Mark Android-only test classes with `[Trait("Platform", "Android")]`.
- Mark iOS-only test classes with `[Trait("Platform", "iOS")]`.

The `test-windows` job filters `Platform!=Android&Platform!=iOS` so a forgotten trait causes a single localised failure rather than an opaque cross-platform breakage.

See `tests/README.md` for the local-developer workflow (`tests/run-tests.ps1 -Platform {Windows|Android|iOS|All}`) and `.github/workflows/build.yml` for the matching CI fan-out.
