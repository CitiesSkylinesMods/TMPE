namespace TrafficManager.Util {
    using System;
    using System.Collections;

    public static class ArrayExtensions {
        public static T[] Append<T>(this T[] array, T item) {
            if(array == null)
                throw new ArgumentNullException("source");
            int n = array.Length;
            Array.Resize(ref array, n + 1);
            array[n] = item;
            return array;
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

        public static bool IsNullOrEmpty(this Array array) {
            return array == null || array.Length == 0;
        }
    }
}