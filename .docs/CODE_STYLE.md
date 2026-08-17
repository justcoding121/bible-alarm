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

## 4. File Size and When to Split

**Rule**: Prefer a single code file of about **800–1000 lines of code (LOC)**. A cohesive ViewModel or service may stay in one file even if Sonar S104 / S138 / S3776 fire. Readability wins over file-length metrics.

**When to split**: Only when the extracted type has a **nameable responsibility** a reader would look up on its own (e.g. a cascade handler, a progress reporter, a platform adapter). Do **not** split only to shrink a parent file.

**Do not create**:
- `ref` parameter bags for shared mutable fields — use a holder object (`MusicStateHolder` style) or return a record
- One-method `*Gate` / `*PropertyManager` types created only to shrink a parent file

**Structure Guidance** (when a split is justified):
- Prefer helpers with clear responsibilities (avoid “misc” helpers).
- Use folder structure to reflect dependency hierarchy. Place helpers in `{ClassName}Helpers/` subfolder (e.g. `MusicPublicationSelectionViewModelHelpers/`). Naming: `{ClassName}{Responsibility}Helper.cs` or `{ClassName}{Responsibility}Handler.cs`.
  - Higher-level orchestration/feature classes may depend on helpers in the same folder or subfolders (e.g. `Helpers/`, `Internal/`).
  - Helpers should not depend back on higher-level classes (avoid “upward” dependencies and cyclic relationships across folders).

**Sonar**: File-length rules (S104, S138) are disabled in `.editorconfig`. Keep S3776 / S107 as signals; silence them with a private local function, nested record, or a `#pragma` with a one-line *why* — do not extract a class just to quiet the analyzer.

## Additional Guidelines

### Summary Comments
- Use `<summary>` XML documentation comments for public APIs when they add context the type name does not already convey
- Keep summaries concise and focused on "what" and "why", not "how"
- Include important context about thread safety, side effects, or usage patterns
- Do **not** write summaries that only restate the type name

### Exception Handling
- Always log exceptions with context (what operation failed, relevant parameters)
- Use structured logging with Serilog: `_logger.Error(ex, "Operation failed. Parameter: {Param}", paramValue)`
- Include relevant context in log messages to aid debugging

### Code Comments
- Comments must add information the code does not already make obvious
- Explain *why* (constraint, non-obvious invariant, historical trap, platform quirk)
- Delete comments that only restate the next line, narrate control flow, or duplicate a clear method name
- Prefer self-documenting code over comments
- When comments are needed, place them on the line above the code they describe (no same-line comments)

## 5. Multi-Platform Test Layout

**Rule**: When adding tests, place them according to which runtime they need:

| Test type            | Location                                            | Built by                                  | Runs in CI job |
| -------------------- | --------------------------------------------------- | ----------------------------------------- | -------------- |
| Cross-platform       | `tests/Bible.Alarm.Tests/**` (outside `Platforms/`) | All three test hosts via `Compile` glob   | All three      |
| Cross-platform (no MAUI) | `tests/Bible.Alarm.Shared.Tests/**`             | `dotnet test` host (`net10.0`)            | `test-windows` |
| Windows-only         | `tests/Bible.Alarm.Tests/Platforms/Windows/**`      | `Bible.Alarm.Tests` only                  | `test-windows` |
| Android-only         | `tests/Platforms/Android/**`                        | `Bible.Alarm.Tests.Android` only (xharness) | `test-android` |
| iOS-only             | `tests/Platforms/iOS/**`                            | `Bible.Alarm.Tests.iOS` only (xharness)   | `test-ios`     |

**Rationale**: device-test hosts (`Bible.Alarm.Tests.Android`, `Bible.Alarm.Tests.iOS`) include cross-platform tests via a `<Compile Include="..\Bible.Alarm.Tests\**\*.cs" />` glob. They exclude `..\Bible.Alarm.Tests\Platforms\**` so that Windows-only tests do not leak into the Android/iOS APK/.app bundles. Platform-only smoke tests live under `tests/Platforms/{Android,iOS}/` so the host folders stay focused on the test-runner glue.

**Required attributes**:
- Mark Windows-only test classes with `[Trait("Platform", "Windows")]`.
- Mark Android-only test classes with `[Trait("Platform", "Android")]`.
- Mark iOS-only test classes with `[Trait("Platform", "iOS")]`.

The `test-windows` job filters `Platform!=Android&Platform!=iOS` so a forgotten trait causes a single localised failure rather than an opaque cross-platform breakage.

See `tests/README.md` for the local-developer workflow (`tests/run-tests.ps1 -Platform {Windows|Android|iOS|All}`) and `.github/workflows/build.yml` for the matching CI fan-out.
