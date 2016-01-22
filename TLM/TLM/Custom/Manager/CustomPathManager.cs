using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using TrafficManager.Custom.Misc;
using ColossalFramework;
using ColossalFramework.Math;
using JetBrains.Annotations;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace TrafficManager.Custom.Manager {
	public class CustomPathManager : PathManager {
		internal CustomPathFind[] _replacementPathFinds;

		public static CustomPathManager _instance;

		//On waking up, replace the stock pathfinders with the custom one
		[UsedImplicitly]
		public new virtual void Awake() {
			_instance = this;
		}

		public void UpdateWithPathManagerValues(PathManager stockPathManager) {
			// Needed fields come from joaofarias' csl-traffic
			// https://github.com/joaofarias/csl-traffic

			m_simulationProfiler = stockPathManager.m_simulationProfiler;
			m_drawCallData = stockPathManager.m_drawCallData;
			m_properties = stockPathManager.m_properties;
			m_pathUnitCount = stockPathManager.m_pathUnitCount;
			m_renderPathGizmo = stockPathManager.m_renderPathGizmo;
			m_pathUnits = stockPathManager.m_pathUnits;
			m_bufferLock = stockPathManager.m_bufferLock;

			Log._Debug("Waking up CustomPathManager.");

			var stockPathFinds = GetComponents<PathFind>();
			var numOfStockPathFinds = stockPathFinds.Length;

			Log._Debug("Creating " + numOfStockPathFinds + " custom PathFind objects.");
			_replacementPathFinds = new CustomPathFind[numOfStockPathFinds];
			try {
				while (!Monitor.TryEnter(this.m_bufferLock)) { }

				for (var i = 0; i < numOfStockPathFinds; i++) {
					_replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
					if (i == 0)
						_replacementPathFinds[i].IsMasterPathFind = true;
				}

				Log._Debug("Setting _replacementPathFinds");
				var fieldInfo = typeof(PathManager).GetField("m_pathfinds", BindingFlags.NonPublic | BindingFlags.Instance);

				Log._Debug("Setting m_pathfinds to custom collection");
				fieldInfo?.SetValue(this, _replacementPathFinds);

				for (var i = 0; i < numOfStockPathFinds; i++) {
					Destroy(stockPathFinds[i]);
				}
			} finally {
				Monitor.Exit(this.m_bufferLock);
			}
		}
	}
}
