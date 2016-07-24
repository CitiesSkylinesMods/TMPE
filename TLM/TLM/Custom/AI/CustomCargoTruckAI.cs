#define PATHRECALCx

using System;
using ColossalFramework;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Custom.PathFinding;

namespace TrafficManager.Custom.AI {
	class CustomCargoTruckAI : CarAI {
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

					/*try {
						VehicleStateManager.LogTraffic(vehicleId, ref data, true);
					} catch (Exception e) {
						Log.Error("CargoTruckAI CustomSimulationStep Error: " + e.ToString());
					}*/

					base.SimulationStep(vehicleId, ref data, physicsLodRefPos);
					//BaseSimulationStep(vehicleId, ref data, physicsLodRefPos);
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

		/*private void BaseSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != (Vehicle.Flags)0) {
				PathManager instance = Singleton<PathManager>.instance;
				byte pathFindFlags = instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].m_pathFindFlags;
				if ((pathFindFlags & 4) != 0) {
					vehicleData.m_pathPositionIndex = 255;
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					vehicleData.m_flags &= ~Vehicle.Flags.Arriving;
					PathfindSuccess(vehicleId, ref vehicleData);
					TrySpawn(vehicleId, ref vehicleData);
					VehicleStateManager.OnPathFindReady(vehicleId, ref vehicleData); // NON-STOCK CODE
				} else if ((pathFindFlags & 8) != 0 || ((pathFindFlags & 1) != 0 && vehicleData.m_blockCounter == 255)) { // NON-STOCK CODE
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					vehicleData.m_path = 0u;
					PathfindFailure(vehicleId, ref vehicleData);
					return;
				}
			} else if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
				TrySpawn(vehicleId, ref vehicleData);
			}

			try {
				VehicleStateManager.LogTraffic(vehicleId, ref vehicleData, true);
			} catch (Exception e) {
				Log.Error("CarAI CustomSimulationStep Error: " + e.ToString());
			}

			try {
				VehicleStateManager.UpdateVehiclePos(vehicleId, ref vehicleData);
			} catch (Exception e) {
				Log.Error("CarAI CustomSimulationStep Error: " + e.ToString());
			}

			Vector3 lastFramePosition = vehicleData.GetLastFramePosition();
			int lodPhysics;
			if (Vector3.SqrMagnitude(physicsLodRefPos - lastFramePosition) >= 1210000f) {
				lodPhysics = 2;
			} else if (
				  Vector3.SqrMagnitude(Singleton<SimulationManager>.instance.m_simulationView.m_position - lastFramePosition) >= 250000f) {
				lodPhysics = 1;
			} else {
				lodPhysics = 0;
			}
			SimulationStep(vehicleId, ref vehicleData, vehicleId, ref vehicleData, lodPhysics);
			if (vehicleData.m_leadingVehicle == 0 && vehicleData.m_trailingVehicle != 0) {
				VehicleManager instance2 = Singleton<VehicleManager>.instance;
				ushort num = vehicleData.m_trailingVehicle;
				int num2 = 0;
				while (num != 0) {
					ushort trailingVehicle = instance2.m_vehicles.m_buffer[num].m_trailingVehicle;
					VehicleInfo info = instance2.m_vehicles.m_buffer[num].Info;
					info.m_vehicleAI.SimulationStep(num, ref instance2.m_vehicles.m_buffer[num], vehicleId,
						ref vehicleData, lodPhysics);
					num = trailingVehicle;
					if (++num2 > 16384) {
						CODebugBase<LogChannel>.Error(LogChannel.Core,
							"Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
			int maxBlockCounter = (m_info.m_class.m_service > ItemClass.Service.Office) ? 150 : 100;
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) ==
				0 && vehicleData.m_cargoParent == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else if (vehicleData.m_blockCounter >= maxBlockCounter && Options.enableDespawning) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else if (vehicleData.m_leadingVehicle == 0 && CustomVehicleAI.ShouldRecalculatePath(vehicleId, ref vehicleData, maxBlockCounter)) {
				CustomVehicleAI.MarkPathRecalculation(vehicleId);
				InvalidPath(vehicleId, ref vehicleData, vehicleId, ref vehicleData);
			}
		}*/

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
			if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) != 0) {
				return CustomCargoTruckAI.BaseCustomStartPathFind(this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), ref this.m_info, vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays);
			}
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
				if (instance.CreatePath(ExtVehicleType.CargoVehicle, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, ref startPosA, ref startPosB, ref endPosA, ref endPosB, laneTypes, vehicleTypes, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false)) {
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

		public static bool BaseCustomStartPathFind(bool heavyVehicle, bool ignoreBlocked, ref VehicleInfo info, ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays) {
#if PATHRECALC
			VehicleState state = VehicleStateManager._GetVehicleState(vehicleID);
			bool recalcRequested = state.PathRecalculationRequested;
			state.PathRecalculationRequested = false;
#endif

			bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float num3;
			float num4;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2) &&
				CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, false, false, 32f, out endPosA, out endPosB, out num3, out num4)) {
				if (!startBothWays || num < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || num3 < 10f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				ExtVehicleType? extVehicleType = VehicleStateManager.DetermineVehicleType(vehicleID, ref vehicleData);
				bool res = false;
				if (extVehicleType == null)
					res = Singleton<PathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, heavyVehicle, ignoreBlocked, false, false);
				else
					res = Singleton<CustomPathManager>.instance.CreatePath(
#if PATHRECALC
						recalcRequested,
#endif
						(ExtVehicleType)extVehicleType, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, ref startPosA, ref startPosB, ref endPosA, ref endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, heavyVehicle, ignoreBlocked, false, false);
				if (res) {
					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
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
