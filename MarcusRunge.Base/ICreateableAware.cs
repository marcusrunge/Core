using System;
using System.Threading.Tasks;

namespace MarcusRunge.Base
{
    /// <summary>
    /// Provides a contract for components that expose their creation state and a creation notification event.
    /// </summary>
    public interface ICreateableAware
    {
        public event EventHandler OnCreated;

        /// <summary>
        /// Occurs when the instance is created.
        /// </summary>
        /// <summary>
        /// Gets a task that represents the asynchronous initialization process of the instance. If the instance is already created, this property may return null.
        /// </summary>
        Task? Initialization { get; }

        /// <summary>
        /// Gets an exception that occurred during the initialization process, if any.
        /// </summary>
        Exception? InitializationException { get; }

        /// <summary>
        /// Gets a value indicating whether the instance has been created.
        /// </summary>
        public bool IsCreated { get; }
    }
}