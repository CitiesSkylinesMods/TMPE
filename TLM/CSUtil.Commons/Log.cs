namespace CSUtil.Commons {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using UnityEngine;

    public static class Log {
        private static readonly object LogLock_ = new object();

        // TODO refactor log filename to configuration
        private static readonly string LogFilename_
            = Path.Combine(Application.dataPath, "TMPE.log");

        private enum LogLevel {
            Trace,
            Debug,
            Info,
            Warning,
            Error
        }

        private static Stopwatch sw = Stopwatch.StartNew();

        static Log() {
            try {
                if (File.Exists(LogFilename_)) {
                    File.Delete(LogFilename_);
                }
            }
            catch (Exception) { }
        }

        [Conditional("DEBUG")]
        public static void _Debug(string s) {
            LogToFile(s, LogLevel.Debug);
        }

        [Conditional("TRACE")]
        public static void _Trace(string s) {
            LogToFile(s, LogLevel.Trace);
        }

        public static void Info(string s) {
            LogToFile(s, LogLevel.Info);
        }

        [Conditional("DEBUG")]
        public static void _DebugOnlyWarning(string s) {
            LogToFile(s, LogLevel.Warning);
        }

        public static void Warning(string s) {
            LogToFile(s, LogLevel.Warning);
        }

        public static void Error(string s) {
            LogToFile(s, LogLevel.Error);
        }

        private static void LogToFile(string log, LogLevel level) {
            try {
                Monitor.Enter(LogLock_);

                using (StreamWriter w = File.AppendText(LogFilename_)) {
                    w.WriteLine($"[{level.ToString()}] @ {sw.ElapsedTicks} {log}");
                    if (level == LogLevel.Warning || level == LogLevel.Error) {
                        w.WriteLine((new System.Diagnostics.StackTrace()).ToString());
                        w.WriteLine();
                    }
                }
            }
            finally {
                Monitor.Exit(LogLock_);
            }
        }
    }
}