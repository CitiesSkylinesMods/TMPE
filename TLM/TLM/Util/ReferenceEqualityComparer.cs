using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace TrafficManager.Util {
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>, IEqualityComparer {

        public static ReferenceEqualityComparer<T> Instance { get; } = new ReferenceEqualityComparer<T>();

        private ReferenceEqualityComparer() { }

        public bool Equals(T x, T y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);

        bool IEqualityComparer.Equals(object x, object y) => ReferenceEquals(x, y);

        int IEqualityComparer.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
