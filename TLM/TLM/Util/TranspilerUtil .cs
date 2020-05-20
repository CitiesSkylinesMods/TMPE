using System;

namespace TrafficManager.Util {
    public static class TranspilerUtil{
        /// <typeparam name="T">delegate type</typeparam>
        /// <returns>Type[] represeting arguments of the delegate</returns>
        internal static Type[] GetGenericArguments<T>() where T : Delegate {
            T dummy = default;
            return dummy.Method.GetGenericArguments();
        }
    }
}
