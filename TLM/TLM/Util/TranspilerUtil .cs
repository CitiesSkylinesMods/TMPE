
namespace TrafficManager.Util {
    using CSUtil.Commons;
    using HarmonyLib;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    public static class TranspilerUtil {
        /// <typeparam name="T">delegate type</typeparam>
        /// <returns>Type[] represeting arguments of the delegate.</returns>
        internal static Type[] GetParameterTypes<T>() where T : Delegate {
            return typeof(T).GetMethod("Invoke").GetParameters().Select(p => p.ParameterType).ToArray();
        }

        internal static MethodInfo DeclaredMethod<DelegateT>(Type type, string name) where DelegateT : Delegate {
            var ret = AccessTools.DeclaredMethod(type, name, GetParameterTypes<DelegateT>());
            if (ret == null)
                Log.Error($"faield to retrieve method {type}.name({typeof(DelegateT)})");
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
