using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kzone.Signal
{
    /// <summary>An asynchronous event handler.
    /// To trigger the event, use the function <see cref="AsyncEventHandlerExtensions.InvokeAsync{TEventArgs}(AsyncEventHandler{TEventArgs}, object, TEventArgs)"/>
    /// instead of <see cref="EventHandler.Invoke(object, EventArgs)"/>, it will guarantee a serialization
    /// of calls to the event delegates and collects any exceptions.
    /// <example><code>
    ///     public event AsyncEventHandler<EventArgs> MyEvent;
    /// 
    ///     // Trigger the event
    ///     public async Task OnMyEvent()
    ///     {
    ///         // A null check is not necessary, the ? operator would fail here
    ///         await MyEvent.InvokeIfNotNullAsync(this, new EventArgs());
    ///     }
    /// </code></example>
    /// </summary>
    /// <typeparam name="TEventArgs">The type of event arguments.</typeparam>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="args">Event arguments.</param>
    /// <returns>An awaitable task.</returns>
    public delegate Task AsyncEventHandler<TEventArgs>(object sender, TEventArgs args);

    public static class AsyncEventHandlerExtensions
    {
        /// <summary>
        /// Invokes asynchronous event handlers, returning an awaitable task. Each handler is fully executed
        /// before the next handler in the list is invoked.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of argument passed to each handler.</typeparam>
        /// <param name="handlers">The event handlers. May be <c>null</c>.</param>
        /// <param name="sender">The event source.</param>
        /// <param name="args">The event argument.</param>
        /// <returns>An awaitable task that completes when all handlers have completed.</returns>
        /// <exception cref="T:System.AggregateException">Thrown if any handlers fail. It contains all
        /// collected exceptions.</exception>
        public static async Task InvokeAsync<TEventArgs>(
            this AsyncEventHandler<TEventArgs> handlers,
            object sender,
            TEventArgs args)
        {
            if (handlers == null)
                return;

            List<Exception> exceptions = null;
            Delegate[] listenerDelegates = handlers.GetInvocationList();
            for (int index = 0; index < listenerDelegates.Length; ++index)
            {
                var listenerDelegate = (AsyncEventHandler<TEventArgs>)listenerDelegates[index];
                try
                {
                    await listenerDelegate(sender, args).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    exceptions ??= new List<Exception>(2);
                    exceptions.Add(ex);
                }
            }

            // Throw collected exceptions, if any
            if (exceptions != null)
                throw new AggregateException(exceptions);
        }
    }
}
