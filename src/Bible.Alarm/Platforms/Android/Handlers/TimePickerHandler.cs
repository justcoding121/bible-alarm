#nullable enable
using System.ComponentModel;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Util;
using Android.Widget;
using AndroidX.AppCompat.App;
using Bible.Alarm.Services.UI;
using Bible.Alarm.Services.UI.Interfaces;
using Microsoft.Maui.Platform;
using Color = Android.Graphics.Color;

namespace Bible.Alarm.Platforms.Android.Handlers;

public class TimePickerHandler : Microsoft.Maui.Handlers.TimePickerHandler
{
    private TimePickerDialog? currentDialog;
    private INotifyPropertyChanged? fontServiceNotifier;
    private bool isShowingDialog;

    protected override MauiTimePicker CreatePlatformView()
    {
        var view = base.CreatePlatformView();
        // Remove the underline and native background so the MAUI Border background shows through
        view.BackgroundTintList = ColorStateList.ValueOf(Color.Transparent);
        view.SetBackgroundColor(Color.Transparent);
        return view;
    }

    protected override void ConnectHandler(MauiTimePicker platformView)
    {
        // Set up our click handler BEFORE calling base to ensure it's registered first
        platformView.Click -= OnPlatformViewClick;
        platformView.Click += OnPlatformViewClick;
        
        base.ConnectHandler(platformView);
        
        // Ensure underline and native background are removed
        platformView.BackgroundTintList = ColorStateList.ValueOf(Color.Transparent);
        platformView.SetBackgroundColor(Color.Transparent);
        
        // Get FontService to listen for font size changes
        var fontService = MauiContext?.Services?.GetService<IFontService>();
        if (fontService is INotifyPropertyChanged notifier)
        {
            fontServiceNotifier = notifier;
            fontServiceNotifier.PropertyChanged += OnFontSizeChanged;
        }
        
        // Re-apply our click handler after base.ConnectHandler (in case base overwrote it)
        platformView.Click -= OnPlatformViewClick;
        platformView.Click += OnPlatformViewClick;
        
        // Make it focusable and clickable - this ensures first tap works immediately
        platformView.Focusable = true;
        platformView.FocusableInTouchMode = true;
        platformView.Clickable = true;
        
        // Also handle touch events to ensure immediate response
        platformView.Touch += OnPlatformViewTouch;
    }

    protected override void DisconnectHandler(MauiTimePicker platformView)
    {
        platformView.Click -= OnPlatformViewClick;
        platformView.Touch -= OnPlatformViewTouch;
        
        // Unsubscribe from font size changes
        if (fontServiceNotifier != null)
        {
            fontServiceNotifier.PropertyChanged -= OnFontSizeChanged;
            fontServiceNotifier = null;
        }
        
        // Clear dialog reference
        currentDialog = null;
        
        base.DisconnectHandler(platformView);
    }

    private void OnPlatformViewTouch(object? sender, global::Android.Views.View.TouchEventArgs e)
    {
        // If it's a down action, immediately show the dialog to avoid double-tap
        // This ensures the dialog opens on first tap without needing focus first
        if (e.Event?.Action == global::Android.Views.MotionEventActions.Down && 
            VirtualView != null && 
            PlatformView?.Context != null)
        {
            // Show dialog immediately on touch down
            ShowTimePickerDialog();
            e.Handled = true;
        }
    }

    private void ShowTimePickerDialog()
    {
        // Prevent showing dialog multiple times if both touch and click events fire
        if (isShowingDialog || VirtualView == null || PlatformView?.Context == null)
            return;

        isShowingDialog = true;

        var context = PlatformView.Context;
        var currentTime = VirtualView.Time ?? TimeSpan.Zero;
        
        // Get font size from FontService (scaled with system font size)
        var fontSize = FontServiceHelper.TitleFontSize;
        
        // Determine 12/24 hour format based on VirtualView.Format
        var is24Hour = !VirtualView.Format.Contains("tt", StringComparison.OrdinalIgnoreCase);
        
        // Create TimePickerDialog - Android will automatically use the theme from MainTheme
        // MainTheme specifies android:timePickerDialogTheme which switches between light/dark automatically
        var dialog = new TimePickerDialog(
            context,
            (sender, args) =>
            {
                if (VirtualView != null)
                {
                    VirtualView.Time = new TimeSpan(args.HourOfDay, args.Minute, 0);
                }
                // Clear dialog reference when dismissed
                currentDialog = null;
            },
            currentTime.Hours,
            currentTime.Minutes,
            is24Hour);
        
        // Store dialog reference so we can update font size if it changes while dialog is open
        currentDialog = dialog;
        
        // Clear reference when dialog is dismissed (handles back button, outside tap, etc.)
        dialog.DismissEvent += (sender, args) =>
        {
            currentDialog = null;
            isShowingDialog = false;
        };
        
        dialog.Show();
        
        // Apply font size to buttons and AM/PM text after dialog is shown
        // Use multiple attempts to ensure views are available
        ApplyDialogFontSizesDelayed(dialog, fontSize);
    }

