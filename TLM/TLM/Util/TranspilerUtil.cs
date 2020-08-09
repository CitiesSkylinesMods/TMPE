namespace TrafficManager.Util {
    using HarmonyLib;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using CSUtil.Commons;
    
    public static class TranspilerUtil {
        public class InstructionNotFoundException : Exception {
            public InstructionNotFoundException() : base() { }
            public InstructionNotFoundException(string m) : base(m) { }

        }

        static bool VERBOSE => false;

        internal static string IL2STR(this IEnumerable<CodeInstruction> instructions) {
            string ret = "";
            foreach (var code in instructions) {
                ret += code + "\n";
            }
            return ret;
        }

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
                return (a.operand is byte aByte && b.operand is byte bByte && aByte == bByte);
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

        public delegate bool Comperator(int idx);
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
    }
}
