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
		CustomPathFind[] _replacementPathFinds;

		public static CustomPathFind PathFindInstance;

		//On waking up, replace the stock pathfinders with the custom one
		[UsedImplicitly]
		public new virtual void Awake() {
			Log.Message("Waking up CustomPathManager.");

			var stockPathFinds = GetComponents<PathFind>();
			var numOfStockPathFinds = stockPathFinds.Length;

			Log.Message("Creating " + numOfStockPathFinds + " custom PathFind objects.");
			_replacementPathFinds = new CustomPathFind[numOfStockPathFinds];
			for (var i = 0; i < numOfStockPathFinds; i++) {
				_replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
				Destroy(stockPathFinds[i]);
			}

			Log.Message("Setting _replacementPathFinds");
			var fieldInfo = typeof(PathManager).GetField("m_pathfinds", BindingFlags.NonPublic | BindingFlags.Instance);

			Log.Message("Setting m_pathfinds to custom collection");
			fieldInfo?.SetValue(this, _replacementPathFinds);
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
		}
	}
}
