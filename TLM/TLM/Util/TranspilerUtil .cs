
namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using HarmonyLib;
    using CSUtil.Commons;
    using System.Reflection;

    public static class TranspilerUtil{
        /// <typeparam name="T">delegate type</typeparam>
        /// <returns>Type[] represeting arguments of the delegate.</returns>
        internal static Type[] GetGenericArguments<T>() where T : Delegate {
            T dummy = default;
            return dummy.Method.GetGenericArguments();
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
