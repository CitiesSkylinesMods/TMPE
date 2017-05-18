using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace CSUtil.Commons {

	public static class Log {
		private enum LogLevel {
			Debug,
			Info,
			Warning,
			Error
		}

		private static object logLock = new object();

		private static string logFilename = Path.Combine(Application.dataPath, "TMPE.log"); // TODO refactor log filename to configuration
		private static Stopwatch sw = Stopwatch.StartNew();
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
