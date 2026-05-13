#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Validates segments used to compose on-disk bootstrap log paths.
/// </summary>
public static class BootstrapLogPathArguments
{
    public static void Validate(string rootDirectory, string logsDirectoryName, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(logsDirectoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
    }
}
