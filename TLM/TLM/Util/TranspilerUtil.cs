namespace TrafficManager.Util {
    using HarmonyLib;
    using System;
    using System.Collections;
    using System.Linq;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using CSUtil.Commons;

    public static class TranspilerUtil {
        public delegate bool Comperator(int idx);

        static bool VERBOSE => false;

        internal static string IL2STR(this IEnumerable<CodeInstruction> instructions) {
            string ret = "";
            foreach (var code in instructions) {
                ret += code + "\n";
            }
            return ret;
        }

        /// <summary>
        /// Gets parameter types from delegate
        /// </summary>
        /// <typeparam name="TDelegate">delegate type</typeparam>
        /// <param name="instance">skip first parameter. Default value is false.</param>
        /// <returns>Type[] representing arguments of the delegate.</returns>
        internal static Type[] GetParameterTypes<TDelegate>(bool instance = false) where TDelegate : Delegate {
            IEnumerable<ParameterInfo> parameters = typeof(TDelegate).GetMethod("Invoke").GetParameters();
            if (instance) {
                parameters = parameters.Skip(1);
            }

            return parameters.Select(p => p.ParameterType).ToArray();
        }

        /// <summary>
        /// Gets directly declared method.
        /// </summary>
        /// <typeparam name="TDelegate">delegate that has the same argument types as the intended overloaded method</typeparam>
        /// <param name="type">the class/type where the method is declared</param>
        /// <param name="name">the name of the method</param>
        /// <param name="instance">is instance delegate (require skip if the first param)</param>
        /// <returns>a method or null when type is null or when a method is not found</returns>
        internal static MethodInfo DeclaredMethod<TDelegate>(Type type, string name, bool instance = false)
            where TDelegate : Delegate {
            var args = GetParameterTypes<TDelegate>(instance);
            var ret = AccessTools.DeclaredMethod(type, name, args);
            if (ret == null)
                Log.Error($"failed to retrieve method {type}.{name}({args.ToSTR()})");
            return ret;
        }

        public static TDelegate CreateDelegate<TDelegate>(Type type, string name, bool instance)
            where TDelegate : Delegate {

            var types = GetParameterTypes<TDelegate>(instance);
            var ret = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                types,
                new ParameterModifier[0]);
            if (ret == null)
                Log.Error($"failed to retrieve method {type}.{name}({types.ToSTR()})");

            return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), ret);
        }

        public static List<CodeInstruction> ToCodeList(IEnumerable<CodeInstruction> instructions) {
            var originalCodes = new List<CodeInstruction>(instructions);
            var codes = new List<CodeInstruction>(originalCodes);
            return codes;
        }

        public static bool IsSameInstruction(CodeInstruction a, CodeInstruction b, bool debug = false) {
            if (a.opcode == b.opcode) {
                if (a.operand == b.operand) {
                    return true;
                }

                // This special code is needed for some reason because the == operator doesn't work on System.Byte
                return (a.operand is byte aByte && b.operand is byte bByte && aByte == bByte)
                       || (a.operand is int aInt && b.operand is int bInt && aInt == bInt);
            } else {
                return false;
            }
        }

        public static int SearchInstruction(List<CodeInstruction> codes, CodeInstruction instruction, int index, int dir = +1, int counter = 1) {
            try {
                return SearchGeneric(codes, idx => IsSameInstruction(codes[idx], instruction), index, dir, counter);
            }
            catch (InstructionNotFoundException) {
                throw new InstructionNotFoundException(" Did not found instruction: " + instruction);
            }
        }

        public static int SearchGeneric(List<CodeInstruction> codes, Comperator comperator, int index, int dir = +1, int counter = 1) {
            int count = 0;
            for (; 0 <= index && index < codes.Count; index += dir) {
                if (comperator(index)) {
                    if (++count == counter)
                        break;
                }
            }
            if (count != counter) {
                throw new InstructionNotFoundException(" Did not found instruction[s]. Comperator =  " + comperator);
            }
            if(VERBOSE)
                Log._Debug("Found : \n" + new[] { codes[index], codes[index + 1] }.IL2STR());
            return index;
        }

        public static void MoveLabels(CodeInstruction source, CodeInstruction target) {
            // move labels
            var labels = source.labels;
            target.labels.AddRange((IEnumerable<Label>)labels);
            labels.Clear();
        }

        public static void InsertInstructions(List<CodeInstruction> codes, CodeInstruction[] insertion, int index, bool moveLabels = true) {
            foreach (var code in insertion) {
                if (code == null)
                    throw new Exception("Bad Instructions:\n" + insertion.IL2STR());
            }
            if (VERBOSE)
                Log._Debug($"Insert point:\n between: <{codes[index - 1]}>  and  <{codes[index]}>");

            MoveLabels(codes[index], insertion[0]);
            codes.InsertRange(index, insertion);

            if (VERBOSE) {
                Log._Debug("\n" + insertion.IL2STR());
                int start = index - 4;
                if (start < 0) start = 0;
                int count = insertion.Length + 12;
                if (count + start >= codes.Count) count = codes.Count - start - 1;
                Log._Debug("PEEK:\n" + codes.GetRange(start, count).IL2STR());
            }
        }

        public class InstructionNotFoundException : Exception {
            public InstructionNotFoundException() : base() { }
            public InstructionNotFoundException(string m) : base(m) { }
        }
    }
}
