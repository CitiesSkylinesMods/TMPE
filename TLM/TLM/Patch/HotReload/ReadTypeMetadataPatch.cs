namespace TrafficManager.Patch.HotReload {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using System.Text.RegularExpressions;
    using TrafficManager.Util;
    using TrafficManager.Lifecycle;

    [HarmonyPatch]
    [PreloadPatch]
    /// <summary>
    /// Problem: object graph and type converter use type from different assembly versions
    /// (one gets type from first assembly while the other uses last assembly) which creates a conflict.
    /// Solution: Here we make sure both get type from last assembly by removing assembly version from type string.
    /// </summary>
    public static class ReadTypeMetadataPatch {
        private delegate Type GetType(string typeName, bool throwOnError);
        private static string assemblyName_ = typeof(ReadTypeMetadataPatch).Assembly.GetName().Name;

        private static bool Prepare() => TMPELifecycle.Instance.InGameHotReload; // only apply when hot-reloading.

        private static MethodBase TargetMethod() {
            var t = Type.GetType("System.Runtime.Serialization.Formatters.Binary.ObjectReader");
            return AccessTools.DeclaredMethod(t, "ReadTypeMetadata");
        }

        /// <summary>
        /// searches for call to GetType(typeString, true) and removes version data from type string.
        /// </summary>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            MethodInfo mType_GetType = TranspilerUtil.DeclaredMethod<GetType>(typeof(Type), nameof(GetType));
            MethodInfo mReplaceAssemblyVersion = AccessTools.DeclaredMethod(typeof(ReadTypeMetadataPatch), nameof(ReplaceAssemblyVersion));

            foreach (var code in instructions) {
                if (code.Calls(mType_GetType)) {
                    yield return new CodeInstruction(OpCodes.Call, mReplaceAssemblyVersion);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1); // load true again
                }
                yield return code;
            }
        }

        private static string ReplaceAssemblyVersion(string s, bool throwOnError) => ReplaceAssemblyVersionImpl(s);

        private static string ReplaceAssemblyVersionImpl(string s) {
            string num = "\\d+"; // matches ###
            string d = "\\."; // matches .
            string pattern = $"{assemblyName_}, Version={num}{d}{num}{d}{num}{d}{num}, Culture=neutral, PublicKeyToken=null";
            var s2 = Regex.Replace(s, pattern, assemblyName_);
            return s2;
        }
    }
}
