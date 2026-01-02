#nullable enable
using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using Bible.Alarm.Stores.Models;
using Serilog;
using Action = AndroidX.Car.App.Model.Action;
using Object = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers;

/// <summary>
/// Handles template building for MainCarScreen.
/// </summary>
public sealed class CarScreenTemplateBuilder(ILogger logger)
{
    /// <summary>
    /// Builds the main template for the car screen.
    /// </summary>
    public Template BuildMainTemplate(List<ScheduleStateItem>? scheduleItems, Action refreshAction)
    {
        if (scheduleItems == null || scheduleItems.Count == 0)
        {
            return BuildEmptyTemplate(refreshAction);
        }

        var itemList = BuildItemList(scheduleItems);
        var template = new ListTemplate.Builder()
            .SetTitle("Bible Alarm")
            .SetHeaderAction(Action.AppIcon)
            .SetSingleList(itemList)
            .Build();

        logger.Debug("Built ListTemplate with {Count} schedule items", scheduleItems.Count);
        return template;
    }

    /// <summary>
    /// Builds an empty template when no schedules are available.
    /// </summary>
    private Template BuildEmptyTemplate(Action refreshAction)
    {
        var template = new PaneTemplate.Builder(
                new Pane.Builder()
                    .SetLoading(false)
                    .AddAction(refreshAction)
                    .Build())
            .SetTitle("Bible Alarm")
            .SetHeaderAction(Action.AppIcon)
            .Build();

        logger.Debug("Built empty PaneTemplate - no schedules available");
        return template;
    }

    /// <summary>
    /// Builds the item list for schedules.
    /// </summary>
    private ItemList BuildItemList(List<ScheduleStateItem> scheduleItems)
    {
        var itemListBuilder = new ItemList.Builder();

        foreach (var scheduleItem in scheduleItems)
        {
            var row = BuildScheduleRow(scheduleItem);
            itemListBuilder.AddItem(row);
        }

        return itemListBuilder.Build();
    }

    /// <summary>
    /// Builds a row for a schedule item.
    /// </summary>
    private Row BuildScheduleRow(ScheduleStateItem scheduleItem)
    {
        var title = AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem);
        var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(scheduleItem);

        var rowBuilder = new Row.Builder()
            .SetTitle(title)
            .SetOnClickListener(PendingIntentForScheduleClick(scheduleItem.Id));

        if (!string.IsNullOrEmpty(subtitle))
        {
            rowBuilder.SetTexts(subtitle);
        }

        // Add playback controls for the currently playing schedule
        if (IsCurrentlyPlaying(scheduleItem))
        {
            rowBuilder.AddAction(BuildPlayPauseAction());
        }

        return rowBuilder.Build();
    }

    /// <summary>
    /// Creates a pending intent for schedule click.
    /// </summary>
    private Object PendingIntentForScheduleClick(int scheduleId)
    {
        // Implementation would create pending intent for schedule selection
        // This is a placeholder - actual implementation would depend on the specific requirements
        return new Object();
    }

    /// <summary>
    /// Checks if a schedule is currently playing.
    /// </summary>
    private bool IsCurrentlyPlaying(ScheduleStateItem scheduleItem)
    {
        // Implementation would check if this schedule is currently playing
        // This is a placeholder
        return false;
    }

    /// <summary>
    /// Builds play/pause action for currently playing schedule.
    /// </summary>
    private Action BuildPlayPauseAction()
    {
        // Implementation would build play/pause action
        // This is a placeholder
        return Action.Pause;
    }
}
