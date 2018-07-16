using System;
using ColossalFramework;
using UnityEngine;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using ColossalFramework.Math;
using TrafficManager.Util;
using System.Reflection;
using ColossalFramework.Globalization;
using TrafficManager.UI;
using System.Xml;
using System.IO;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using TrafficManager.Traffic.Data;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;
using CSUtil.Commons.Benchmark;
using System.Runtime.CompilerServices;
using static TrafficManager.Custom.PathFinding.CustomPathManager;
using TrafficManager.Traffic.Enums;
using TrafficManager.RedirectionFramework.Attributes;

namespace TrafficManager.Custom.AI {
	// TODO move Parking AI features from here to a distinct manager
	[TargetType(typeof(PassengerCarAI))]
	public class CustomPassengerCarAI : CarAI {
		[RedirectMethod]
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			bool mayDespawn = (vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 && VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData);

			if (mayDespawn) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else {
				base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
			}
		}

		[RedirectMethod]
		public string CustomGetLocalizedStatus(ushort vehicleID, ref Vehicle data, out InstanceID target) {
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			ushort driverInstanceId = GetDriverInstance(vehicleID, ref data);
			ushort targetBuildingId = 0;
			bool targetIsNode = false;
			if (driverInstanceId != 0) {
				if ((data.m_flags & Vehicle.Flags.Parking) != (Vehicle.Flags)0) {
					uint citizen = citizenManager.m_instances.m_buffer[(int)driverInstanceId].m_citizen;
					if (citizen != 0u && citizenManager.m_citizens.m_buffer[citizen].m_parkedVehicle != 0) {
						target = InstanceID.Empty;
						return Locale.Get("VEHICLE_STATUS_PARKING");
					}
				}
				targetBuildingId = citizenManager.m_instances.m_buffer[(int)driverInstanceId].m_targetBuilding;
				targetIsNode = ((citizenManager.m_instances.m_buffer[driverInstanceId].m_flags & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None);
			}
			if (targetBuildingId == 0) {
				target = InstanceID.Empty;
				return Locale.Get("VEHICLE_STATUS_CONFUSED");
			}
			bool leavingCity = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			if (leavingCity) {
				target = InstanceID.Empty;
				return Locale.Get("VEHICLE_STATUS_LEAVING");
			}
			target = InstanceID.Empty;
			if (targetIsNode) {
				target.NetNode = targetBuildingId;
			} else {
				target.Building = targetBuildingId;
			}

			string ret = Locale.Get("VEHICLE_STATUS_GOINGTO");

			// NON-STOCK CODE START
			if (Options.parkingAI) {
				ret = AdvancedParkingManager.Instance.EnrichLocalizedCarStatus(ret, ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId]);
			}
			// NON-STOCK CODE END

			return ret;
		}

		[RedirectMethod]
		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
			ushort driverInstanceId = GetDriverInstance(vehicleID, ref vehicleData);
			if (driverInstanceId == 0) {
				return false;
			}

			return Constants.ManagerFactory.VehicleBehaviorManager.StartPassengerCarPathFind(vehicleID, ref vehicleData, this.m_info, driverInstanceId, ref Singleton<CitizenManager>.instance.m_instances.m_buffer[driverInstanceId], ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId], startPos, endPos, startBothWays, endBothWays, undergroundTarget, IsHeavyVehicle(), CombustionEngine(), IgnoreBlocked(vehicleID, ref vehicleData));
		}
		
		[RedirectMethod]
		public void CustomUpdateParkedVehicle(ushort parkedId, ref VehicleParked data) {
			float x = this.m_info.m_generatedInfo.m_size.x;
			float z = this.m_info.m_generatedInfo.m_size.z;
			uint ownerCitizenId = data.m_ownerCitizen;
			ushort homeID = 0;
			if (ownerCitizenId != 0u) {
				homeID = Singleton<CitizenManager>.instance.m_citizens.m_buffer[ownerCitizenId].m_homeBuilding;
			}

			// NON-STOCK CODE START
			if (!AdvancedParkingManager.Instance.TryMoveParkedVehicle(parkedId, ref data, data.m_position, GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding, homeID)) {
				Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedId);
			}
			// NON-STOCK CODE END
		}

		[RedirectMethod]
		public bool CustomParkVehicle(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset) {
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;

			// TODO remove this:
			ushort driverCitizenInstanceId = GetDriverInstance(vehicleID, ref vehicleData);
			uint driverCitizenId = citizenManager.m_instances.m_buffer[(int)driverCitizenInstanceId].m_citizen;
			ushort targetBuildingId = citizenManager.m_instances.m_buffer[(int)driverCitizenInstanceId].m_targetBuilding; // NON-STOCK CODE
			
#if BENCHMARK
			using (var bm = new Benchmark(null, "ExtParkVehicle")) {
#endif
				return Constants.ManagerFactory.VehicleBehaviorManager.ParkPassengerCar(vehicleID, ref vehicleData, this.m_info, driverCitizenId, driverCitizenInstanceId, ref ExtCitizenInstanceManager.Instance.ExtInstances[driverCitizenInstanceId], targetBuildingId, pathPos, nextPath, nextPositionIndex, out segmentOffset);
#if BENCHMARK
			}
#endif
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private ushort GetDriverInstance(ushort vehicleID, ref Vehicle data) {
			Log.Error("GetDriverInstance is not overridden!");
			return 0;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool FindParkingSpaceRoadSide(ushort ignoreParked, ushort requireSegment, Vector3 refPos, float width, float length, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Log.Error("FindParkingSpaceRoadSide is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;
			return false;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool FindParkingSpace(bool isElectric, ushort homeID, Vector3 refPos, Vector3 searchDir, ushort segment, float width, float length, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Log.Error("FindParkingSpace is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;
			return false;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool FindParkingSpaceProp(bool isElectric, ushort ignoreParked, PropInfo info, Vector3 position, float angle, bool fixedHeight, Vector3 refPos, float width, float length, ref float maxDistance, ref Vector3 parkPos, ref Quaternion parkRot) {
			Log.Error("FindParkingSpaceProp is not overridden!");
			return false;
		}
	}
}
