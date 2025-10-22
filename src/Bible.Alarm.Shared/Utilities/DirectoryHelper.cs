using System;
using System.IO;

namespace Bible.Alarm.Shared.Utilities;

public static class DirectoryHelper
{
    public static string IndexDirectory => indexDirectory.Value;

    private static readonly Lazy<string> indexDirectory = new(() =>
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDir.Name != "bible-alarm") currentDir = currentDir.Parent;

        return Path.Combine(currentDir.FullName, "src", "_tools", "_index");
    });

    public static void Ensure(string directory)
    {
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
    }
}