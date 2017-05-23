using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	public class VehicleRestrictionsManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<Configuration.LaneVehicleTypes>> {
		public enum RestrictionMode {
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

		public const NetInfo.LaneType LANE_TYPES = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
		public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram;
		public const ExtVehicleType EXT_VEHICLE_TYPES = ExtVehicleType.PassengerTrain | ExtVehicleType.CargoTrain | ExtVehicleType.PassengerCar | ExtVehicleType.Bus | ExtVehicleType.Taxi | ExtVehicleType.CargoTruck | ExtVehicleType.Service | ExtVehicleType.Emergency;

		public static readonly VehicleRestrictionsManager Instance = new VehicleRestrictionsManager();

		private VehicleRestrictionsManager() {

		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"- Not implemented -");
			// TODO implement
		}

		/// <summary>
		/// For each segment id and lane index: Holds the default set of vehicle types allowed for the lane
		/// </summary>
		private ExtVehicleType?[][][] defaultVehicleTypeCache = null;

		/// <summary>
		/// Determines the allowed vehicle types that may approach the given node from the given segment.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="nodeId"></param>
		/// <returns></returns>
		[Obsolete]
		internal ExtVehicleType GetAllowedVehicleTypes(ushort segmentId, ushort nodeId, RestrictionMode busLaneMode) { // TODO optimize method (don't depend on collections!)
			ExtVehicleType ret = ExtVehicleType.None;
			foreach (ExtVehicleType vehicleType in GetAllowedVehicleTypesAsSet(segmentId, nodeId, busLaneMode)) {
				ret |= vehicleType;
			}
			return ret;
		}

		/// <summary>
		/// Determines the allowed vehicle types that may approach the given node from the given segment.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="nodeId"></param>
		/// <returns></returns>
		[Obsolete]
		internal HashSet<ExtVehicleType> GetAllowedVehicleTypesAsSet(ushort segmentId, ushort nodeId, RestrictionMode busLaneMode) {
			HashSet<ExtVehicleType> ret = new HashSet<ExtVehicleType>(GetAllowedVehicleTypesAsDict(segmentId, nodeId, busLaneMode).Values);
			return ret;
		}

		/// <summary>
		/// Determines the allowed vehicle types that may approach the given node from the given segment (lane-wise).
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="nodeId"></param>
		/// <returns></returns>
		internal IDictionary<byte, ExtVehicleType> GetAllowedVehicleTypesAsDict(ushort segmentId, ushort nodeId, RestrictionMode busLaneMode) {
			IDictionary<byte, ExtVehicleType> ret = new TinyDictionary<byte, ExtVehicleType>();

			NetManager netManager = Singleton<NetManager>.instance;
			if (segmentId == 0 || (netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None ||
				nodeId == 0 || (netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return ret;
			}

			var dir = NetInfo.Direction.Forward;
			var dir2 = ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);

			NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;
			uint laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				if (laneInfo.m_vehicleType != VehicleInfo.VehicleType.None) {
					ushort toNodeId = (laneInfo.m_finalDirection & dir2) != NetInfo.Direction.None ? netManager.m_segments.m_buffer[segmentId].m_endNode : netManager.m_segments.m_buffer[segmentId].m_startNode;
					if ((laneInfo.m_finalDirection & NetInfo.Direction.Both) == NetInfo.Direction.Both || toNodeId == nodeId) {
						ExtVehicleType vehicleTypes = GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, busLaneMode);
						ret[(byte)laneIndex] = vehicleTypes;
					}
				}
				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}

			return ret;
		}

		/// <summary>
		/// Determines the allowed vehicle types for the given segment and lane.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="laneIndex"></param>
		/// <param name="segmentInfo"></param>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		internal ExtVehicleType GetAllowedVehicleTypes(ushort segmentId, NetInfo segmentInfo, uint laneIndex, NetInfo.Lane laneInfo, RestrictionMode busLaneMode) {
			ExtVehicleType?[] fastArray = Flags.laneAllowedVehicleTypesArray[segmentId];
			if (fastArray != null && fastArray.Length > laneIndex && fastArray[laneIndex] != null) {
				return (ExtVehicleType)fastArray[laneIndex];
			}

			return GetDefaultAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, busLaneMode);
		}

		internal bool HasSegmentRestrictions(ushort segmentId) { // TODO clean up restrictions (currently we do not check if restrictions are equal with the base type)
			bool ret = false;
			Services.NetService.IterateSegmentLanes(segmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segId, ref NetSegment segment, byte laneIndex) {
				ExtVehicleType defaultMask = GetDefaultAllowedVehicleTypes(laneInfo, RestrictionMode.Unrestricted);
				ExtVehicleType currentMask = GetAllowedVehicleTypes(segmentId, segment.Info, laneIndex, laneInfo, RestrictionMode.Configured);

				if (defaultMask != currentMask) {
					ret = true;
					return false;
				}
				return true;
			});

			return ret;
		}

		/// <summary>
		/// Determines the default set of allowed vehicle types for a given segment and lane.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="segmentInfo"></param>
		/// <param name="laneIndex"></param>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		public ExtVehicleType GetDefaultAllowedVehicleTypes(ushort segmentId, NetInfo segmentInfo, uint laneIndex, NetInfo.Lane laneInfo, RestrictionMode busLaneMode) {
			// manage cached default vehicle types
			if (defaultVehicleTypeCache == null) {
				defaultVehicleTypeCache = new ExtVehicleType?[NetManager.MAX_SEGMENT_COUNT][][];
			}

			ExtVehicleType?[] cachedDefaultTypes = null;
			int cacheIndex = (int)busLaneMode;
			
			if (defaultVehicleTypeCache[segmentId] != null) {
				cachedDefaultTypes = defaultVehicleTypeCache[segmentId][cacheIndex];
			}

			if (cachedDefaultTypes == null || cachedDefaultTypes.Length != segmentInfo.m_lanes.Length) {
				defaultVehicleTypeCache[segmentId] = new ExtVehicleType?[3][];
				defaultVehicleTypeCache[segmentId][0] = new ExtVehicleType?[segmentInfo.m_lanes.Length];
				defaultVehicleTypeCache[segmentId][1] = new ExtVehicleType?[segmentInfo.m_lanes.Length];
				defaultVehicleTypeCache[segmentId][2] = new ExtVehicleType?[segmentInfo.m_lanes.Length];

				cachedDefaultTypes = defaultVehicleTypeCache[segmentId][cacheIndex];
			}

			ExtVehicleType? defaultVehicleType = cachedDefaultTypes[laneIndex];
			if (defaultVehicleType == null) {
				defaultVehicleType = GetDefaultAllowedVehicleTypes(laneInfo, busLaneMode);
				cachedDefaultTypes[laneIndex] = defaultVehicleType;
			}
			return (ExtVehicleType)defaultVehicleType;
		}

		public ExtVehicleType GetDefaultAllowedVehicleTypes(NetInfo.Lane laneInfo, RestrictionMode busLaneMode) {
			ExtVehicleType ret = ExtVehicleType.None;
			if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None)
				ret |= ExtVehicleType.Bicycle;
			if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Tram) != VehicleInfo.VehicleType.None)
				ret |= ExtVehicleType.Tram;
			if (busLaneMode == RestrictionMode.Restricted ||
					(busLaneMode == RestrictionMode.Configured && Options.banRegularTrafficOnBusLanes)) {
				if ((laneInfo.m_laneType & NetInfo.LaneType.TransportVehicle) != NetInfo.LaneType.None)
					ret |= ExtVehicleType.RoadPublicTransport | ExtVehicleType.Service | ExtVehicleType.Emergency;
				else if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
					ret |= ExtVehicleType.RoadVehicle;
			} else {
				if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
					ret |= ExtVehicleType.RoadVehicle;
			}
			if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None)
				ret |= ExtVehicleType.RailVehicle;
			if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Ship) != VehicleInfo.VehicleType.None)
				ret |= ExtVehicleType.Ship;
			if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Plane) != VehicleInfo.VehicleType.None)
				ret |= ExtVehicleType.Plane;
			if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Ferry) != VehicleInfo.VehicleType.None)
				ret |= ExtVehicleType.Ferry;
			if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Blimp) != VehicleInfo.VehicleType.None)
				ret |= ExtVehicleType.Blimp;
			if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.CableCar) != VehicleInfo.VehicleType.None)
				ret |= ExtVehicleType.CableCar;
			return ret;
		}

		/// <summary>
		/// Determines the default set of allowed vehicle types for a given lane.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="segmentInfo"></param>
		/// <param name="laneIndex"></param>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		internal ExtVehicleType GetDefaultAllowedVehicleTypes(uint laneId, RestrictionMode busLaneMode) {
			if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None)
				return ExtVehicleType.None;
			ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return ExtVehicleType.None;

			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;
			uint laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				if (curLaneId == laneId) {
					return GetDefaultAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, busLaneMode);
				}
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}

			return ExtVehicleType.None;
		}

		/// <summary>
		/// Sets the allowed vehicle types for the given segment and lane.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="laneIndex"></param>
		/// <param name="laneId"></param>
		/// <param name="allowedTypes"></param>
		/// <returns></returns>
		internal bool SetAllowedVehicleTypes(ushort segmentId, NetInfo segmentInfo, uint laneIndex, NetInfo.Lane laneInfo, uint laneId, ExtVehicleType allowedTypes) {
			if (! Services.NetService.IsLaneValid(laneId)) {
				return false;
			}

			if (! Services.NetService.IsSegmentValid(segmentId)) {
				// TODO we do not need the segmentId given here. Lane is enough
				return false;
			}

			allowedTypes &= GetBaseMask(segmentInfo.m_lanes[laneIndex], RestrictionMode.Configured); // ensure default base mask
			Flags.setLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, allowedTypes);
			SubscribeToSegmentGeometry(segmentId);
			NotifyStartEndNode(segmentId);

			if (Options.instantEffects) {
				Services.NetService.PublishSegmentChanges(segmentId);
			}

			return true;
		}

		/// <summary>
		/// Adds the given vehicle type to the set of allowed vehicles at the specified lane
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="laneIndex"></param>
		/// <param name="laneId"></param>
		/// <param name="laneInfo"></param>
		/// <param name="road"></param>
		/// <param name="vehicleType"></param>
		public void AddAllowedType(ushort segmentId, NetInfo segmentInfo, uint laneIndex, uint laneId, NetInfo.Lane laneInfo, ExtVehicleType vehicleType) {
			if (!Services.NetService.IsLaneValid(laneId)) {
				return;
			}

			if (!Services.NetService.IsSegmentValid(segmentId)) {
				// TODO we do not need the segmentId given here. Lane is enough
				return;
			}

			ExtVehicleType allowedTypes = GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, RestrictionMode.Configured);
			allowedTypes |= vehicleType;
			allowedTypes &= GetBaseMask(segmentInfo.m_lanes[laneIndex], RestrictionMode.Configured); // ensure default base mask
			Flags.setLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, allowedTypes);
			SubscribeToSegmentGeometry(segmentId);
			NotifyStartEndNode(segmentId);

			if (Options.instantEffects) {
				Services.NetService.PublishSegmentChanges(segmentId);
			}
		}

		/// <summary>
		/// Removes the given vehicle type from the set of allowed vehicles at the specified lane
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="laneIndex"></param>
		/// <param name="laneId"></param>
		/// <param name="laneInfo"></param>
		/// <param name="road"></param>
		/// <param name="vehicleType"></param>
		public void RemoveAllowedType(ushort segmentId, NetInfo segmentInfo, uint laneIndex, uint laneId, NetInfo.Lane laneInfo, ExtVehicleType vehicleType) {
			if (!Services.NetService.IsLaneValid(laneId)) {
				return;
			}

			if (!Services.NetService.IsSegmentValid(segmentId)) {
				// TODO we do not need the segmentId given here. Lane is enough
				return;
			}

			ExtVehicleType allowedTypes = GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo, RestrictionMode.Configured);
			allowedTypes &= ~vehicleType;
			allowedTypes &= GetBaseMask(segmentInfo.m_lanes[laneIndex], RestrictionMode.Configured); // ensure default base mask
			Flags.setLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, allowedTypes);
			SubscribeToSegmentGeometry(segmentId);
			NotifyStartEndNode(segmentId);

			if (Options.instantEffects) {
				Services.NetService.PublishSegmentChanges(segmentId);
			}
		}

		public void ToggleAllowedType(ushort segmentId, NetInfo segmentInfo, uint laneIndex, uint laneId, NetInfo.Lane laneInfo, ExtVehicleType vehicleType, bool add) {
			if (add)
				AddAllowedType(segmentId, segmentInfo, laneIndex, laneId, laneInfo, vehicleType);
			else
				RemoveAllowedType(segmentId, segmentInfo, laneIndex, laneId, laneInfo, vehicleType);
		}

		/// <summary>
		/// Determines the maximum allowed set of vehicles (the base mask) for a given lane
		/// </summary>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		public ExtVehicleType GetBaseMask(NetInfo.Lane laneInfo, RestrictionMode includeBusLanes) {
			return GetDefaultAllowedVehicleTypes(laneInfo, includeBusLanes);
		}

		/// <summary>
		/// Determines the maximum allowed set of vehicles (the base mask) for a given lane
		/// </summary>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		public ExtVehicleType GetBaseMask(uint laneId, RestrictionMode includeBusLanes) {
			if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None)
				return ExtVehicleType.None;
			ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return ExtVehicleType.None;

			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;
			uint laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				if (curLaneId == laneId) {
					return GetBaseMask(laneInfo, includeBusLanes);
				}
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}
			return ExtVehicleType.None;
		}

		public bool IsAllowed(ExtVehicleType? allowedTypes, ExtVehicleType vehicleType) {
			return allowedTypes == null || ((ExtVehicleType)allowedTypes & vehicleType) != ExtVehicleType.None;
		}

		public bool IsBicycleAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.Bicycle);
		}

		public bool IsBusAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.Bus);
		}

		public bool IsCargoTrainAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.CargoTrain);
		}

		public bool IsCargoTruckAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.CargoTruck);
		}

		public bool IsEmergencyAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.Emergency);
		}

		public bool IsPassengerCarAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.PassengerCar);
		}

		public bool IsPassengerTrainAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.PassengerTrain);
		}

		public bool IsServiceAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.Service);
		}

		public bool IsTaxiAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.Taxi);
		}

		public bool IsTramAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.Tram);
		}

		public bool IsBlimpAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.Blimp);
		}

		public bool IsCableCarAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.CableCar);
		}

		public bool IsFerryAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.Ferry);
		}

		public bool IsRailVehicleAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.RailVehicle);
		}

		public bool IsRoadVehicleAllowed(ExtVehicleType? allowedTypes) {
			return IsAllowed(allowedTypes, ExtVehicleType.RoadVehicle);
		}

		public bool IsRailLane(NetInfo.Lane laneInfo) {
			return (laneInfo.m_vehicleType & VehicleInfo.VehicleType.Train) != VehicleInfo.VehicleType.None;
		}

		public bool IsRoadLane(NetInfo.Lane laneInfo) {
			return (laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None;
		}

		public bool IsTramLane(NetInfo.Lane laneInfo) {
			return (laneInfo.m_vehicleType & VehicleInfo.VehicleType.Tram) != VehicleInfo.VehicleType.None;
		}

		public bool IsRailSegment(NetInfo segmentInfo) {
			ItemClass connectionClass = segmentInfo.GetConnectionClass();
			return connectionClass.m_service == ItemClass.Service.PublicTransport && connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain;
		}

		public bool IsRoadSegment(NetInfo segmentInfo) {
			ItemClass connectionClass = segmentInfo.GetConnectionClass();
			return connectionClass.m_service == ItemClass.Service.Road;
		}

		public bool IsMonorailSegment(NetInfo segmentInfo) {
			ItemClass connectionClass = segmentInfo.GetConnectionClass();
			return connectionClass.m_service == ItemClass.Service.PublicTransport && connectionClass.m_subService == ItemClass.SubService.PublicTransportMonorail;
		}

		internal void ClearCache(ushort segmentId) {
			if (defaultVehicleTypeCache != null) {
				defaultVehicleTypeCache[segmentId] = null;
			}
		}

		internal void ClearCache() {
			defaultVehicleTypeCache = null;
		}

		public void NotifyStartEndNode(ushort segmentId) {
			// notify observers of start node and end node (e.g. for separate traffic lights)
			ushort startNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_startNode;
			ushort endNodeId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_endNode;
			if (startNodeId != 0)
				NodeGeometry.Get(startNodeId).NotifyObservers();
			if (endNodeId != 0)
				NodeGeometry.Get(endNodeId).NotifyObservers();
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			Flags.resetSegmentVehicleRestrictions(geometry.SegmentId);
			ClearCache(geometry.SegmentId);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			ClearCache();
		}

		public bool LoadData(List<Configuration.LaneVehicleTypes> data) {
			bool success = true;
			Log.Info($"Loading lane vehicle restriction data. {data.Count} elements");
			foreach (Configuration.LaneVehicleTypes laneVehicleTypes in data) {
				try {
					if (!Services.NetService.IsLaneValid(laneVehicleTypes.laneId))
						continue;

					ExtVehicleType baseMask = GetBaseMask(laneVehicleTypes.laneId, RestrictionMode.Configured);
					ExtVehicleType maskedType = laneVehicleTypes.vehicleTypes & baseMask;
					Log._Debug($"Loading lane vehicle restriction: lane {laneVehicleTypes.laneId} = {laneVehicleTypes.vehicleTypes}, masked = {maskedType}");
					if (maskedType != baseMask) {
						Flags.setLaneAllowedVehicleTypes(laneVehicleTypes.laneId, maskedType);
					} else {
						Log._Debug($"Masked type does not differ from base type. Ignoring.");
					}
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading data from vehicle restrictions: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		public List<Configuration.LaneVehicleTypes> SaveData(ref bool success) {
			List<Configuration.LaneVehicleTypes> ret = new List<Configuration.LaneVehicleTypes>();
			foreach (KeyValuePair<uint, ExtVehicleType> e in Flags.getAllLaneAllowedVehicleTypes()) {
				try {
					ret.Add(new Configuration.LaneVehicleTypes(e.Key, e.Value));
				} catch (Exception ex) {
					Log.Error($"Exception occurred while saving lane vehicle restrictions @ {e.Key}: {ex.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}
