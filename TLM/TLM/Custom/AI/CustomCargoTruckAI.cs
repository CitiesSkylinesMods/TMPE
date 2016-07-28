#define PATHRECALCx

using System;
using ColossalFramework;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Custom.PathFinding;

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
			if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) != 0) {
				return base.StartPathFind(vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, undergroundTarget);
			}

			VehicleStateManager.Instance()._GetVehicleState(vehicleID).VehicleType = ExtVehicleType.CargoTruck;

			bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			bool startPosFound = CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2);
			PathUnit.Position position;
			PathUnit.Position position2;
			float num3;
			float num4;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, allowUnderground, false, 32f, out position, out position2, out num3, out num4)) {
				if (!startPosFound || num3 < num) {
					startPosA = position;
					startPosB = position2;
					num = num3;
					num2 = num4;
				}
				startPosFound = true;
			}
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float num5;
			float num6;
			bool endPosFound = CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, undergroundTarget, false, 32f, out endPosA, out endPosB, out num5, out num6);
			PathUnit.Position position3;
			PathUnit.Position position4;
			float num7;
			float num8;
			if (CustomPathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, undergroundTarget, false, 32f, out position3, out position4, out num7, out num8)) {
				if (!endPosFound || num7 < num5) {
					endPosA = position3;
					endPosB = position4;
					num5 = num7;
					num6 = num8;
				}
				endPosFound = true;
			}
			if (startPosFound && endPosFound) {
				CustomPathManager instance = Singleton<CustomPathManager>.instance;
				if (!startBothWays || num < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || num5 < 10f) {
					endPosB = default(PathUnit.Position);
				}
				NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle;
				VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship;
				uint path;
				if (instance.CreatePath(ExtVehicleType.CargoVehicle, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleTypes, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false)) {
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
