#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.iOS.Services.CarPlay.Interfaces;
using CarPlay;
using Foundation;
using Serilog;
using UIKit;

namespace Bible.Alarm.Platforms.iOS.Services.CarPlay;

/// <summary>
/// CarPlay Scene Delegate for Bible Alarm.
/// Handles CarPlay connection/disconnection and manages the schedule list template.
/// 
/// Shows a list of schedules that users can tap to start playback, similar to Android Auto.
/// 
/// NOTE: This requires the com.apple.developer.carplay-audio entitlement from Apple MFi portal.
/// Without the entitlement, this delegate will never be called (CarPlay won't connect to the app).
/// </summary>
[Register("CarPlaySceneDelegate")]
[Preserve(AllMembers = true)]
public class CarPlaySceneDelegate : UIResponder, ICPTemplateApplicationSceneDelegate
{
    private static readonly ILogger logger = Log.ForContext<CarPlaySceneDelegate>();

    private CPInterfaceController? interfaceController;
    private CPListTemplate? scheduleListTemplate;

    /// <summary>
    /// Static flag indicating if CarPlay is currently connected.
    /// </summary>
    public static bool IsCarPlayConnected { get; private set; }

    /// <summary>
    /// True for a short window after CarPlay connects. Used to suppress iOS-initiated
    /// auto-play commands that fire when the car head unit discovers a "now playing" app.
    /// </summary>
    public static bool IsRecentlyConnected { get; private set; }

    /// <summary>
    /// Static reference to the current delegate instance.
    /// Used by effects to refresh the schedule list when schedules change.
    /// </summary>
    public static CarPlaySceneDelegate? Current { get; private set; }

    /// <summary>
    /// Called when CarPlay connects to the app.
    /// </summary>
    [Export("templateApplicationScene:didConnectInterfaceController:")]
    public void DidConnect(CPTemplateApplicationScene scene, CPInterfaceController controller)
    {
        try
        {
            logger.Information("[CarPlay] Connected to CarPlay interface controller");
            interfaceController = controller;
            IsCarPlayConnected = true;
            IsRecentlyConnected = true;
            Current = this;

            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                IsRecentlyConnected = false;
                logger.Debug("[CarPlay] Cleared IsRecentlyConnected flag (auto-play suppression window ended)");
            });

            // Create and set the schedule list template
            SetRootTemplate();

