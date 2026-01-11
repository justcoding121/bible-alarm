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
    public AndroidX.Car.App.Model.ITemplate BuildMainTemplate(List<ScheduleStateItem>? scheduleItems, Action refreshAction, Screen screen)
    {
        if (scheduleItems == null || scheduleItems.Count == 0)
        {
            return BuildEmptyTemplate(refreshAction);
        }

        var itemList = BuildItemList(scheduleItems, screen);
        var headerBuilder = new Header.Builder();
        var header = headerBuilder?
            .SetTitle("Bible Alarm")?
            .SetStartHeaderAction(Action.AppIcon)?
            .Build() ?? throw new InvalidOperationException("Failed to build Header");
        var templateBuilder = new ListTemplate.Builder();
        var template = templateBuilder?
            .SetHeader(header)?
            .SetSingleList(itemList)?
            .Build();

        logger.Debug("Built ListTemplate with {Count} schedule items", scheduleItems.Count);
        return template ?? throw new InvalidOperationException("Failed to build ListTemplate");
    }

    /// <summary>
    /// Builds an empty template when no schedules are available.
    /// </summary>
    private AndroidX.Car.App.Model.ITemplate BuildEmptyTemplate(Action refreshAction)
    {
        var headerBuilder = new Header.Builder();
        var header = headerBuilder?
            .SetTitle("Bible Alarm")?
            .SetStartHeaderAction(Action.AppIcon)?
            .Build() ?? throw new InvalidOperationException("Failed to build Header");
        var paneBuilder = new Pane.Builder();
        var pane = paneBuilder?
            .SetLoading(false)?
            .AddAction(refreshAction)?
            .Build() ?? throw new InvalidOperationException("Failed to build Pane");
        var templateBuilder = new PaneTemplate.Builder(pane);
        var template = templateBuilder?
            .SetHeader(header)?
            .Build();

        logger.Debug("Built empty PaneTemplate - no schedules available");
        return template ?? throw new InvalidOperationException("Failed to build PaneTemplate");
    }

    /// <summary>
    /// Builds the item list for schedules.
    /// </summary>
    private ItemList BuildItemList(List<ScheduleStateItem> scheduleItems, Screen screen)
    {
        var itemListBuilder = new ItemList.Builder();

        foreach (var scheduleItem in scheduleItems)
        {
            var row = BuildScheduleRow(scheduleItem, screen);
            itemListBuilder.AddItem(row);
        }

        return itemListBuilder.Build() ?? throw new InvalidOperationException("Failed to build ItemList");
    }

    /// <summary>
    /// Builds a row for a schedule item.
    /// </summary>
    private Row BuildScheduleRow(ScheduleStateItem scheduleItem, Screen screen)
    {
        var title = AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem);
        var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(scheduleItem);

        var mainCarScreen = screen as MainCarScreen;
        var rowBuilder = new Row.Builder();

        // Start the chain with SetTitle
        var rowWithTitle = rowBuilder?.SetTitle(title);

        // Conditionally add click listener
        if (rowWithTitle != null && mainCarScreen != null)
        {
            rowWithTitle.SetOnClickListener(new ScheduleRowClickCallback(mainCarScreen, scheduleItem.Id));
        }

        // Conditionally add subtitle
        if (rowWithTitle != null && !string.IsNullOrEmpty(subtitle))
        {
            rowWithTitle.AddText(subtitle);
        }

        // Conditionally add playback controls
        if (rowWithTitle != null && IsCurrentlyPlaying(scheduleItem))
        {
            rowWithTitle.AddAction(BuildPlayPauseAction());
        }

        return rowWithTitle?
            .Build() ?? throw new InvalidOperationException("Failed to build Row");
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
        // This is a placeholder - would need to create proper Action with callback
        // Note: CarIcon.Pause doesn't exist, using AppIcon as placeholder
        var actionBuilder = new Action.Builder();
        return actionBuilder?
            .SetTitle("Pause")?
            .SetIcon(CarIcon.AppIcon)?
            .Build() ?? throw new InvalidOperationException("Failed to build play/pause action");
    }
}

/// <summary>
/// Click callback for schedule items in the Car App list.
/// </summary>
internal class ScheduleRowClickCallback(MainCarScreen screen, int scheduleId) : Object, IOnClickListener
{
    private static readonly ILogger logger = Log.ForContext<ScheduleRowClickCallback>();

    public void OnClick()
    {
        try
        {
            logger.Information("Schedule {ScheduleId} clicked - starting playback", scheduleId);
            screen.OnScheduleItemClicked(scheduleId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error handling schedule click for schedule {ScheduleId}", scheduleId);
        }
    }
}
