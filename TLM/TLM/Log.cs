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
#if DEBUG
		private static bool logToConsole = false;
#endif

		static Log() {
			File.Delete(logFilename);
		}

		public static void _Debug(string s) {
#if DEBUG
			try {
				Monitor.Enter(logLock);
				if (logToConsole)
					UnityEngine.Debug.Log(Prefix + s);
				LogToFile(s, LogLevel.Debug);
			} catch (Exception) {
				
			} finally {
				Monitor.Exit(logLock);
			}
#endif
		}

		public static void Info(string s) {
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
			try {
				using (StreamWriter w = File.AppendText(logFilename)) {
					w.WriteLine($"[{level.ToString()}] @ {sw.ElapsedTicks} {log}");
					if (level == LogLevel.Warning || level == LogLevel.Error) {
						w.WriteLine((new System.Diagnostics.StackTrace()).ToString());
						w.WriteLine();
					}
                }
			} catch (Exception) { }
		}
	}

}
