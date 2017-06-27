using GenericGameBridge.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;
using UnityEngine;

namespace TrafficManager {
	/// <summary>
	/// Helper class to make Traffic Manager services available in-game
	/// </summary>
	public class TrafficManager : MonoBehaviour {
		private const string GameObjectName = "TMPE";

		public IServiceFactory ServiceFactory {
			get {
				return Constants.ServiceFactory;
			}
		}

		public IManagerFactory ManagerFactory {
			get {
				return Constants.ManagerFactory;
			}
		}

		public static void Initialize() {
			GameObject gameObject = new GameObject(GameObjectName);
		}

		public static void Dispose() {
			var gameObject = GameObject.Find(GameObjectName);
			if (gameObject != null) {
				Destroy(gameObject);
			}
		}
	}
}