    private void OnFontSizeChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update dialog buttons and AM/PM if dialog is currently open and font size changed
        if (currentDialog != null && (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(IFontService.TitleFontSize)))
        {
            var fontSize = FontServiceHelper.TitleFontSize;
            ApplyDialogFontSizesDelayed(currentDialog, fontSize);
        }
    }

    private void OnPlatformViewClick(object? sender, EventArgs e)
    {
        // Show dialog on click (fallback if touch handler doesn't fire)
        ShowTimePickerDialog();
    }

    private static void ApplyDialogFontSizesDelayed(TimePickerDialog dialog, double fontSize)
    {
        // Try immediately
        ApplyDialogFontSizes(dialog, fontSize);
        
        // Also try after a short delay to ensure dialog is fully rendered
        if (dialog.Window?.DecorView != null)
        {
            dialog.Window.DecorView.Post(() =>
            {
                ApplyDialogFontSizes(dialog, fontSize);
            });
            
            // Try again after a slightly longer delay
            dialog.Window.DecorView.PostDelayed(() =>
            {
                ApplyDialogFontSizes(dialog, fontSize);
            }, 100);
        }
    }

    private static void ApplyDialogFontSizes(TimePickerDialog dialog, double fontSize)
    {
        try
        {
            // Apply font size to dialog buttons
            var positiveButton = dialog.GetButton(-1); // DialogButtonType.Positive
            var negativeButton = dialog.GetButton(-2); // DialogButtonType.Negative
            
            if (positiveButton != null)
            {
                positiveButton.SetTextSize(ComplexUnitType.Sp, (float)fontSize);
            }
            
            if (negativeButton != null)
            {
                negativeButton.SetTextSize(ComplexUnitType.Sp, (float)fontSize);
            }
            
            // Apply font size to AM/PM text in TimePicker
            ApplyAmPmFontSize(dialog, fontSize);
        }
        catch (Exception)
        {
            // Views might not be available yet, will retry via delayed calls
        }
    }

    private static void ApplyAmPmFontSize(TimePickerDialog dialog, double fontSize)
    {
        try
        {
            var dialogView = dialog.Window?.DecorView;
            if (dialogView == null)
                return;

            var timePicker = FindTimePicker(dialogView);
            if (timePicker is null)
                return;

            TryApplyAmPmFontSizeToTimePicker(dialog.Context!, timePicker, fontSize);
        }
        catch (Exception)
        {
            // AM/PM views might not be available or accessible
        }
    }

    private static void TryApplyAmPmFontSizeToTimePicker(
        global::Android.Content.Context context,
        global::Android.Views.View timePicker,
        double fontSize)
    {
        if (timePicker is not global::Android.Views.ViewGroup timePickerGroup)
        {
            return;
        }

        if (!TryApplyMaterialAmPmLabelFontSizes(context, timePickerGroup, fontSize))
        {
            FindAndStyleAmPmTextViews(timePicker, fontSize);
        }
    }

    private static bool TryApplyMaterialAmPmLabelFontSizes(global::Android.Content.Context context, global::Android.Views.ViewGroup timePickerGroup, double fontSize)
    {
        var amPmId = context.Resources?.GetIdentifier("material_timepicker_am_label", "id", context.PackageName);
        if (amPmId.HasValue && amPmId.Value != 0)
        {
            var amPmView = timePickerGroup.FindViewById(amPmId.Value);
            if (amPmView is TextView amPmTextView)
            {
                amPmTextView.SetTextSize(ComplexUnitType.Sp, (float)fontSize);
            }
        }

        var pmId = context.Resources?.GetIdentifier("material_timepicker_pm_label", "id", context.PackageName);
        if (pmId.HasValue && pmId.Value != 0)
        {
            var pmView = timePickerGroup.FindViewById(pmId.Value);
            if (pmView is TextView pmTextView)
            {
                pmTextView.SetTextSize(ComplexUnitType.Sp, (float)fontSize);
                return true;
            }
        }

        return amPmId.HasValue && amPmId.Value != 0;
    }

    private static global::Android.Widget.TimePicker? FindTimePicker(global::Android.Views.View root)
    {
        if (root is global::Android.Widget.TimePicker timePicker)
        {
            return timePicker;
        }

        if (root is global::Android.Views.ViewGroup viewGroup)
        {
            for (int i = 0; i < viewGroup.ChildCount; i++)
            {
                var child = viewGroup.GetChildAt(i);
                if (child is not null)
                {
                    var found = FindTimePicker(child);
                    if (found is not null)
                        return found;
                }
            }
        }

        return null;
    }

    private static void FindAndStyleAmPmTextViews(global::Android.Views.View root, double fontSize)
    {
        if (root is TextView textView)
        {
            var text = textView.Text;
            if (!string.IsNullOrEmpty(text) && (text.Equals("AM", StringComparison.OrdinalIgnoreCase) || 
                                                 text.Equals("PM", StringComparison.OrdinalIgnoreCase)))
            {
                textView.SetTextSize(ComplexUnitType.Sp, (float)fontSize);
            }
        }

        if (root is global::Android.Views.ViewGroup viewGroup)
        {
            for (int i = 0; i < viewGroup.ChildCount; i++)
            {
                var child = viewGroup.GetChildAt(i);
                if (child is not null)
                {
                    FindAndStyleAmPmTextViews(child, fontSize);
                }
            }
        }
    }
}

