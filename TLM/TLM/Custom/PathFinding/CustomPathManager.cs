#define QUEUEDSTATSx
#define EXTRAPFx

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using JetBrains.Annotations;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.State;

// ReSharper disable InconsistentNaming

namespace TrafficManager.Custom.PathFinding {
	public class CustomPathManager : PathManager {
		internal static CustomPathFind[] _replacementPathFinds;

		public static CustomPathManager _instance;

#if QUEUEDSTATS
		public static uint TotalQueuedPathFinds {
			get; private set;
		} = 0;

#if EXTRAPF
		public static uint ExtraQueuedPathFinds {
			get; private set;
		} = 0;
#endif
#endif

		public static bool InitDone {
			get; private set;
		} = false;

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
			int numCustomPathFinds = numOfStockPathFinds;
#if EXTRAPF
			++numCustomPathFinds;
#endif

			Log._Debug("Creating " + numCustomPathFinds + " custom PathFind objects.");
			_replacementPathFinds = new CustomPathFind[numCustomPathFinds];
			try {
				Monitor.Enter(this.m_bufferLock);

				for (var i = 0; i < numCustomPathFinds; i++) {
					_replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
					if (i == 0) {
						_replacementPathFinds[i].IsMasterPathFind = true;
					}
#if EXTRAPF
					else if (i == numCustomPathFinds - 1) {
						_replacementPathFinds[i].IsExtraPathFind = true;
					}
#endif
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

			InitDone = true;
		}

		public bool CreatePath(ExtVehicleType vehicleType, out uint unit, ref Randomizer randomizer, uint buildIndex, ref PathUnit.Position startPos, ref PathUnit.Position endPos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position position = default(PathUnit.Position);
			return this.CreatePath(false, vehicleType, out unit, ref randomizer, buildIndex, ref startPos, ref position, ref endPos, ref position, ref position, laneTypes, vehicleTypes, maxLength, false, false, false, false, false);
		}

		public bool CreatePath(ExtVehicleType vehicleType, out uint unit, ref Randomizer randomizer, uint buildIndex, ref PathUnit.Position startPosA, ref PathUnit.Position startPosB, ref PathUnit.Position endPosA, ref PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position def = default(PathUnit.Position);
			return this.CreatePath(false, vehicleType, out unit, ref randomizer, buildIndex, ref startPosA, ref startPosB, ref endPosA, ref endPosB, ref def, laneTypes, vehicleTypes, maxLength, false, false, false, false, false);
		}

		public bool CreatePath(ExtVehicleType vehicleType, out uint unit, ref Randomizer randomizer, uint buildIndex, ref PathUnit.Position startPosA, ref PathUnit.Position startPosB, ref PathUnit.Position endPosA, ref PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue) {
			PathUnit.Position def = default(PathUnit.Position);
			return this.CreatePath(false, vehicleType, out unit, ref randomizer, buildIndex, ref startPosA, ref startPosB, ref endPosA, ref endPosB, ref def, laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, false);
		}

		public bool CreatePath(bool recalc, ExtVehicleType vehicleType, out uint unit, ref Randomizer randomizer, uint buildIndex, ref PathUnit.Position startPos, ref PathUnit.Position endPos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position position = default(PathUnit.Position);
			return this.CreatePath(recalc, vehicleType, out unit, ref randomizer, buildIndex, ref startPos, ref position, ref endPos, ref position, ref position, laneTypes, vehicleTypes, maxLength, false, false, false, false, false);
		}

		public bool CreatePath(bool recalc, ExtVehicleType vehicleType, out uint unit, ref Randomizer randomizer, uint buildIndex, ref PathUnit.Position startPosA, ref PathUnit.Position startPosB, ref PathUnit.Position endPosA, ref PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength) {
			PathUnit.Position def = default(PathUnit.Position);
			return this.CreatePath(recalc, vehicleType, out unit, ref randomizer, buildIndex, ref startPosA, ref startPosB, ref endPosA, ref endPosB, ref def, laneTypes, vehicleTypes, maxLength, false, false, false, false, false);
		}

		public bool CreatePath(bool recalc, ExtVehicleType vehicleType, out uint unit, ref Randomizer randomizer, uint buildIndex, ref PathUnit.Position startPosA, ref PathUnit.Position startPosB, ref PathUnit.Position endPosA, ref PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue) {
			PathUnit.Position def = default(PathUnit.Position);
			return this.CreatePath(recalc, vehicleType, out unit, ref randomizer, buildIndex, ref startPosA, ref startPosB, ref endPosA, ref endPosB, ref def, laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, false);
		}

		public bool CreatePath(bool recalc, ExtVehicleType vehicleType, out uint unit, ref Randomizer randomizer, uint buildIndex, ref PathUnit.Position startPosA, ref PathUnit.Position startPosB, ref PathUnit.Position endPosA, ref PathUnit.Position endPosB, ref PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, bool randomParking) {
			uint num;
			try {
				Monitor.Enter(this.m_bufferLock);
				if (!this.m_pathUnits.CreateItem(out num, ref randomizer)) {
					unit = 0u;
					bool result = false;
					return result;
				}
				this.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1u);
			} finally {
				Monitor.Exit(this.m_bufferLock);
			}
			unit = num;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags = 1;
			if (isHeavyVehicle) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 16;
			}
			if (ignoreBlocked) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 32;
			}
			if (stablePath) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 64;
			}
			if (randomParking) {
				this.m_pathUnits.m_buffer[unit].m_simulationFlags |= 128;
			}
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags = 0;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_buildIndex = buildIndex;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position00 = startPosA;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position01 = endPosA;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position02 = startPosB;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position03 = endPosB;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position11 = vehiclePosition;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = 0u;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes = (byte)laneTypes;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes = (byte)vehicleTypes;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = maxLength;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount = 20;
			this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_referenceCount = 1;
			int minQueued = 10000000;
			CustomPathFind pathFind = null;
#if QUEUEDSTATS
			TotalQueuedPathFinds = 0;
#if EXTRAPF
			ExtraQueuedPathFinds = 0;
#endif
#endif
			for (int i = 0; i < _replacementPathFinds.Length; i++) {
				CustomPathFind pathFindCandidate = _replacementPathFinds[i];
#if QUEUEDSTATS
				TotalQueuedPathFinds += (uint)pathFindCandidate.m_queuedPathFindCount;
#if EXTRAPF
				if (pathFindCandidate.IsExtraPathFind)
					ExtraQueuedPathFinds += (uint)pathFindCandidate.m_queuedPathFindCount;
#endif
#endif
				if (pathFindCandidate.IsAvailable
#if EXTRAPF
					&& (!pathFindCandidate.IsExtraPathFind || recalc)
#endif
					&& pathFindCandidate.m_queuedPathFindCount < minQueued) {
					minQueued = pathFindCandidate.m_queuedPathFindCount;
					pathFind = pathFindCandidate;
				}
			}
			if (pathFind != null && pathFind.CalculatePath(vehicleType, unit, skipQueue)) {
				return true;
			}
			this.ReleasePath(unit);
			return false;
		}

		private void StopPathFinds() {
			foreach (CustomPathFind pathFind in _replacementPathFinds) {
				UnityEngine.Object.Destroy(pathFind);
			}
		}

		protected virtual void OnDestroy() {
			Log._Debug("CustomPathManager: OnDestroy");
			StopPathFinds();
		}
	}
}
