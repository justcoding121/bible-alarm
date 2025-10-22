using System.Linq.Expressions;
using Bible.Alarm.Common.Mvvm.Commands;

namespace Bible.Alarm.Common.Mvvm.Assignments;

/// <summary>
/// The result of an assignment of an Observable property.
/// </summary>
public class Assignement<TObservable, T>(TObservable owner, string property, T oldValue, T newValue)
    where TObservable : VmObservable
{
    /// <summary>
    /// Gets the Obserbable object that has the updated property.
    /// </summary>
    /// <value>The owner.</value>
    public TObservable Owner { get; } = owner;

    /// <summary>
    /// Gets the name of the property that has been assigned.
    /// </summary>
    /// <value>The name of the property.</value>
    public string PropertyName { get; } = property;

    /// <summary>
    /// Gets a value indicating whether the new value is different from the old one.
    /// </summary>
    /// <value><c>true</c> if has changed; otherwise, <c>false</c>.</value>
    public bool HasChanged { get; } = !EqualityComparer<T>.Default.Equals(oldValue, newValue);

    /// <summary>
    /// Gets the old value.
    /// </summary>
    /// <value>The old value.</value>
    public T OldValue { get; } = oldValue;

    /// <summary>
    /// Gets the new value.
    /// </summary>
    /// <value>The new value.</value>
    public T NewValue { get; } = newValue;

    /// <summary>
    /// If the value has changed, then raise a set of other property.
    /// </summary>
    /// <returns>The raise.</returns>
    public Assignement<TObservable, T> ThenRaise<TProperty>(Expression<Func<TProperty>> property)
    {
        if (HasChanged)
        {
            var expression = (MemberExpression)property.Body;
            var propertyName = expression.Member.Name;
            Owner.RaiseProperties(propertyName);
        }

        return this;
    }

    /// <summary>
    /// If the value has changed, then raise a set of commands.
    /// </summary>
    /// <returns>The raise can execute changed.</returns>
    /// <param name="commands">Commands.</param>
    public Assignement<TObservable, T> ThenRaiseCanExecuteChanged(params IRelayCommand[] commands)
    {
        if (HasChanged)
            foreach (var c in commands)
                c.RaiseCanExecuteChanged();

        return this;
    }
}