#nullable enable

using System.Linq;
using Windows.ApplicationModel.Activation;
using Bible.Alarm.Common;
using Bible.Alarm.Common.Messenger;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Bible.Alarm.Services.Scheduler.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace Bible.Alarm.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class App : MauiWinUIApplication
{
    private static readonly ILogger logger = Log.ForContext<App>();

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Set up global exception handlers
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
        TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
    }

    private void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e) => logger.Error(e.Exception, "Unobserved task exception.");

    private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception != null)
        {
            logger.Fatal(exception, "Unhandled exception occurred. IsTerminating: {IsTerminating}", e.IsTerminating);
        }
        else
        {
            logger.Fatal("Unhandled exception (non-Exception object): {ExceptionObject}. IsTerminating: {IsTerminating}",
                e.ExceptionObject, e.IsTerminating);
        }

        // Note: In WinUI 3, we cannot prevent app termination for unhandled exceptions.
        // The app will restart when you click "Continue" in Visual Studio debugger
        // because the exception is still unhandled. To prevent crashes, ensure all
        // exceptions are caught and handled appropriately in their respective try-catch blocks.
    }

    protected override MauiApp CreateMauiApp() => MauiAppHolder.CreateAndStore();

    /// <summary>
    /// Handles app activation (e.g., from toast notifications, protocol handlers, etc.)
    /// This replaces the UWP OnActivated method for WinUI 3.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        // Use AppInstance to ensure only one instance of the app runs
        // This prevents opening a new instance when clicking toast notifications or alarms
        var key = "BibleAlarmInstance";
        var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var instance = AppInstance.FindOrRegisterForKey(key);

        // If this is not the main instance, redirect activation and exit
        if (!instance.IsCurrent)
        {
            logger.Information("App already running - redirecting activation to existing instance. Arguments: {Arguments}", args.Arguments);
            
            // Redirect activation to the existing instance
            instance.RedirectActivationToAsync(activatedEventArgs).AsTask().Wait();
            
            // Exit this new instance
            Environment.Exit(0);
            return;
        }

        // This is the main instance - set up activation handler
        instance.Activated += OnAppInstanceActivated;

        // Ensure MauiApp is created
        // Foreground launch - bootstrap will run on background Task
        MauiAppHolder.CreateAndStore();

        // Handle activation arguments (e.g., from toast notifications)
        if (!string.IsNullOrEmpty(args.Arguments))
        {
            HandleActivation(args.Arguments);
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
                    logger.Information("Toast activation received in existing instance: {Arguments}", toastArgs.Argument);
                    HandleActivation(toastArgs.Argument);
                }
            }
            else if (e.Kind == ExtendedActivationKind.Launch)
            {
                // Handle launch activation (e.g., from toast buttons that use foreground activation)
                var launchArgs = e.Data as LaunchActivatedEventArgs;
                if (launchArgs?.Arguments != null)
                {
                    logger.Information("Launch activation received in existing instance: {Arguments}", launchArgs.Arguments);
                    HandleActivation(launchArgs.Arguments);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling app instance activation");
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
                        // Ensure MauiApp is created
                        MauiAppHolder.CreateAndStore();

                        var alarmHandler = MauiAppHolder.Services.GetRequiredService<IWindowsAlarmHandler>();
                        await alarmHandler.HandleAsync(scheduleId, true);

                        // Reschedule the next occurrence for recurring alarms
                        // WinUI 3 doesn't have background tasks, so we reschedule immediately when notification fires
                        var schedulerService = MauiAppHolder.Services.GetRequiredService<ISchedulerService>();
                        await schedulerService.RescheduleNextOccurrenceAsync(scheduleId);
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, $"Error handling alarm activation for schedule {scheduleId}");
                    }
                });
            }
        }
        catch (Exception e)
        {
            logger.Error(e, "Error parsing activation arguments");
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
                        logger.Information("Toast notification: Next button pressed");
                        WeakReferenceMessenger.Default.Send(new NextButtonPressedMessage());
                    }
                    else if (arguments.Contains("action=previous", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Information("Toast notification: Previous button pressed");
                        WeakReferenceMessenger.Default.Send(new PreviousButtonPressedMessage());
                    }
                    else if (arguments.Contains("action=play", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Information("Toast notification: Play button pressed");
                        WeakReferenceMessenger.Default.Send(new PlayButtonPressedMessage());
                    }
                    else if (arguments.Contains("action=pause", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Information("Toast notification: Pause button pressed");
                        WeakReferenceMessenger.Default.Send(new PauseButtonPressedMessage());
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error handling media control action");
                }
            });
        }
        catch (Exception e)
        {
            logger.Error(e, "Error parsing media control action");
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
