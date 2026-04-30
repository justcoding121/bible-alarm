#nullable enable
using Bible.Alarm.Common;
using Bible.Alarm.Platforms.iOS.Services.CarPlay.Interfaces;
using Bible.Alarm.Shared.Constants;
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

    private CPInterfaceController? cpInterfaceController;
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
    public void DidConnect(CPTemplateApplicationScene templateApplicationScene, CPInterfaceController interfaceController)
    {
        try
        {
            logger.Information(AppConstants.Logging.CarPlayDiagnosticsLog.ConnectedToInterfaceController);
            cpInterfaceController = interfaceController;
            IsCarPlayConnected = true;
            IsRecentlyConnected = true;
            Current = this;

            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                IsRecentlyConnected = false;
                logger.Debug(AppConstants.Logging.CarPlayDiagnosticsLog.ClearedIsRecentlyConnectedFlag);
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
                logger.Debug(AppConstants.Logging.CarPlayDiagnosticsLog.RotationServiceNotAvailable);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.CarPlayDiagnosticsLog.ErrorDuringCarPlayConnection);
        }
    }

    /// <summary>
    /// Called when the CarPlay scene is about to connect to a session with a window.
    /// </summary>
    [Export("templateApplicationScene:didConnectInterfaceController:toWindow:")]
    public void DidConnect(CPTemplateApplicationScene templateApplicationScene, CPInterfaceController interfaceController, CPWindow window)
    {
        DidConnect(templateApplicationScene, interfaceController);
    }

    /// <summary>
    /// Called when CarPlay disconnects from the app.
    /// </summary>
    [Export("templateApplicationScene:didDisconnectInterfaceController:")]
    public void DidDisconnect(CPTemplateApplicationScene templateApplicationScene, CPInterfaceController interfaceController)
    {
        try
        {
            logger.Information(AppConstants.Logging.CarPlayDiagnosticsLog.DisconnectedFromInterfaceController);
            IsCarPlayConnected = false;
            Current = null;

            var oldController = cpInterfaceController;
            var oldTemplate = scheduleListTemplate;
            cpInterfaceController = null;
            scheduleListTemplate = null;

            SuppressCarPlayFinalizers(oldController, oldTemplate);

            try
            {
                var rotationService = ServiceProviderManager.GetService<ICarPlayDefaultScheduleRotationService>();
                rotationService?.Stop();
            }
            catch (Exception rotationEx)
            {
                logger.Warning(rotationEx, AppConstants.Logging.CarPlayDiagnosticsLog.FailedToStopRotationService);
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.CarPlayDiagnosticsLog.ErrorDuringCarPlayDisconnection);
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
            logger.Debug(ex, AppConstants.Logging.CarPlayDiagnosticsLog.SuppressCarPlayFinalizersDisposedDuringTeardown);
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.CarPlayDiagnosticsLog.SuppressCarPlayFinalizersUnexpected);
        }
    }

    /// <summary>
    /// Sets the root template with the schedule list.
    /// </summary>
    private void SetRootTemplate()
    {
        if (cpInterfaceController == null)
        {
            logger.Warning(AppConstants.Logging.CarPlayDiagnosticsLog.CannotSetRootTemplateInterfaceControllerNull);
            return;
        }

        try
        {
            scheduleListTemplate = CreateScheduleListTemplate();

            cpInterfaceController.SetRootTemplate(scheduleListTemplate, animated: true, completion: (success, error) =>
            {
                if (success)
                {
                    logger.Information(AppConstants.Logging.CarPlayDiagnosticsLog.SuccessfullySetScheduleListAsRootTemplate);
                }
                else
                {
                    logger.Warning(AppConstants.Logging.CarPlayDiagnosticsLog.FailedToSetRootTemplateWithError, error?.LocalizedDescription ?? AppConstants.Logging.UnknownErrorFallback);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.CarPlayDiagnosticsLog.ErrorSettingRootTemplate);
        }
    }

    /// <summary>
    /// Creates the schedule list template with all available schedules.
    /// </summary>
    private static CPListTemplate CreateScheduleListTemplate()
    {
        var schedules = CarPlayScheduleHelper.LoadScheduleStateItemsFromState();

        if (schedules.Count == 0)
        {
            logger.Warning(AppConstants.Logging.CarPlayDiagnosticsLog.NoSchedulesInStateShowingEmpty);
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
                logger.Information(AppConstants.Logging.CarPlayDiagnosticsLog.UserTappedSchedule, title, scheduleId);
                CarPlayPlaybackHandler.HandleScheduleItemClicked(scheduleId, completion);
            };

            listItems.Add(listItem);
        }

        // Create a section with all schedules
        var section = new CPListSection(listItems.ToArray(), AppConstants.CarPlayScheduleList.SectionTitleSchedules, null);

        // Create the list template with title and sections array
        var sections = new CPListSection[] { section };
        var template = new CPListTemplate(AppConstants.AppSettings.ApplicationDisplayName, sections);

        logger.Information(AppConstants.Logging.CarPlayDiagnosticsLog.CreatedScheduleListTemplateWithCount, schedules.Count);

        return template;
    }

    /// <summary>
    /// Creates a loading/empty state template shown when schedules are not yet loaded or unavailable.
    /// Replaced by the actual schedule list once bootstrap completes and state is populated.
    /// </summary>
    private static CPListTemplate CreateEmptyStateTemplate()
    {
        var emptyItem = new CPListItem(
            AppConstants.CarPlayScheduleList.LoadingPrimaryText,
            AppConstants.CarPlayScheduleList.LoadingSecondaryText);
        emptyItem.Handler = (item, completion) =>
        {
            // Do nothing on tap - just complete the handler
            completion();
        };

        var items = new ICPListTemplateItem[] { emptyItem };
        var section = new CPListSection(items, null, null);
        var sections = new CPListSection[] { section };
        return new CPListTemplate(AppConstants.AppSettings.ApplicationDisplayName, sections);
    }

    /// <summary>
    /// Refreshes the schedule list template.
    /// Call this when schedules are added, removed, or modified.
    /// </summary>
    public void RefreshScheduleList()
    {
        if (!IsCarPlayConnected || cpInterfaceController == null)
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

                var section = new CPListSection(listItems.ToArray(), AppConstants.CarPlayScheduleList.SectionTitleSchedules, null);
                var sections = new CPListSection[] { section };
                scheduleListTemplate.UpdateSections(sections);

                SuppressOldSectionFinalizers(oldSections);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, AppConstants.Logging.CarPlayDiagnosticsLog.ErrorRefreshingScheduleList);
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
            logger.Debug(ex, AppConstants.Logging.CarPlayDiagnosticsLog.SuppressOldSectionFinalizersDisposedDuringTeardown);
        }
        catch (Exception ex)
        {
            logger.Debug(ex, AppConstants.Logging.CarPlayDiagnosticsLog.SuppressOldSectionFinalizersUnexpected);
        }
    }
}