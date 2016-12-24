#define PATHRECALCx

using System;
using ColossalFramework;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Traffic;
using TrafficManager.Manager;

namespace TrafficManager.Custom.AI {
	public class CustomCargoTruckAI : CarAI {
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle data, Vector3 physicsLodRefPos) {
			try {
				if ((data.m_flags & Vehicle.Flags.Congestion) != 0 && Options.enableDespawning) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				} else {
					if ((data.m_flags & Vehicle.Flags.WaitingTarget) != 0 && (data.m_waitCounter += 1) > 20) {
						RemoveOffers(vehicleId, ref data);
						data.m_flags &= ~Vehicle.Flags.WaitingTarget;
						data.m_flags |= Vehicle.Flags.GoingBack;
						data.m_waitCounter = 0;
						if (!StartPathFind(vehicleId, ref data)) {
							data.Unspawn(vehicleId);
						}
					}

					base.SimulationStep(vehicleId, ref data, physicsLodRefPos);
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
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, allowUnderground, false, 32f, out startAltPosA, out startAltPosB, out startAltDistSqrA, out startAltDistSqrB)) {
				if (!startPosFound || startAltDistSqrA < startDistSqrA) {
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
			if (CustomPathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, undergroundTarget, false, 32f, out endAltPosA, out endAltPosB, out endAltDistSqrA, out endAltDistSqrB)) {
				if (!endPosFound || endAltDistSqrA < endDistSqrA) {
					endPosA = endAltPosA;
					endPosB = endAltPosB;
					endDistSqrA = endAltDistSqrA;
					endDistSqrB = endAltDistSqrB;
				}
				endPosFound = true;
			}
			if (startPosFound && endPosFound) {
				CustomPathManager instance = CustomPathManager._instance;
				if (!startBothWays || startDistSqrA < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || endDistSqrA < 10f) {
					endPosB = default(PathUnit.Position);
				}
				NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle;
				VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship;
				uint path;
				if (instance.CreatePath(ExtVehicleType.CargoVehicle, vehicleID, ExtCitizenInstance.ExtPathType.None, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleTypes, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false)) {
#if USEPATHWAITCOUNTER
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleID);
					state.PathWaitCounter = 0;
#endif

					if (vehicleData.m_path != 0u) {
						instance.ReleasePath(vehicleData.m_path);
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
