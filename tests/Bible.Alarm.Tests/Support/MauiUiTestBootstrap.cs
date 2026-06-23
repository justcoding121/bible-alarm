#nullable enable

using Bible.Alarm;
using Bible.Alarm.Common;
using Serilog;
using Syncfusion.Licensing;
using Syncfusion.Maui.Core.Hosting;

namespace Bible.Alarm.Tests.Support;

internal static class MauiUiTestBootstrap
{
    /// <summary>
    /// Keeps the MAUI app builder output alive for the test process. On iOS, discarding the built
    /// <see cref="MauiApp"/> drops Syncfusion handler registration and <see cref="Views.Home"/> XAML fails.
    /// </summary>
    private static MauiApp? retainedMauiApp;

    /// <summary>
    /// True when a MAUI <see cref="Application"/> was created. Required only for UI control/handler tests
    /// (behaviors, CollectionView helpers, navigation with pages). ViewModel unit tests do not need this;
    /// <c>dotnet test</c> on Windows often cannot initialize WinUI MainThread (REGDB_E_CLASSNOTREG).
    /// </summary>
    public static bool IsReady { get; private set; }

    public static Exception? BootstrapFailure { get; private set; }

    public static void TryInitialize()
    {
        if (IsReady)
        {
            return;
        }

        if (Application.Current is not null)
        {
            MauiUiTestHostHelper.EnsureAppResources();
            IsReady = true;
            BootstrapFailure = null;
            return;
        }

        Log.Logger ??= new LoggerConfiguration().CreateLogger();

        try
        {
            if (retainedMauiApp is null)
            {
                if (!string.IsNullOrEmpty(AppSettings.SyncfusionLicenseKey))
                {
                    SyncfusionLicenseProvider.RegisterLicense(AppSettings.SyncfusionLicenseKey);
                }

                retainedMauiApp = MauiApp.CreateBuilder()
                    .UseMauiApp<BibleAlarmTestApplication>()
                    .ConfigureSyncfusionCore()
                    .Build();
            }

            // Build() alone does not always set Application.Current on the Windows test host.
            Application.Current ??= new BibleAlarmTestApplication();

            if (Application.Current is not null)
            {
                MauiUiTestHostHelper.EnsureAppResources();
            }

            IsReady = Application.Current is not null;
            BootstrapFailure = IsReady ? null : new InvalidOperationException("MAUI test host did not create Application.Current.");
        }
        catch (Exception ex)
        {
            BootstrapFailure = ex;
            IsReady = false;
        }
    }

    /// <summary>
    /// Optional full app bootstrap for tests that need <see cref="MauiAppHolder"/> services.
    /// Never call from static/module init — only from individual tests that can tolerate skip.
    /// </summary>
    public static bool TryInitializeFullAppHolder()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return true;
        }

        try
        {
            MauiAppHolder.CreateAndStore();
            return MauiAppHolder.IsInitialized;
        }
        catch (Exception ex)
        {
            BootstrapFailure = ex;
            return false;
        }
    }

    private sealed class BibleAlarmTestApplication : Application;
}
