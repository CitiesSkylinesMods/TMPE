using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using UnityEngine;

namespace TrafficManager.Custom.Manager {
	public class CustomVehicleManager : VehicleManager {
		public void CustomReleaseVehicle(ushort vehicleId) {
#if DEBUG
			//Log._Debug($"CustomVehicleManager.CustomReleaseVehicle({vehicleId})");
#endif
			VehicleStateManager.Instance.OnReleaseVehicle(vehicleId);
			ReleaseVehicleImplementation(vehicleId, ref this.m_vehicles.m_buffer[(int)vehicleId]);
		}

		private void ReleaseVehicleImplementation(ushort vehicleId, ref Vehicle vehicleData) {
			Log.Error("CustomVehicleManager.ReleaseVehicleImplementation called.");
		}

		public bool CustomCreateVehicle(out ushort vehicleId, ref Randomizer r, VehicleInfo info, Vector3 position, TransferManager.TransferReason type, bool transferToSource, bool transferToTarget) {
			// NON-STOCK CODE START
			if (this.m_vehicleCount > VehicleManager.MAX_VEHICLE_COUNT - 5) {
				// prioritize service vehicles and public transport when hitting the vehicle limit
				ItemClass.Service service = info.GetService();
				if (service == ItemClass.Service.Residential || service == ItemClass.Service.Industrial || service == ItemClass.Service.Commercial || service == ItemClass.Service.Office) {
					vehicleId = 0;
					return false;
				}
			}
			// NON-STOCK CODE END

			ushort vehId;
			if (this.m_vehicles.CreateItem(out vehId, ref r)) {
				vehicleId = vehId;
				Vehicle.Frame frame = new Vehicle.Frame(position, Quaternion.identity);
				this.m_vehicles.m_buffer[(int)vehicleId].m_flags = Vehicle.Flags.Created;
				this.m_vehicles.m_buffer[(int)vehicleId].m_flags2 = (Vehicle.Flags2)0;
				if (transferToSource) {
					this.m_vehicles.m_buffer[vehicleId].m_flags = (this.m_vehicles.m_buffer[vehicleId].m_flags | Vehicle.Flags.TransferToSource);
				}
				if (transferToTarget) {
					this.m_vehicles.m_buffer[vehicleId].m_flags = (this.m_vehicles.m_buffer[vehicleId].m_flags | Vehicle.Flags.TransferToTarget);
				}
				this.m_vehicles.m_buffer[(int)vehicleId].Info = info;
				this.m_vehicles.m_buffer[(int)vehicleId].m_frame0 = frame;
				this.m_vehicles.m_buffer[(int)vehicleId].m_frame1 = frame;
				this.m_vehicles.m_buffer[(int)vehicleId].m_frame2 = frame;
				this.m_vehicles.m_buffer[(int)vehicleId].m_frame3 = frame;
				this.m_vehicles.m_buffer[(int)vehicleId].m_targetPos0 = Vector4.zero;
				this.m_vehicles.m_buffer[(int)vehicleId].m_targetPos1 = Vector4.zero;
				this.m_vehicles.m_buffer[(int)vehicleId].m_targetPos2 = Vector4.zero;
				this.m_vehicles.m_buffer[(int)vehicleId].m_targetPos3 = Vector4.zero;
				this.m_vehicles.m_buffer[(int)vehicleId].m_sourceBuilding = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_targetBuilding = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_transferType = (byte)type;
				this.m_vehicles.m_buffer[(int)vehicleId].m_transferSize = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_waitCounter = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_blockCounter = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_nextGridVehicle = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_nextOwnVehicle = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_nextGuestVehicle = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_nextLineVehicle = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_transportLine = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_leadingVehicle = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_trailingVehicle = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_cargoParent = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_firstCargo = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_nextCargo = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_citizenUnits = 0u;
				this.m_vehicles.m_buffer[(int)vehicleId].m_path = 0u;
				this.m_vehicles.m_buffer[(int)vehicleId].m_lastFrame = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_pathPositionIndex = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_lastPathOffset = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_gateIndex = 0;
				this.m_vehicles.m_buffer[(int)vehicleId].m_waterSource = 0;
				info.m_vehicleAI.CreateVehicle(vehicleId, ref this.m_vehicles.m_buffer[vehicleId]);
				info.m_vehicleAI.FrameDataUpdated(vehicleId, ref this.m_vehicles.m_buffer[vehicleId], ref this.m_vehicles.m_buffer[vehicleId].m_frame0);
				this.m_vehicleCount = (int)(this.m_vehicles.ItemCount() - 1u);

				VehicleStateManager.Instance.DetermineVehicleType(vehicleId, ref this.m_vehicles.m_buffer[vehicleId]); // NON-STOCK CODE

				return true;
			}
			vehicleId = 0;
			return false;
		}
	}
}
