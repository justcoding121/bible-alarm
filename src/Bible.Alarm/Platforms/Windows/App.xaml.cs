#nullable enable

using Windows.ApplicationModel.Activation;
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.Windows.Services.Handlers;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace Bible.Alarm.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        private static readonly ILogger Logger = Log.ForContext<App>();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiAppHolder.CreateAndStore(isForeground: true);

        /// <summary>
        /// Handles app activation (e.g., from toast notifications, protocol handlers, etc.)
        /// This replaces the UWP OnActivated method for WinUI 3.
        /// </summary>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            // Ensure MauiApp is created
            // Foreground launch - bootstrap will run on background Task
            MauiAppHolder.CreateAndStore(isForeground: true);

            // Handle activation arguments (e.g., from toast notifications)
            if (!string.IsNullOrEmpty(args.Arguments))
            {
                HandleActivation(args.Arguments);
            }

            // Also subscribe to AppInstance activation events for protocol handlers, etc.
            AppInstance.GetCurrent().Activated += OnAppInstanceActivated;
        }

        /// <summary>
        /// Handles app instance activation (protocol handlers, toast notifications, etc.)
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
                        HandleActivation(toastArgs.Argument);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling app instance activation");
            }
        }

        /// <summary>
        /// Handles activation arguments (e.g., schedule ID from toast notification)
        /// </summary>
        private static void HandleActivation(string arguments)
        {
            try
            {
                // Parse schedule ID from arguments
                // Format could be: "scheduleId=123" or just "123"
                int scheduleId = 0;
                if (int.TryParse(arguments.Trim(), out scheduleId) ||
                    (arguments.Contains("scheduleId=") &&
                     int.TryParse(arguments.Split('=').LastOrDefault(), out scheduleId)))
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            // Ensure MauiApp is created
                            MauiAppHolder.CreateAndStore();

                            var alarmHandler = MauiAppHolder.Services.GetRequiredService<WindowsAlarmHandler>();
                            await alarmHandler.HandleAsync(scheduleId, true);

                            // Reschedule the next occurrence for recurring alarms
                            // WinUI 3 doesn't have background tasks, so we reschedule immediately when notification fires
                            var schedulerService = MauiAppHolder.Services.GetRequiredService<Bible.Alarm.Services.Scheduler.Interfaces.ISchedulerService>();
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
}