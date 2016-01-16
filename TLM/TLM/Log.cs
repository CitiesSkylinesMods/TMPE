using System;
using UnityEngine;

namespace TrafficManager {

	public static class Log {
		const string Prefix = "TrafficLightManager: ";

		public static void Message(string s) {
#if DEBUG
			try {
				//DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, Prefix + s.ToString());
				Debug.Log(Prefix + s);
			} catch (Exception) {
				// cross thread issue?
			}

#endif
		}

		public static void Error(string s) {
#if DEBUG
			try {
				Debug.LogError(Prefix + s + ": " + (new System.Diagnostics.StackTrace()).ToString());
				//DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, Prefix + s);
			} catch (Exception) {
				// cross thread issue?
			}
#endif
		}

		public static void Warning(string s) {
#if DEBUG
			try {

				Debug.LogWarning(Prefix + s + ": " + (new System.Diagnostics.StackTrace()).ToString());
				//DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, Prefix + s);
			} catch (Exception) {
				// cross thread issue?
			}
#endif
		}
	}

}
