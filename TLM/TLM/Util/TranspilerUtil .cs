
namespace TrafficManager.Util {
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

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

        /// <summary>
        /// Generates Code instruction to access the given argument.
        /// </summary>
        public static CodeInstruction GenerateLDArg(MethodInfo method, string argName) {
            byte idx = (byte)GetParameterLoc(method, argName);
            if (!method.IsStatic)
                idx++; // first argument is object instance.
            if (idx == 0) {
                return new CodeInstruction(OpCodes.Ldarg_0);
            } else if (idx == 1) {
                return new CodeInstruction(OpCodes.Ldarg_1);
            } else if (idx == 2) {
                return new CodeInstruction(OpCodes.Ldarg_2);
            } else if (idx == 3) {
                return new CodeInstruction(OpCodes.Ldarg_3);
            } else {
                return new CodeInstruction(OpCodes.Ldarg_S, idx);
            }
        }

        /// <summary>
        /// Returns the index of the given parameter.
        /// Post condtion: for instnace methods add one to the return value to get argument location.
        /// </summary>
        public static byte GetParameterLoc(MethodInfo method, string name) {
            var parameters = method.GetParameters();
            for (byte i = 0; i < parameters.Length; ++i) {
                if (parameters[i].Name == name) {
                    return i;
                }
            }
            throw new Exception($"did not found parameter with name:<{name}>");
        }
    }
}
