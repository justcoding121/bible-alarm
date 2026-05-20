#nullable enable

using Bible.Alarm.Common;
using Serilog;

namespace Bible.Alarm.Tests.Support;

internal static class MauiUiTestBootstrap
{
    public static bool IsReady { get; private set; }

    public static void TryInitialize()
    {
        if (MauiAppHolder.IsInitialized)
        {
            IsReady = true;
            return;
        }

        try
        {
            Log.Logger ??= new LoggerConfiguration().CreateLogger();

            MauiAppHolder.CreateAndStore();
            IsReady = true;
        }
        catch
        {
            IsReady = false;
        }
    }
}