            var rotationService = ServiceProviderManager.GetService<ICarPlayDefaultScheduleRotationService>();
            if (rotationService != null)
            {
                rotationService.Start();
            }
            else
            {
                logger.Debug("[CarPlay] Rotation service not available (DI may not be ready)");
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[CarPlay] Error during CarPlay connection");
        }
    }

    /// <summary>
    /// Called when CarPlay disconnects from the app.
    /// </summary>
    [Export("templateApplicationScene:didDisconnectInterfaceController:")]
    public void DidDisconnect(CPTemplateApplicationScene scene, CPInterfaceController controller)
    {
        try
        {
            logger.Information("[CarPlay] Disconnected from CarPlay interface controller");
            IsCarPlayConnected = false;
            Current = null;

            var oldController = interfaceController;
            var oldTemplate = scheduleListTemplate;
            interfaceController = null;
            scheduleListTemplate = null;

            SuppressCarPlayFinalizers(oldController, oldTemplate);

            try
            {
                var rotationService = ServiceProviderManager.GetService<ICarPlayDefaultScheduleRotationService>();
                rotationService?.Stop();
            }
            catch (Exception rotationEx)
            {
                logger.Warning(rotationEx, "[CarPlay] Failed to stop rotation service");
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "[CarPlay] Error during CarPlay disconnection");
        }
    }

    private static void SuppressCarPlayFinalizers(CPInterfaceController? controller, CPListTemplate? template)
    {
        try
        {
            if (controller != null)
            {
                GC.SuppressFinalize(controller);
            }

            if (template == null)
            {
                return;
            }

            GC.SuppressFinalize(template);

            foreach (var section in template.Sections)
            {
                GC.SuppressFinalize(section);

                foreach (var item in section.Items2)
                {
                    GC.SuppressFinalize(item);
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            logger.Debug(ex, "[CarPlay] SuppressCarPlayFinalizers: disposed during teardown (non-fatal)");
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "[CarPlay] SuppressCarPlayFinalizers: unexpected (non-fatal)");
        }
    }

    /// <summary>
    /// Called when the CarPlay scene is about to connect to a session with a window.
    /// </summary>
    [Export("templateApplicationScene:didConnectInterfaceController:toWindow:")]
    public void DidConnect(CPTemplateApplicationScene scene, CPInterfaceController controller, CPWindow window)
    {
        DidConnect(scene, controller);
    }

    /// <summary>
    /// Sets the root template with the schedule list.
    /// </summary>
    private void SetRootTemplate()
    {
        if (interfaceController == null)
        {
            logger.Warning("[CarPlay] Cannot set root template - interface controller is null");
            return;
        }

        try
        {
            scheduleListTemplate = CreateScheduleListTemplate();

            interfaceController.SetRootTemplate(scheduleListTemplate, animated: true, completion: (success, error) =>
            {
                if (success)
                {
                    logger.Information("[CarPlay] Successfully set schedule list as root template");
                }
                else
                {
                    logger.Warning("[CarPlay] Failed to set root template: {Error}", error?.LocalizedDescription ?? "Unknown error");
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[CarPlay] Error setting root template");
        }
    }

    /// <summary>
    /// Creates the schedule list template with all available schedules.
    /// </summary>
    private CPListTemplate CreateScheduleListTemplate()
    {
        var schedules = CarPlayScheduleHelper.LoadScheduleStateItemsFromState();

        if (schedules.Count == 0)
        {
            logger.Warning("[CarPlay] No schedules in state - showing loading/empty state");
            return CreateEmptyStateTemplate();
        }

        var listItems = new List<ICPListTemplateItem>();

        foreach (var schedule in schedules)
        {
            var title = CarPlayScheduleHelper.BuildScheduleTitle(schedule);
            var subtitle = CarPlayScheduleHelper.BuildScheduleSubtitle(schedule);
            var scheduleId = schedule.Id;

            var listItem = new CPListItem(title, subtitle);

            listItem.Handler = (item, completion) =>
            {
                logger.Information("[CarPlay] User tapped schedule: {Title} (ID: {ScheduleId})", title, scheduleId);
                CarPlayPlaybackHandler.HandleScheduleItemClicked(scheduleId, completion);
            };

            listItems.Add(listItem);
        }

        // Create a section with all schedules
        var section = new CPListSection(listItems.ToArray(), "Schedules", null);

        // Create the list template with title and sections array
        var sections = new CPListSection[] { section };
        var template = new CPListTemplate("Bible Alarm", sections);

        logger.Information("[CarPlay] Created schedule list template with {Count} schedules", schedules.Count);

        return template;
    }

    /// <summary>
    /// Creates a loading/empty state template shown when schedules are not yet loaded or unavailable.
    /// Replaced by the actual schedule list once bootstrap completes and state is populated.
    /// </summary>
    private static CPListTemplate CreateEmptyStateTemplate()
    {
        var emptyItem = new CPListItem("Loading schedules…", "Schedules will appear once the app is ready");
        emptyItem.Handler = (item, completion) =>
        {
            // Do nothing on tap - just complete the handler
            completion();
        };

        var items = new ICPListTemplateItem[] { emptyItem };
        var section = new CPListSection(items, null, null);
        var sections = new CPListSection[] { section };
        return new CPListTemplate("Bible Alarm", sections);
    }

    /// <summary>
    /// Refreshes the schedule list template.
    /// Call this when schedules are added, removed, or modified.
    /// </summary>
    public void RefreshScheduleList()
    {
        if (!IsCarPlayConnected || interfaceController == null)
        {
            return;
        }

        try
        {
            if (scheduleListTemplate != null)
            {
                var oldSections = scheduleListTemplate.Sections;

                var schedules = CarPlayScheduleHelper.LoadScheduleStateItemsFromState();
                var listItems = new List<ICPListTemplateItem>();

                foreach (var schedule in schedules)
                {
                    var title = CarPlayScheduleHelper.BuildScheduleTitle(schedule);
                    var subtitle = CarPlayScheduleHelper.BuildScheduleSubtitle(schedule);
                    var scheduleId = schedule.Id;

                    var listItem = new CPListItem(title, subtitle);
                    listItem.Handler = (item, completion) =>
                    {
                        CarPlayPlaybackHandler.HandleScheduleItemClicked(scheduleId, completion);
                    };

                    listItems.Add(listItem);
                }

                var section = new CPListSection(listItems.ToArray(), "Schedules", null);
                var sections = new CPListSection[] { section };
                scheduleListTemplate.UpdateSections(sections);

                SuppressOldSectionFinalizers(oldSections);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[CarPlay] Error refreshing schedule list");
        }
    }

    private static void SuppressOldSectionFinalizers(CPListSection[] oldSections)
    {
        try
        {
            foreach (var section in oldSections)
            {
                foreach (var item in section.Items2)
                {
                    GC.SuppressFinalize(item);
                }

                GC.SuppressFinalize(section);
            }
        }
        catch (ObjectDisposedException ex)
        {
            logger.Debug(ex, "[CarPlay] SuppressOldSectionFinalizers: disposed during teardown (non-fatal)");
        }
        catch (Exception ex)
        {
            logger.Debug(ex, "[CarPlay] SuppressOldSectionFinalizers: unexpected (non-fatal)");
        }
    }
}