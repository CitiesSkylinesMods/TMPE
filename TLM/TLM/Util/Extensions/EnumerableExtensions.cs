namespace TrafficManager.Util.Extensions {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal static class EnumerableExtensions {
        internal static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> e) => e ?? Enumerable.Empty<T>();
    }
}
