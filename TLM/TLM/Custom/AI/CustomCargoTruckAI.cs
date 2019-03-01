using System;
using ColossalFramework;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using TrafficManager.Traffic.Data;
using CSUtil.Commons.Benchmark;
using static TrafficManager.Custom.PathFinding.CustomPathManager;

namespace TrafficManager.Custom.AI {
	public class CustomCargoTruckAI : CarAI {
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			try {
				// NON-STOCK CODE START
				bool mayDespawn = true;
#if BENCHMARK
				using (var bm = new Benchmark(null, "MayDespawn")) {
#endif
					mayDespawn = (vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 && VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData);
#if BENCHMARK
				}
#endif
				// NON-STOCK CODE END

				if (mayDespawn) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				} else {
					if ((vehicleData.m_flags & Vehicle.Flags.WaitingTarget) != 0 && (vehicleData.m_waitCounter += 1) > 20) {
						RemoveOffers(vehicleId, ref vehicleData);
						vehicleData.m_flags &= ~Vehicle.Flags.WaitingTarget;
						vehicleData.m_flags |= Vehicle.Flags.GoingBack;
						vehicleData.m_waitCounter = 0;
						if (!StartPathFind(vehicleId, ref vehicleData)) {
							vehicleData.Unspawn(vehicleId);
						}
					}

					base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
				}
			} catch (Exception ex) {
				Log.Error("Error in CargoTruckAI.SimulationStep: " + ex.ToString());
			}
		}

		// stock code
		private static void RemoveOffers(ushort vehicleId, ref Vehicle data) {
			if ((data.m_flags & Vehicle.Flags.WaitingTarget) != (Vehicle.Flags)0) {
				var offer = default(TransferManager.TransferOffer);
				offer.Vehicle = vehicleId;
				if ((data.m_flags & Vehicle.Flags.TransferToSource) != (Vehicle.Flags)0) {
					Singleton<TransferManager>.instance.RemoveIncomingOffer((TransferManager.TransferReason)data.m_transferType, offer);
				} else if ((data.m_flags & Vehicle.Flags.TransferToTarget) != (Vehicle.Flags)0) {
					Singleton<TransferManager>.instance.RemoveOutgoingOffer((TransferManager.TransferReason)data.m_transferType, offer);
				}
			}
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
			//Log._Debug($"CustomCargoTruckAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif

#if BENCHMARK
			using (var bm = new Benchmark(null, "OnStartPathFind")) {
#endif
				ExtVehicleType vehicleType = VehicleStateManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
				if (vehicleType == ExtVehicleType.None) {
#if DEBUG
					Log.Warning($"CustomCargoTruck.CustomStartPathFind: Vehicle {vehicleID} does not have a valid vehicle type!");
#endif
				}
#if BENCHMARK
			}
#endif

			if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) != 0) {
				return base.StartPathFind(vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, undergroundTarget);
			}

			bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float startDistSqrA;
			float startDistSqrB;
			bool startPosFound = CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, allowUnderground, false, 32f, out startPosA, out startPosB, out startDistSqrA, out startDistSqrB);
			PathUnit.Position startAltPosA;
			PathUnit.Position startAltPosB;
			float startAltDistSqrA;
			float startAltDistSqrB;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship | VehicleInfo.VehicleType.Plane, allowUnderground, false, 32f, out startAltPosA, out startAltPosB, out startAltDistSqrA, out startAltDistSqrB)) {
				if (!startPosFound || (startAltDistSqrA < startDistSqrA && (Mathf.Abs(endPos.x) > 8000f || Mathf.Abs(endPos.z) > 8000f))) {
					startPosA = startAltPosA;
					startPosB = startAltPosB;
					startDistSqrA = startAltDistSqrA;
					startDistSqrB = startAltDistSqrB;
				}
				startPosFound = true;
			}
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float endDistSqrA;
			float endDistSqrB;
			bool endPosFound = CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, undergroundTarget, false, 32f, out endPosA, out endPosB, out endDistSqrA, out endDistSqrB);
			PathUnit.Position endAltPosA;
			PathUnit.Position endAltPosB;
			float endAltDistSqrA;
			float endAltDistSqrB;
			if (CustomPathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship | VehicleInfo.VehicleType.Plane, undergroundTarget, false, 32f, out endAltPosA, out endAltPosB, out endAltDistSqrA, out endAltDistSqrB)) {
				if (!endPosFound || (endAltDistSqrA < endDistSqrA && (Mathf.Abs(endPos.x) > 8000f || Mathf.Abs(endPos.z) > 8000f))) {
					endPosA = endAltPosA;
					endPosB = endAltPosB;
					endDistSqrA = endAltDistSqrA;
					endDistSqrB = endAltDistSqrB;
				}
				endPosFound = true;
			}
			if (startPosFound && endPosFound) {
				CustomPathManager pathMan = CustomPathManager._instance;
				if (!startBothWays || startDistSqrA < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || endDistSqrA < 10f) {
					endPosB = default(PathUnit.Position);
				}
				NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle;
				VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship | VehicleInfo.VehicleType.Plane;
				uint path;
				// NON-STOCK CODE START
				PathCreationArgs args;
				args.extPathType = ExtCitizenInstance.ExtPathType.None;
				args.extVehicleType = ExtVehicleType.CargoVehicle;
				args.vehicleId = vehicleID;
				args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
				args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
				args.startPosA = startPosA;
				args.startPosB = startPosB;
				args.endPosA = endPosA;
				args.endPosB = endPosB;
				args.vehiclePosition = default(PathUnit.Position);
				args.laneTypes = laneTypes;
				args.vehicleTypes = vehicleTypes;
				args.maxLength = 20000f;
				args.isHeavyVehicle = this.IsHeavyVehicle();
				args.hasCombustionEngine = this.CombustionEngine();
				args.ignoreBlocked = this.IgnoreBlocked(vehicleID, ref vehicleData);
				args.ignoreFlooded = false;
				args.ignoreCosts = false;
				args.randomParking = false;
				args.stablePath = false;
				args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

				if (pathMan.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args)) {
					// NON-STOCK CODE END
					if (vehicleData.m_path != 0u) {
						pathMan.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}
	}
}
