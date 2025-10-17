namespace Bible.Alarm
{
    using System;
    using System.Collections.Generic;

    public class Container : IContainer
    {
        public Dictionary<string, object> Context { get; private set; }
        public Container(Dictionary<string, object> context)
        {
            Context = context;
        }

        #region Fields

        private Dictionary<Type, object> instances = new Dictionary<Type, object>();

        private Dictionary<Type, Tuple<bool, Func<object>>> factories = new Dictionary<Type, Tuple<bool, Func<object>>>();

        #endregion

        #region Properties

        public IEnumerable<Type> RegisteredTypes => factories.Keys;

        #endregion

        #region Methods

        public object Resolve(Type type)
        {
            var factory = this.factories[type];

            if (factory.Item1)
            {
                if (instances.TryGetValue(type, out object instance))
                {
                    return instance;
                }

                var newInstance = factory.Item2();
                instances[type] = newInstance;
                return newInstance;
            }

            return factory.Item2();
        }

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public void Register<T>(Func<IContainer, T> factory)
        {
            this.factories[typeof(T)] = new Tuple<bool, Func<object>>(false, () => factory(this));
        }

        public void RegisterSingleton<T>(Func<IContainer, T> factory)
        {
            this.factories[typeof(T)] = new Tuple<bool, Func<object>>(true, () => factory(this));
        }

        public void WipeContainer()
        {
            this.instances = new Dictionary<Type, object>();
            this.factories = new Dictionary<Type, Tuple<bool, Func<object>>>();
        }

        #endregion
    }
}
