#nullable enable

using CommunityToolkit.Maui;

namespace CommunityToolkit.Maui.MediaElement.Tests.Support;

internal static class MediaElementTestBootstrap
{
    public static bool IsReady { get; private set; }

    public static void TryInitialize()
    {
        if (Microsoft.Maui.Controls.Application.Current != null)
        {
            IsReady = true;
            return;
        }

        try
        {
            MauiApp.CreateBuilder()
                .UseMauiCommunityToolkitMediaElement()
                .UseMauiApp<TestApp>()
                .Build();
            IsReady = true;
        }
        catch
        {
            IsReady = false;
        }
    }

    private sealed class TestApp : Microsoft.Maui.Controls.Application;
}
