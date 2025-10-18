namespace Mvvmicro;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;

/// <summary>
/// Subscribes to Observable property changes according to a viable application lifecycle.
/// </summary>
public class NotifyPropertyObserver<TObserver, TObservable>
    where TObservable : INotifyPropertyChanged
    where TObserver : class
{
    #region Constructors

    public NotifyPropertyObserver(TObservable observable, TObserver observer)
    {
        _observable = observable;
        _observable.PropertyChanged += OnPropertyChanged;
        _observer = new WeakReference<TObserver>(observer);
    }

    #endregion

    #region Fields

    private bool _hasBeenActive;

    private TObservable _observable;

    private WeakReference<TObserver> _observer;

    private Dictionary<string, Action> _propertyObservers = new();

    private HashSet<string> _pendingChanges = new();

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets a value indicating whether all property observers should be triggered at first registration (default: true).
    /// </summary>
    /// <value><c>true</c> if should trigger initial values; otherwise, <c>false</c>.</value>
    public bool ShouldTriggerInitialValues { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether all changes (while inactive) should be queued and triggers when becoming active again.
    /// </summary>
    /// <value><c>true</c> if should trigger pending changes; otherwise, <c>false</c>.</value>
    public bool ShouldTriggerPendingChanges { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether this <see cref="T:Mvvmicro.NotifyPropertyObserver`2"/> is active.
    /// </summary>
    /// <value><c>true</c> if is active; otherwise, <c>false</c>.</value>
    public bool IsActive { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Observe the specified property.
    /// </summary>
    /// <returns>The observe.</returns>
    /// <param name="property">Property.</param>
    /// <param name="whenChanged">When changed.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public NotifyPropertyObserver<TObserver, TObservable> Observe<T>(Expression<Func<TObservable, T>> property,
        Action<TObservable, T> whenChanged)
    {
        if (IsActive)
            throw new InvalidOperationException("Property observers can only be configured before activation.");

        var expression = (MemberExpression)property.Body;
        var propertyName = expression.Member.Name;
        var getter = property.Compile();

        void Action()
        {
            var newValue = getter(_observable);
            whenChanged(_observable, newValue);
        }

        _propertyObservers[propertyName] = Action;

        return this;
    }

    /// <summary>
    /// Start the property observers, and triggers pending changes first.
    /// </summary>
    public void Start()
    {
        if (!IsActive)
        {
            if (!_hasBeenActive && ShouldTriggerInitialValues)
                foreach (var property in _propertyObservers)
                    property.Value();

            if (ShouldTriggerPendingChanges)
                foreach (var change in _pendingChanges)
                    if (_propertyObservers.TryGetValue(change, out var action))
                        action();

            _pendingChanges = new HashSet<string>();
            IsActive = true;
            _hasBeenActive = true;
        }
    }

    /// <summary>
    /// Stop this property observers.
    /// </summary>
    public void Stop()
    {
        if (IsActive)
        {
            _propertyObservers = new Dictionary<string, Action>();
            IsActive = false;
        }
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_observer.TryGetTarget(out var _))
        {
            if (IsActive)
            {
                if (_propertyObservers.TryGetValue(e.PropertyName, out var action)) action();
            }
            else
            {
                _pendingChanges.Add(e.PropertyName);
            }
        }
        else
        {
            ((TObservable)sender).PropertyChanged -= OnPropertyChanged;
        }
    }

    #endregion
}