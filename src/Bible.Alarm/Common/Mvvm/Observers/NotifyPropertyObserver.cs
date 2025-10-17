namespace Mvvmicro
{
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
            this._observable = observable;
            this._observable.PropertyChanged += OnPropertyChanged;
            this._observer = new WeakReference<TObserver>(observer);
        }

        #endregion

        #region Fields

        private bool _hasBeenActive;

        private TObservable _observable;

        private WeakReference<TObserver> _observer;

        private Dictionary<string, Action> _propertyObservers = new Dictionary<string, Action>();

        private HashSet<string> _pendingChanges = new HashSet<string>();

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
        public NotifyPropertyObserver<TObserver, TObservable> Observe<T>(Expression<Func<TObservable, T>> property, Action<TObservable, T> whenChanged)
        {
            if (this.IsActive)
                throw new InvalidOperationException("Property observers can only be configured before activation.");

            var expression = (MemberExpression)property.Body;
            var propertyName = expression.Member.Name;
            var getter = property.Compile();
            void Action()
            {
                var newValue = getter(this._observable);
                whenChanged(this._observable, newValue);
            }
            this._propertyObservers[propertyName] = Action;

            return this;
        }

        /// <summary>
        /// Start the property observers, and triggers pending changes first.
        /// </summary>
        public void Start()
        {
            if (!this.IsActive)
            {
                if (!this._hasBeenActive && this.ShouldTriggerInitialValues)
                {
                    foreach (var property in this._propertyObservers)
                    {
                        property.Value();
                    }
                }

                if (this.ShouldTriggerPendingChanges)
                {
                    foreach (var change in this._pendingChanges)
                    {
                        if (this._propertyObservers.TryGetValue(change, out Action action))
                        {
                            action();
                        }
                    }
                }

                this._pendingChanges = new HashSet<string>();
                this.IsActive = true;
                this._hasBeenActive = true;
            }
        }

        /// <summary>
        /// Stop this property observers.
        /// </summary>
        public void Stop()
        {
            if (this.IsActive)
            {
                this._propertyObservers = new Dictionary<string, Action>();
                this.IsActive = false;
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_observer.TryGetTarget(out TObserver _))
            {
                if (this.IsActive)
                {
                    if (this._propertyObservers.TryGetValue(e.PropertyName, out Action action))
                    {
                        action();
                    }
                }
                else
                {
                    this._pendingChanges.Add(e.PropertyName);
                }
            }
            else
            {
                ((TObservable)sender).PropertyChanged -= OnPropertyChanged;
            }
        }

        #endregion
    }
}
