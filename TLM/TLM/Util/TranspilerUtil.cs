namespace TrafficManager.Util {
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using System.Linq;
    using System.Reflection;

    public static class TranspilerUtil {
        /// <typeparam name="TDelegate">delegate type</typeparam>
        /// <returns>Type[] represeting arguments of the delegate.</returns>
        internal static Type[] GetParameterTypes<TDelegate>() where TDelegate : Delegate {
            return typeof(TDelegate).GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
        }

        /// <summary>
        /// Gets directly declared method.
        /// </summary>
        /// <typeparam name="TDelegate">delegate that has the same argument types as the intented overloaded method</typeparam>
        /// <param name="type">the class/type where the method is delcared</param>
        /// <param name="name">the name of the method</param>
        /// <returns>a method or null when type is null or when a method is not found</returns>
        internal static MethodInfo DeclaredMethod<TDelegate>(Type type, string name)
            where TDelegate : Delegate {
            var args = GetParameterTypes<TDelegate>();
            var ret = AccessTools.DeclaredMethod(type, name, args);
            if (ret == null)
                Log.Error($"failed to retrieve method {type}.{name}({args.ToSTR()})");
            return ret;
        }
    }
}
