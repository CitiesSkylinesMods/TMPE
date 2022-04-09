using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TrafficManager.Util {
    /// <summary>
    /// Implements IDisposable as a proxy to an <see cref="Action"/> delegate.
    /// </summary>
    public sealed class AnonymousDisposable : IDisposable {
        private volatile Action dispose;

        /// <summary>
        /// An IDisposable object that does nothing.  This may be returned when an overriding
        /// method is required to return IDisposable but has no need for its functionality.
        /// It may also be returned as a placeholder when anticipating a future need for 
        /// IDisposable.
        /// </summary>
        public static readonly IDisposable Empty = new AnonymousDisposable(null);

        /// <summary>
        /// Constructs an object that invokes the specified action when its <see cref="IDisposable.Dispose"/>
        /// method is called.
        /// </summary>
        /// <param name="dispose"></param>
        public AnonymousDisposable(Action dispose) => this.dispose = dispose;

        /// <summary>
        /// On the first call, invokes the delegate that was provided to the constructor.
        /// On subsequent calls, does nothing.
        /// </summary>
        public void Dispose() => Interlocked.Exchange(ref dispose, null)?.Invoke();
    }
}
