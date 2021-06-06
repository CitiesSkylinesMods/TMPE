using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TrafficManager {
    public struct ValueTuple {
        public static ValueTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2) =>
            new(item1, item2);
    }

    [StructLayout(LayoutKind.Auto)]
    public struct ValueTuple<T1, T2> : IEquatable<ValueTuple<T1, T2>> {
        public T1 Item1;
        public T2 Item2;

        public ValueTuple(T1 item1, T2 item2) {
            this.Item1 = item1;
            this.Item2 = item2;
        }

        public static bool operator ==(ValueTuple<T1, T2> left, ValueTuple<T1, T2> right) =>
            left.Equals(right);

        public static bool operator !=(ValueTuple<T1, T2> left, ValueTuple<T1, T2> right) =>
            !(left == right);

        public override string ToString() =>
            "(" + Item1?.ToString() + ", " + Item2?.ToString() + ")";

        public override bool Equals(object obj) =>
            obj is ValueTuple<T1, T2> tuple && Equals(tuple);

        public bool Equals(ValueTuple<T1, T2> other) =>
            EqualityComparer<T1>.Default.Equals(Item1, other.Item1)
            && EqualityComparer<T2>.Default.Equals(Item2, other.Item2);

        public override int GetHashCode() {
            int hashCode = -1030903623;
            hashCode = (hashCode * -1521134295) + EqualityComparer<T1>.Default.GetHashCode(Item1);
            hashCode = (hashCode * -1521134295) + EqualityComparer<T2>.Default.GetHashCode(Item2);
            return hashCode;
        }
    }
}
