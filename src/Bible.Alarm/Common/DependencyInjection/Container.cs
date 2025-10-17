namespace Bible.Alarm
{
    using System;
    using System.Collections.Generic;

    public class Container(Dictionary<string, object> context) : IContainer
    {
        public Dictionary<string, object> Context { get; private set; } = context;

        #region Fields

        private Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        private Dictionary<Type, Tuple<bool, Func<object>>> _factories = new Dictionary<Type, Tuple<bool, Func<object>>>();

        #endregion

        #region Properties

        public IEnumerable<Type> RegisteredTypes => _factories.Keys;

        #endregion

        #region Methods

        public object Resolve(Type type)
        {
            var factory = this._factories[type];

            if (factory.Item1)
            {
                if (_instances.TryGetValue(type, out object instance))
                {
                    return instance;
                }

                var newInstance = factory.Item2();
                _instances[type] = newInstance;
                return newInstance;
            }

            return factory.Item2();
        }

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public void Register<T>(Func<IContainer, T> factory)
        {
            this._factories[typeof(T)] = new Tuple<bool, Func<object>>(false, () => factory(this));
        }

        public void RegisterSingleton<T>(Func<IContainer, T> factory)
        {
            this._factories[typeof(T)] = new Tuple<bool, Func<object>>(true, () => factory(this));
        }

        public void WipeContainer()
        {
            this._instances = new Dictionary<Type, object>();
            this._factories = new Dictionary<Type, Tuple<bool, Func<object>>>();
        }

        #endregion
    }
}
