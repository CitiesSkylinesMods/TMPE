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
		public SegmentGeometry[] segmentGeometries;
		CustomPathFind[] _replacementPathFinds;

		public static CustomPathFind PathFindInstance;

		//On waking up, replace the stock pathfinders with the custom one
		[UsedImplicitly]
		public new virtual void Awake() {
			Log.Message("Waking up CustomPathManager.");
			segmentGeometries = new SegmentGeometry[Singleton<NetManager>.instance.m_segments.m_size];
			Log.Message($"Building {segmentGeometries.Length} segment geometries...");
            for (ushort i = 0; i < segmentGeometries.Length; ++i) {
				segmentGeometries[i] = new SegmentGeometry(i);
			}
			Log.Message($"Calculated segment geometries.");

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

		//
		// BEGIN STOCK CODE
		//

		public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPos, PathUnit.Position endPos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position position = default(PathUnit.Position);
			return CreatePath(out unit, ref randomizer, buildIndex, startPos, position, endPos, position, position, laneTypes, vehicleTypes, maxLength, false, false, false, false);
		}

		public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			return CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, false, false, false, false);
		}

		public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue) {
			return CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue);
		}

		public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue) {
			return CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, ItemClass.Service.None);
		}
		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, ItemClass.Service vehicleService) {
			return CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, vehicleService);
		}

		public bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, ItemClass.Service vehicleService) {
			while (!Monitor.TryEnter(m_bufferLock, SimulationManager.SYNCHRONIZE_TIMEOUT)) {
			}
			uint num;
			try {
				if (!m_pathUnits.CreateItem(out num, ref randomizer)) {
					unit = 0u;
					return false;
				}
				m_pathUnitCount = (int)(m_pathUnits.ItemCount() - 1u);
			} finally {
				Monitor.Exit(m_bufferLock);
			}
			unit = num;

			byte simulationFlags = createSimulationFlag(isHeavyVehicle, ignoreBlocked, stablePath, vehicleService);
			assignPathProperties(unit, buildIndex, startPosA, startPosB, endPosA, endPosB, vehiclePosition, laneTypes, vehicleTypes, maxLength, simulationFlags);

			return findShortestPath(unit, skipQueue);
		}

		public new static uint GetLaneID(PathUnit.Position pathPos) {
			var netManager = Singleton<NetManager>.instance;
			var num = netManager.m_segments.m_buffer[pathPos.m_segment].m_lanes;
			var num2 = 0;
			while (num2 < pathPos.m_lane && num != 0u) {
				num = netManager.m_lanes.m_buffer[(int)((UIntPtr)num)].m_nextLane;
				num2++;
			}
			return num;
		}

		private void assignPathProperties(uint unit, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, byte simulationFlags) {

			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags = simulationFlags;

			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags = 0;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_buildIndex = buildIndex;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position00 = startPosA;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position01 = endPosA;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position02 = startPosB;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position03 = endPosB;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position11 = vehiclePosition;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = 0u;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes = (byte)laneTypes;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes = (byte)vehicleTypes;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = maxLength;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount = 20;
			m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_referenceCount = 1;
		}

		private bool findShortestPath(uint unit, bool skipQueue) {

			int[] maxPathLength = { 10000000 };
			CustomPathFind pathFind = null;
			foreach (var pathFind2 in _replacementPathFinds.Where(pathFind2 => pathFind2.IsAvailable && pathFind2.m_queuedPathFindCount < maxPathLength[0])) {
				maxPathLength[0] = pathFind2.m_queuedPathFindCount;
				pathFind = pathFind2;
			}

			if (pathFind != null && pathFind.CalculatePath(unit, skipQueue)) {
				return true;
			}
			ReleasePath(unit);
			return false;
		}

		private static byte createSimulationFlag(bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, ItemClass.Service vehicleService) {
			byte simulationFlags = 1;

			if (isHeavyVehicle) {
				simulationFlags |= 16;
			}
			if (ignoreBlocked) {
				simulationFlags |= 32;
			}
			if (stablePath) {
				simulationFlags |= 64;
			}

			byte vehicleFlag = 0;
			if (vehicleService == ItemClass.Service.None)
				return simulationFlags; //No VehicleFlag needed.
			if (vehicleService == ItemClass.Service.Commercial)
				vehicleFlag |= 128;
			else if (vehicleService == ItemClass.Service.FireDepartment)
				vehicleFlag |= 130;
			else if (vehicleService == ItemClass.Service.Garbage)
				vehicleFlag |= 132;
			else if (vehicleService == ItemClass.Service.HealthCare)
				vehicleFlag |= 134;
			else if (vehicleService == ItemClass.Service.Industrial)
				vehicleFlag |= 136;
			else if (vehicleService == ItemClass.Service.PoliceDepartment)
				vehicleFlag |= 138;
			else if (vehicleService == ItemClass.Service.PublicTransport)
				vehicleFlag |= 140;

			simulationFlags |= vehicleFlag;

			return simulationFlags;
		}
	}
}
