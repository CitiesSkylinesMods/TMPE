using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;

namespace TrafficManager.Manager {
	public enum VehicleRestrictionsMode {
		/// <summary>
		/// Interpret bus lanes as "free for all"
		/// </summary>
		Unrestricted,
		/// <summary>
		/// Interpret bus lanes according to the configuration
		/// </summary>
		Configured,
		/// <summary>
		/// Interpret bus lanes as restricted
		/// </summary>
		Restricted
	}

	/// <summary>
	/// Represents vehicle restrictions effect strength
	/// </summary>
	public enum VehicleRestrictionsAggression {
		/// <summary>
		/// Low aggression
		/// </summary>
		Low = 0,
		/// <summary>
		/// Medium aggression
		/// </summary>
		Medium = 1,
		/// <summary>
		/// High aggression
		/// </summary>
		High = 2,
		/// <summary>
		/// Strict aggression
		/// </summary>
		Strict = 3
	}

	public interface IVehicleRestrictionsManager {
		// TODO documentation
		void AddAllowedType(ushort segmentId, NetInfo segmentInfo, uint laneIndex, uint laneId, NetInfo.Lane laneInfo, ExtVehicleType vehicleType);
		ExtVehicleType GetAllowedVehicleTypes(ushort segmentId, ushort nodeId, VehicleRestrictionsMode busLaneMode);
		ExtVehicleType GetAllowedVehicleTypes(ushort segmentId, NetInfo segmentInfo, uint laneIndex, NetInfo.Lane laneInfo, VehicleRestrictionsMode busLaneMode);
		IDictionary<byte, ExtVehicleType> GetAllowedVehicleTypesAsDict(ushort segmentId, ushort nodeId, VehicleRestrictionsMode busLaneMode);
		HashSet<ExtVehicleType> GetAllowedVehicleTypesAsSet(ushort segmentId, ushort nodeId, VehicleRestrictionsMode busLaneMode);
		ExtVehicleType GetBaseMask(uint laneId, VehicleRestrictionsMode includeBusLanes);
		ExtVehicleType GetBaseMask(NetInfo.Lane laneInfo, VehicleRestrictionsMode includeBusLanes);
		ExtVehicleType GetDefaultAllowedVehicleTypes(NetInfo.Lane laneInfo, VehicleRestrictionsMode busLaneMode);
		ExtVehicleType GetDefaultAllowedVehicleTypes(ushort segmentId, NetInfo segmentInfo, uint laneIndex, NetInfo.Lane laneInfo, VehicleRestrictionsMode busLaneMode);
		bool IsAllowed(ExtVehicleType? allowedTypes, ExtVehicleType vehicleType);
		bool IsBicycleAllowed(ExtVehicleType? allowedTypes);
		bool IsBlimpAllowed(ExtVehicleType? allowedTypes);
		bool IsBusAllowed(ExtVehicleType? allowedTypes);
		bool IsCableCarAllowed(ExtVehicleType? allowedTypes);
		bool IsCargoTrainAllowed(ExtVehicleType? allowedTypes);
		bool IsCargoTruckAllowed(ExtVehicleType? allowedTypes);
		bool IsEmergencyAllowed(ExtVehicleType? allowedTypes);
		bool IsFerryAllowed(ExtVehicleType? allowedTypes);
		bool IsMonorailSegment(NetInfo segmentInfo);
		bool IsPassengerCarAllowed(ExtVehicleType? allowedTypes);
		bool IsPassengerTrainAllowed(ExtVehicleType? allowedTypes);
		bool IsRailLane(NetInfo.Lane laneInfo);
		bool IsRailSegment(NetInfo segmentInfo);
		bool IsRailVehicleAllowed(ExtVehicleType? allowedTypes);
		bool IsRoadLane(NetInfo.Lane laneInfo);
		bool IsRoadSegment(NetInfo segmentInfo);
		bool IsRoadVehicleAllowed(ExtVehicleType? allowedTypes);
		bool IsServiceAllowed(ExtVehicleType? allowedTypes);
		bool IsTaxiAllowed(ExtVehicleType? allowedTypes);
		bool IsTramAllowed(ExtVehicleType? allowedTypes);
		bool IsTramLane(NetInfo.Lane laneInfo);
		bool LoadData(List<Configuration.LaneVehicleTypes> data);
		void NotifyStartEndNode(ushort segmentId);
		void OnLevelUnloading();
		void RemoveAllowedType(ushort segmentId, NetInfo segmentInfo, uint laneIndex, uint laneId, NetInfo.Lane laneInfo, ExtVehicleType vehicleType);
		List<Configuration.LaneVehicleTypes> SaveData(ref bool success);
		void ToggleAllowedType(ushort segmentId, NetInfo segmentInfo, uint laneIndex, uint laneId, NetInfo.Lane laneInfo, ExtVehicleType vehicleType, bool add);
	}
}
