using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarcusRunge.Core
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

    // Internal base type that provides thread-safe creation and one-time async initialization for a singleton-like instance.
    internal abstract class CreatableBase<TInterface, TClass, TBase> : ICreateableAware
        where TClass : CreatableBase<TInterface, TClass, TBase>, TInterface, new()
    {
        // Global synchronization for singleton creation and starting the async initialization exactly once.
        private static readonly object _sync = new object();

        private static Exception? _initializationException;
        private static Task? _initTask;
        private static TClass? _instance;

        //Instance-level synchronization for event handler registration and draining(invocation after "created" flips).
        private readonly object _createdLock = new object();

        private EventHandler? _createdHandlers;

        // 0 = not created; 1 = created (written via Interlocked, read via Volatile).
        private int _isCreated;

        ///<inheritdoc />
        public event EventHandler OnCreated
        {
            add
            {
                // Method purpose:
                // - Register a callback that should run when the instance becomes "created".
                // - If the instance is already created, invoke the handler immediately (late subscribers are notified instantly).

                // Guard clause:
                // - Ignore null handlers to avoid exceptions and unnecessary locking.
                if (value is null) return;

                // Fast-path (no lock):
                // - If creation already happened, call the handler immediately and return.
                // - This avoids locking for the common "already created" case.
                if (IsCreated)
                {
                    value(this, EventArgs.Empty);
                    return;
                }

                // Slow-path:
                // - The instance is not created (as observed right now), so we try to register the handler.
                // - We must lock because:
                //   1) multiple threads may add/remove simultaneously
                //   2) the "created" transition drains and clears the handler list
                lock (_createdLock)
                {
                    // Double-check under the lock:
                    // - Creation might have completed between the earlier IsCreated check and acquiring the lock.
                    // - If still not created, we safely append to the multicast delegate and exit.
                    if (!IsCreated)
                    {
                        _createdHandlers += value;
                        return;
                    }
                }

                // Race resolution:
                // - If we get here, creation completed while we were locking.
                // - We must still honor the contract: late subscribers are notified.
                value(this, EventArgs.Empty);
            }
            remove
            {
                // Method purpose:
                // - Unregister a previously added handler.
                // - Uses a lock to synchronize with concurrent additions and with the "drain + clear" on creation.
                if (value is null) return;
                lock (_createdLock)
                {
                    _createdHandlers -= value;
                }
            }
        }

        ///<inheritdoc />
        public Task? Initialization => Volatile.Read(ref _initTask);

        ///<inheritdoc />
        public Exception? InitializationException => Volatile.Read(ref _initializationException);

        ///<inheritdoc />
        public bool IsCreated => Volatile.Read(ref _isCreated) == 1;

        // Factory method to create/get the singleton instance.
        internal static TInterface Create(TBase @base)
        {
            // Method purpose:
            // - Ensure the singleton instance exists (synchronous part).
            // - Ensure async initialization is started (exactly once).
            // - Return the created instance cast as TInterface.

            // Step 1:
            // - Create the instance if it does not exist yet.
            EnsureCreated(@base);

            // Step 2:
            // - Start async initialization if it has not been started yet.
            EnsureAsyncInitStarted(@base);

            // Step 3:
            // - Return the instance (null-forgiving because EnsureCreated guarantees it under normal flow).
            return _instance!;
        }

        // Implementers define the synchronous creation hook (typically cheap, no async/await).
        protected abstract void OnCreate(TBase @base);

        // Implementers define the asynchronous creation hook (heavy setup, IO, etc.).
        protected abstract Task OnCreateAsync(TBase @base, CancellationToken cancellationToken);

        private static void EnsureAsyncInitStarted(TBase @base)
        {
            // Method purpose:
            // - Start the async initialization once and only once for the singleton instance.
            // - Record a failure (base exception) if the initialization task faults.

            // Fast-path (no lock):
            // - If initialization task already exists, there is nothing to do.
            if (Volatile.Read(ref _initTask) != null) return;

            // Slow-path (locked):
            // - Synchronize with other threads that might start initialization concurrently.
            lock (_sync)
            {
                // Double-check under the lock:
                // - Another thread might have started initialization between our first check and taking the lock.
                if (_initTask != null) return;

                // Safety check:
                // - Initialization requires a created instance; if it's missing, this indicates an invalid call order.
                if (_instance is null) throw new InvalidOperationException("Instance not created.");

                // Start initialization:
                // - Store the task so that subsequent calls see it and do not start again.
                _initTask = _instance.InitializeAsync(@base);
            }
        }

        private static void EnsureCreated(TBase @base)
        {
            // Method purpose:
            // - Create the singleton instance exactly once.
            // - Run the synchronous creation hook before publishing the instance.

            // Fast-path (no lock):
            // - If instance already exists, return immediately.
            if (_instance != null) return;

            // Slow-path (locked):
            // - Synchronize to ensure only one thread constructs and publishes the singleton instance.
            lock (_sync)
            {
                // Double-check under the lock:
                // - Another thread may have created the instance while we were waiting for the lock.
                if (_instance != null) return;

                // Create concrete instance:
                // - Requires 'new()' constraint on TClass.
                var inst = new TClass();

                // Run synchronous setup:
                // - This allows derived implementations to populate fields/state needed before publication.
                inst.OnCreate(@base);

                // Publish instance:
                // - After this assignment, other threads may observe _instance as non-null.
                _instance = inst;
            }
        }

        private async Task InitializeAsync(TBase @base)
        {
            // Method purpose:
            // - Perform the asynchronous initialization hook.
            // - Transition the instance into the "created" state exactly once.
            // - Notify and drain registered OnCreated handlers exactly once.
            try
            {
                // Run async creation hook:
                // - Uses CancellationToken.None here (caller controls cancellation outside this contract).
                // - ConfigureAwait(false) avoids capturing context, suitable for library/internal code.
                await OnCreateAsync(@base, CancellationToken.None).ConfigureAwait(false);

                // State transition (exactly once):
                // - Interlocked.Exchange returns the previous value.
                // - If it was 0, this call performs the first transition to "created".
                if (Interlocked.Exchange(ref _isCreated, 1) == 0)
                {
                    EventHandler? handlers;

                    // Drain handlers under lock:
                    // - We take a snapshot and clear the field so:
                    //   1) handlers run at most once
                    //   2) memory can be released
                    //   3) concurrent add/remove is synchronized
                    lock (_createdLock)
                    {
                        handlers = _createdHandlers;
                        _createdHandlers = null;
                    }

                    // Invoke outside the lock:
                    // - Prevents handlers from running under a lock (avoids deadlocks/reentrancy issues).
                    handlers?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                // Failure behavior:
                // - Record the exception for internal diagnostics and rethrow to fault the initialization task.
                Volatile.Write(ref _initializationException, ex);
                throw;
            }
        }
    }
}