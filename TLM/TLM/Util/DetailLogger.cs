using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using CSUtil.Commons;

namespace TrafficManager.Util {
    internal static class DetailLogger {

        public delegate void LogDelegate(params object[] lines);

        [Conditional("DEBUG")]
        public static void LogDebug(string heading, params object[] lines) {
            var entry = new StringBuilder();

            if (heading != null) {
                entry.Append(heading);
                entry.Append(' ');
            }
            string newline = string.Empty;

            foreach (var o in lines) {
                entry.Append(newline);
                newline = "\n";
                if (o is string) {
                    entry.Append(o);
                } else {
                    BuildBLock(o);
                }
            }
            Log._Debug(entry.ToString());

            void BuildBLock(object o) {
                string separator = string.Empty;
                foreach (var p in o.GetType().GetProperties()) {
                    entry.Append(separator);
                    separator = " ";
                    if (p.Name[0] != '_' || p.Name.Any(c => c != '_')) {
                        entry.Append(p.Name)
                            .Append('=');
                    }
                    var v = p.GetValue(o, null);
                    if (v.GetType().IsDefined(typeof(CompilerGeneratedAttribute), false)) {
                        entry.Append('[');
                        BuildBLock(v);
                        entry.Append(']');
                    } else {
                        entry.Append(p.GetValue(o, null));
                    }
                }
            }
        }
    }
}
