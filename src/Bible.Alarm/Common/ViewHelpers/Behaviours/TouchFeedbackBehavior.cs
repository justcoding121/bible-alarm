#nullable enable
namespace Bible.Alarm.Common.ViewHelpers.Behaviours;

/// <summary>
/// Behavior that adds touch feedback animation to views when tapped
/// </summary>
public class TouchFeedbackBehavior : Behavior<View>
{
    private View? _associatedView;

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        _associatedView = bindable;
        
        // Find TapGestureRecognizer and add Tapped event handler
        if (bindable.GestureRecognizers != null)
        {
            foreach (var gesture in bindable.GestureRecognizers)
            {
                if (gesture is TapGestureRecognizer tapGesture)
                {
                    tapGesture.Tapped += OnTapped;
                }
            }
        }
    }

    protected override void OnDetachingFrom(View bindable)
    {
        if (bindable.GestureRecognizers != null)
        {
            foreach (var gesture in bindable.GestureRecognizers)
            {
                if (gesture is TapGestureRecognizer tapGesture)
                {
                    tapGesture.Tapped -= OnTapped;
                }
            }
        }
        _associatedView = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (_associatedView != null)
        {
            AnimateUtils.AnimateTouchFeedback(_associatedView);
        }
    }
}

