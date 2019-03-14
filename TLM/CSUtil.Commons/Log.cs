using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace CSUtil.Commons {

	public static class Log {
		private enum LogLevel {
			Trace,
			Debug,
			Info,
			Warning,
			Error
		}

		private static object logLock = new object();

		private static string logFilename = Path.Combine(Application.dataPath, "TMPE.log"); // TODO refactor log filename to configuration
		private static Stopwatch sw = Stopwatch.StartNew();

		static Log() {
			try {
				if (File.Exists(logFilename)) {
					File.Delete(logFilename);
				}
			} catch (Exception) {
				
			}
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

		public static void Warning(string s) {
			LogToFile(s, LogLevel.Warning);
		}

		public static void Error(string s) {
			LogToFile(s, LogLevel.Error);
		}

		private static void LogToFile(string log, LogLevel level) {
			try {
				Monitor.Enter(logLock);
				
				using (StreamWriter w = File.AppendText(logFilename)) {
					w.WriteLine($"[{level.ToString()}] @ {sw.ElapsedTicks} {log}");
					if (level == LogLevel.Warning || level == LogLevel.Error) {
						w.WriteLine((new System.Diagnostics.StackTrace()).ToString());
						w.WriteLine();
					}
				}
			} finally {
				Monitor.Exit(logLock);
			}
		}
	}

}
