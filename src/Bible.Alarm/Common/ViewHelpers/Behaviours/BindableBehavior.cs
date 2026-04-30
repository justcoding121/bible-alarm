namespace Bible.Alarm.Common.ViewHelpers.Behaviours;

/// <summary>
/// This base behavior class is aware of the binding context of its associated view,
/// so that when the view's binding context changes, the behavior binding context will be the same.
/// https://github.com/asimmon/Pillar/blob/master/src/Pillar/Behaviors/BindableBehavior.cs
/// </summary>
public partial class BindableBehavior<T> : Behavior<T> where T : BindableObject
{
    /// <summary>
    /// The associated Xamarin.Forms visual element
    /// </summary>
    public T AssociatedObject { get; private set; }

    /// <inheritdoc />
    protected override void OnAttachedTo(T bindable)
    {
        base.OnAttachedTo(bindable);

        AssociatedObject = bindable;

        if (bindable.BindingContext != null)
        {
            BindingContext = bindable.BindingContext;
        }

        bindable.BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object sender, EventArgs e) => OnBindingContextChanged();

    /// <inheritdoc />
    protected override void OnDetachingFrom(T bindable) => bindable.BindingContextChanged -= OnBindingContextChanged;

    /// <summary>
    /// Track any changes of the view's binding context
    /// </summary>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        BindingContext = AssociatedObject.BindingContext;
    }
}
