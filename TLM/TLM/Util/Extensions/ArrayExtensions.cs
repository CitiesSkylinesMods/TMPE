namespace TrafficManager.Util {
    using System;
    public static class ArrayExtensions {
        public static T[] Append<T>(this T[] source, T item) {
            if(source == null)
                throw new ArgumentNullException("source");
            int n = source.Length;
            T[] ret = new T[n + 1];
            Array.Copy(source, ret, n);
            ret[n] = item;
            return ret;
        }
        public static T[] AppendOrCreate<T>(this T[] source, T item) {
            int n = source?.Length ?? 0;
            T[] res = new T[n + 1];
            if(n > 0) {
                Array.Copy(source, res, n);
            }
            res[n] = item;
            return res;
        }
    }
}