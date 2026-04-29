#nullable enable

using System;
using Bible.Alarm.Common.Helpers;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Views.Schedule;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class ScheduleDetailsContainer : ContentView
{
    public ScheduleDetailsContainer()
    {
        InitializeComponent();
        WireUpButtonHandlers();
        WireUpEntryHandlers();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        
        // Wire up handlers after visual tree is ready
        if (Handler != null)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                WireUpButtonHandlers();
                WireUpEntryHandlers();
            });
        }
    }

    private void WireUpButtonHandlers()
    {
        // Find all buttons in the day selection FlexLayout and add Clicked handlers
        if (Content is Grid mainGrid)
        {
            foreach (var child in mainGrid.Children)
            {
                if (child is FlexLayout flexLayout)
                {
                    foreach (var flexChild in flexLayout.Children)
                    {
                        if (flexChild is Border border && border.Content is Button button)
                        {
                            button.Clicked -= OnDayButtonClicked; // Remove first to avoid duplicates
                            button.Clicked += OnDayButtonClicked;
                        }
                        else if (flexChild is Border borderWithLayout && borderWithLayout.Content is Layout borderLayout)
                        {
                            FindButtonsInLayout(borderLayout);
                        }
                    }
                }
            }
        }
    }

    private void FindButtonsInLayout(Layout layout)
    {
        foreach (var child in layout.Children)
        {
            if (child is Button button)
            {
                button.Clicked -= OnDayButtonClicked; // Remove first to avoid duplicates
                button.Clicked += OnDayButtonClicked;
            }
            else if (child is Layout childLayout)
            {
                FindButtonsInLayout(childLayout);
            }
        }
    }

    private void OnDayButtonClicked(object? sender, EventArgs e)
    {
        UnfocusScheduleNameEntry();
    }

    private void UnfocusScheduleNameEntry()
    {
        // Find parent ScheduleContent and call its unfocus method
        var parent = Parent;
        while (parent != null)
        {
            if (parent is ScheduleContent scheduleContent)
            {
                scheduleContent.UnfocusScheduleNameEntry();
                break;
            }
            parent = parent.Parent;
        }

        // Also hide keyboard directly as fallback
        if (ScheduleNameEntry?.IsFocused is true)
        {
            KeyboardHelper.HideKeyboard(ScheduleNameEntry);
        }
    }

    private void WireUpEntryHandlers()
    {
        if (ScheduleNameEntry != null)
        {
            ScheduleNameEntry.Unfocused -= OnEntryUnfocused;
            ScheduleNameEntry.Unfocused += OnEntryUnfocused;
        }
    }

    private void OnEntryUnfocused(object? sender, FocusEventArgs e)
    {
        if (ScheduleNameEntry?.IsFocused is false)
        {
            KeyboardHelper.HideKeyboard(ScheduleNameEntry);
        }
    }

    public Entry? GetScheduleNameEntry() => ScheduleNameEntry;
}

