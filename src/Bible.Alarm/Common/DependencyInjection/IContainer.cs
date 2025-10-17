namespace Bible.Alarm
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A container for managing dependencies between objects.
    /// </summary>
    public interface IContainer
    {
        Dictionary<string, object> Context { get; }

        /// <summary>
        /// Gets all the registered types.
        /// </summary>
        /// <value>The registered types.</value>
        IEnumerable<Type> RegisteredTypes { get; }

        /// <summary>
        /// Register a factory for the given type.
        /// </summary>
        /// <returns>The register.</returns>
        /// <param name="factory">Factory.</param>
        /// <typeparam name="T">The registered type.</typeparam>
        void Register<T>(Func<IContainer, T> factory);


        /// <summary>
        /// Register a factory for the given type.
        /// </summary>
        /// <returns>The register.</returns>
        /// <param name="factory">Factory.</param>
        /// <typeparam name="T">The registered type.</typeparam>
        void RegisterSingleton<T>(Func<IContainer, T> factory);

        /// <summary>
        /// Get an instance of the given type.
        /// </summary>
        /// <remarks>
        /// The type should be registered before use.
        /// </remarks>
        /// <returns>The of.</returns>
        /// <typeparam name="T">The requested type.</typeparam>
        T Resolve<T>();

        /// <summary>
        /// Get an instance of the given type.
        /// </summary>
        /// <remarks>
        /// The type should be registered before use.
        /// </remarks>
        /// <returns>The of.</returns>
        /// <typeparam name="T">The requested type.</typeparam>
        object Resolve(Type type);

        /// <summary>
        /// Wipes the container of all factories and instances.
        /// </summary>
        void WipeContainer();
    }
}
