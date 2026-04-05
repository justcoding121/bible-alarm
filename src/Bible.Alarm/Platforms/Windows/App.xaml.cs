#nullable enable

using System.Diagnostics;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Windows.Helpers;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Platforms.Windows.Services.UI.WindowsToastServiceHelpers;
using Bible.Alarm.Services.Scheduler.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using Windows.ApplicationModel.Activation;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace Bible.Alarm.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class App : MauiWinUIApplication
{
    private static ILogger Logger => Log.ForContext<App>();

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
        InitializeComponent();
    }

    private const int CrashFlushDelayMs = 500;

    private void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WindowsBootstrapLogger.WriteException(e.Exception);
        Logger.Error(e.Exception, "Unobserved task exception.");
        FlushAndDelay();
    }

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception != null)
        {
            WindowsBootstrapLogger.WriteException(exception);
            Logger.Fatal(exception, "Unhandled exception occurred. IsTerminating: {IsTerminating}", e.IsTerminating);
        }
        else
        {
            WindowsBootstrapLogger.WriteLine($"Unhandled non-Exception: {e.ExceptionObject}. IsTerminating: {e.IsTerminating}");
            Logger.Fatal("Unhandled exception (non-Exception object): {ExceptionObject}. IsTerminating: {IsTerminating}",
                e.ExceptionObject, e.IsTerminating);
        }

        FlushAndDelay();
    }

    private static void FlushAndDelay()
    {
        try
        {
            Log.CloseAndFlush();
        }
        catch
        {
        }

        Thread.Sleep(CrashFlushDelayMs);
    }

    protected override MauiApp CreateMauiApp() => MauiAppHolder.CreateAndStore();

    /// <summary>
    /// Handles app activation (e.g., from toast notifications, protocol handlers, etc.)
    /// This replaces the UWP OnActivated method for WinUI 3.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Check for existing instance BEFORE calling base.OnLaunched to prevent window flash
        // Use AppInstance to ensure only one instance of the app runs
        // This prevents opening a new instance when clicking toast notifications or alarms
        var key = "BibleAlarmInstance";
        var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var instance = AppInstance.FindOrRegisterForKey(key);

        // If this is not the main instance, redirect activation and exit WITHOUT creating window
        // Unless the "main" instance is no longer running (stale key from a crash) - then we become main
        if (!instance.IsCurrent)
        {
            var others = Process.GetProcessesByName("Bible.Alarm")
                .Where(p => p.Id != Environment.ProcessId)
                .ToList();
            if (others.Count == 0)
            {
                Logger.Information("Single-instance key was held by a process that is no longer running; proceeding as main instance.");
            }
            else
            {
                Logger.Information("App already running - redirecting activation to existing instance. Arguments: {Arguments}", args.Arguments);
                instance.RedirectActivationToAsync(activatedEventArgs).AsTask().Wait();
                Environment.Exit(0);
                return;
            }
        }

        // This is the main instance - now safe to create window
        base.OnLaunched(args);

        // This is the main instance - set up activation handler
        instance.Activated += OnAppInstanceActivated;

        // Ensure MauiApp is created
        // Foreground launch - bootstrap will run on background Task
        MauiAppHolder.CreateAndStore();

        // Handle activation arguments (e.g., from toast notifications)
        // Check both args.Arguments and activatedEventArgs to handle all activation scenarios
        if (!string.IsNullOrEmpty(args.Arguments))
        {
            HandleActivation(args.Arguments);
        }
        else if (activatedEventArgs != null)
        {
            // Handle activation from AppInstance (e.g., when app is launched from notification)
            // This handles the case where args.Arguments is empty but activation came from a notification
            HandleAppInstanceActivation(activatedEventArgs);
        }
    }

    /// <summary>
    /// Handles app instance activation (protocol handlers, toast notifications, etc.)
    /// This is called when activation is redirected to an existing instance.
    /// </summary>
    private void OnAppInstanceActivated(object? sender, AppActivationArguments e)
    {
        try
        {
            // Ensure MauiApp is created
            MauiAppHolder.CreateAndStore();

            // Bring window to foreground when activated from notification
            BringWindowToForeground();

            // Handle the activation
            HandleAppInstanceActivation(e);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling app instance activation");
        }
    }

    /// <summary>
    /// Handles app instance activation arguments from various sources (toast notifications, protocol handlers, etc.)
    /// This is used both for initial launch and redirected activations.
    /// </summary>
    private static void HandleAppInstanceActivation(AppActivationArguments e)
    {
        try
        {
            // Handle different activation kinds
            if (e.Kind == ExtendedActivationKind.Protocol)
            {
                var protocolArgs = e.Data as ProtocolActivatedEventArgs;
                if (protocolArgs?.Uri != null)
                {
                    HandleActivation(protocolArgs.Uri.Query);
                }
            }
            else if (e.Kind == ExtendedActivationKind.ToastNotification)
            {
                var toastArgs = e.Data as ToastNotificationActivatedEventArgs;
                if (toastArgs?.Argument != null)
                {
                    Logger.Information("Toast activation received: {Arguments}", toastArgs.Argument);
                    HandleActivation(toastArgs.Argument);
                }
            }
            else if (e.Kind == ExtendedActivationKind.Launch)
            {
                // Handle launch activation (e.g., from toast notifications that use foreground activation)
                var launchArgs = e.Data as LaunchActivatedEventArgs;
                if (launchArgs?.Arguments != null)
                {
                    Logger.Information("Launch activation received: {Arguments}", launchArgs.Arguments);
                    HandleActivation(launchArgs.Arguments);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling app instance activation arguments");
        }
    }

    /// <summary>
    /// Brings the main window to the foreground and activates it.
    /// This ensures the app window is visible when activated from a notification.
    /// </summary>
    private static void BringWindowToForeground()
    {
        try
        {
            var window = ToastWindowManager.GetNativeWindow();
            if (window == null)
            {
                Logger.Debug("Window not available when trying to bring to foreground");
                return;
            }

            // Get the DispatcherQueue for the window to ensure we run on the UI thread
            var dispatcherQueue = window.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                Logger.Debug("DispatcherQueue not available when trying to bring to foreground");
                return;
            }

            // Dispatch to UI thread - AppWindow operations must be on the UI thread
            dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    // Get AppWindow from the window (must be on UI thread)
                    var appWindow = window.AppWindow;
                    if (appWindow == null)
                    {
                        Logger.Debug("AppWindow not available when trying to bring to foreground");
                        return;
                    }

                    // Check if window is minimized and restore it
                    var presenter = appWindow.Presenter as OverlappedPresenter;
                    if (presenter != null && presenter.State == OverlappedPresenterState.Minimized)
                    {
                        presenter.Restore();
                        Logger.Debug("Window restored from minimized state");
                    }

                    // Show the window (brings to foreground)
                    // This ensures the window is visible when activated from a notification
                    appWindow.Show();

                    Logger.Debug("Window brought to foreground from notification activation");
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to bring window to foreground on UI thread, but continuing with activation");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to bring window to foreground, but continuing with activation");
        }
    }

    /// <summary>
    /// Handles activation arguments (e.g., schedule ID from toast notification, media controls)
    /// </summary>
    private static void HandleActivation(string arguments)
    {
        try
        {
            // Check for media control actions first
            if (arguments.StartsWith("action=", StringComparison.OrdinalIgnoreCase))
            {
                HandleMediaControlAction(arguments);
                return;
            }

            // Parse schedule ID from arguments
            // Format could be: "scheduleId=123" or just "123"
            if (int.TryParse(arguments.Trim(), out var scheduleId) ||
                (arguments.Contains("scheduleId=") &&
                 int.TryParse(arguments.Split('=').LastOrDefault(), out scheduleId)))
            {
            Task.Run(async () =>
            {
                try
                {
                    MauiAppHolder.CreateAndStore();

                    MauiProgram.InitializePlatformBootstrap(MauiAppHolder.Services, isForeground: false);
                    await MauiProgram.WaitForBootstrapAsync();

                    var alarmHandler = MauiAppHolder.Services.GetRequiredService<IWindowsAlarmHandler>();
                    await alarmHandler.HandleAsync(scheduleId, true);

                    var schedulerService = MauiAppHolder.Services.GetRequiredService<ISchedulerService>();
                    await schedulerService.RescheduleNextOccurrenceAsync(scheduleId);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Error handling alarm activation for schedule {scheduleId}");
                }
                });
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error parsing activation arguments");
        }
    }

    /// <summary>
    /// Handles media control actions from toast notifications (next/previous)
    /// </summary>
    private static void HandleMediaControlAction(string arguments)
    {
        try
        {
            Task.Run(async () =>
            {
                try
                {
                    // Ensure MauiApp is created
                    MauiAppHolder.CreateAndStore();

                    if (arguments.Contains("action=next", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Information("Toast notification: Next button pressed");
                        WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());
                    }
                    else if (arguments.Contains("action=previous", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Information("Toast notification: Previous button pressed");
                        WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());
                    }
                    else if (arguments.Contains("action=play", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Information("Toast notification: Play button pressed");
                        WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                    }
                    else if (arguments.Contains("action=pause", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Information("Toast notification: Pause button pressed");
                        WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error handling media control action");
                }
            });
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error parsing media control action");
        }
    }

    /// <summary>
    /// Note: WinUI 3 desktop apps don't support UWP background tasks the same way.
    /// Background tasks from the Xamarin UWP version (SchedulerTask, MediaIndexUpdateTask)
    /// are handled differently in WinUI 3:
    /// - Use Windows Task Scheduler for system-level scheduling
    /// - Use app lifecycle events for in-process background work
    /// - Use AppInstance activation for protocol handlers and notifications
    /// 
    /// The OnBackgroundActivated method from UWP is not available in WinUI 3.
    /// Instead, use AppInstance.GetCurrent().Activated event (already handled above).
    /// </summary>
}
