#nullable enable

using System;
using Syncfusion.Maui.Core;

namespace Bible.Alarm.Views.Schedule;

public partial class AlarmSettingsContainer : ContentView
{
    public AlarmSettingsContainer()
    {
        InitializeComponent();
        WireUpSwitchHandlers();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        
        // Wire up handlers after visual tree is ready
        if (Handler != null)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
            {
                WireUpSwitchHandlers();
            });
        }
    }

    private void WireUpSwitchHandlers()
    {
        // Start traversal from Content property, not the ContentView itself
        if (Content is View content)
        {
            FindAndWireSwitches(content);
        }
    }

    private void FindAndWireSwitches(View view)
    {
        if (view == null)
        {
            return;
        }

        if (view is Shared.PlatformSwitch platformSwitch)
        {
            platformSwitch.PropertyChanged -= OnSwitchPropertyChanged; // Remove first to avoid duplicates
            platformSwitch.PropertyChanged += OnSwitchPropertyChanged;
            return;
        }

        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is View childView)
                {
                    FindAndWireSwitches(childView);
                }
            }
        }
        else if (view is ContentView contentView && contentView.Content is View content)
        {
            FindAndWireSwitches(content);
        }
        else if (view is Border border && border.Content is View borderContent)
        {
            FindAndWireSwitches(borderContent);
        }
        else if (view is SfEffectsView sfEffectsView && sfEffectsView.Content is View sfContent)
        {
            FindAndWireSwitches(sfContent);
        }
    }

    private void OnSwitchPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Shared.PlatformSwitch.IsToggled))
        {
            UnfocusScheduleNameEntry();
        }
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
    }
}
