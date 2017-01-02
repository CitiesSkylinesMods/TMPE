using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace TrafficManager {

	public static class Log {
		private enum LogLevel {
			Debug,
			Info,
			Warning,
			Error
		}

		private static object logLock = new object();

		const string Prefix = "TrafficLightManager: ";

		private static string logFilename = Path.Combine(Application.dataPath, "TMPE.log");
		private static Stopwatch sw = Stopwatch.StartNew();
		private static bool logToConsole = false;
		private static bool logFileAccessible = true;


		static Log() {
			try {
				if (File.Exists(logFilename))
					File.Delete(logFilename);
			} catch (Exception) {
				logFileAccessible = false;
			}
		}

		[Conditional("DEBUG")]
		public static void _Debug(string s) {
			if (!logFileAccessible) {
				return;
			}

			try {
				Monitor.Enter(logLock);
				if (logToConsole)
					UnityEngine.Debug.Log(Prefix + s);
				LogToFile(s, LogLevel.Debug);
			} catch (Exception) {
				
			} finally {
				Monitor.Exit(logLock);
			}
		}

		public static void Info(string s) {
			if (!logFileAccessible) {
				return;
			}

			try {
#if DEBUG
				if (logToConsole)
					UnityEngine.Debug.Log(Prefix + s);
#endif
				Monitor.Enter(logLock);
				LogToFile(s, LogLevel.Info);
			} catch (Exception) {

			} finally {
				Monitor.Exit(logLock);
			}
		}

		public static void Error(string s) {
			if (!logFileAccessible) {
				return;
			}

			try {
#if DEBUG
				if (logToConsole)
					UnityEngine.Debug.LogError(Prefix + s + " " + (new System.Diagnostics.StackTrace()).ToString());
#endif
				Monitor.Enter(logLock);
				LogToFile(s, LogLevel.Error);
			} catch (Exception) {
				// cross thread issue?
			} finally {
				Monitor.Exit(logLock);
			}
		}

		public static void Warning(string s) {
			if (!logFileAccessible) {
				return;
			}

			try {
#if DEBUG
				if (logToConsole)
					UnityEngine.Debug.LogWarning(Prefix + s + ": " + (new System.Diagnostics.StackTrace()).ToString());
#endif
				Monitor.Enter(logLock);
				LogToFile(s, LogLevel.Warning);
			} catch (Exception) {
				// cross thread issue?
			} finally {
				Monitor.Exit(logLock);
			}
		}

		private static void LogToFile(string log, LogLevel level) {
			if (! logFileAccessible) {
				return;
			}

			try {
				using (StreamWriter w = File.AppendText(logFilename)) {
					w.WriteLine($"[{level.ToString()}] @ {sw.ElapsedTicks} {log}");
					if (level == LogLevel.Warning || level == LogLevel.Error) {
						w.WriteLine((new System.Diagnostics.StackTrace()).ToString());
						w.WriteLine();
					}
                }
			} catch (Exception) {
				logFileAccessible = false;
			}
		}
	}

}
