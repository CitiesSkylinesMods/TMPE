using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	public class VehicleRestrictionsManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<Configuration.LaneVehicleTypes>> {
		public static VehicleRestrictionsManager Instance { get; private set; } = null;

		static VehicleRestrictionsManager() {
			Instance = new VehicleRestrictionsManager();
		}

		/// <summary>
		/// For each segment id and lane index: Holds the default set of vehicle types allowed for the lane
		/// </summary>
		private ExtVehicleType?[][] defaultVehicleTypeCache = null;

		/// <summary>
		/// Determines the allowed vehicle types that may approach the given node from the given segment.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="nodeId"></param>
		/// <returns></returns>
		internal ExtVehicleType GetAllowedVehicleTypes(ushort segmentId, ushort nodeId) { // TODO optimize method (don't depend on collections!)
			ExtVehicleType ret = ExtVehicleType.None;
			foreach (ExtVehicleType vehicleType in GetAllowedVehicleTypesAsSet(segmentId, nodeId)) {
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
		internal HashSet<ExtVehicleType> GetAllowedVehicleTypesAsSet(ushort segmentId, ushort nodeId) {
			HashSet<ExtVehicleType> ret = new HashSet<ExtVehicleType>(GetAllowedVehicleTypesAsDict(segmentId, nodeId).Values);
			return ret;
		}

		/// <summary>
		/// Determines the allowed vehicle types that may approach the given node from the given segment (lane-wise).
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="nodeId"></param>
		/// <returns></returns>
		internal Dictionary<byte, ExtVehicleType> GetAllowedVehicleTypesAsDict(ushort segmentId, ushort nodeId) {
			Dictionary<byte, ExtVehicleType> ret = new Dictionary<byte, ExtVehicleType>();

			NetManager netManager = Singleton<NetManager>.instance;
			if (segmentId == 0 || (netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None ||
				nodeId == 0 || (netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				return ret;
			}

			var dir = NetInfo.Direction.Forward;
			var dir2 = ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = TrafficPriorityManager.IsLeftHandDrive() ? NetInfo.InvertDirection(dir2) : dir2;

			NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;
			uint laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				ushort toNodeId = (laneInfo.m_direction == dir3) ? netManager.m_segments.m_buffer[segmentId].m_endNode : netManager.m_segments.m_buffer[segmentId].m_startNode;

				if (toNodeId == nodeId) {
					ExtVehicleType vehicleTypes = GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo);
					ret[(byte)laneIndex] = vehicleTypes;
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
		/// <param name="segmetnInfo"></param>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		internal ExtVehicleType GetAllowedVehicleTypes(ushort segmentId, NetInfo segmentInfo, uint laneIndex, NetInfo.Lane laneInfo) {
			ExtVehicleType?[] fastArray = Flags.laneAllowedVehicleTypesArray[segmentId];
			if (fastArray != null && fastArray.Length > laneIndex && fastArray[laneIndex] != null) {
				return (ExtVehicleType)fastArray[laneIndex];
			}

			return GetDefaultAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo);
		}

		internal bool HasSegmentRestrictions(ushort segmentId) { // TODO clean up restrictions (currently we do not check if restrictions are equal with the base type)
			ExtVehicleType?[] fastArray = Flags.laneAllowedVehicleTypesArray[segmentId];
			return fastArray != null;
		}

		/// <summary>
		/// Determines the default set of allowed vehicle types for a given segment and lane.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="segmentInfo"></param>
		/// <param name="laneIndex"></param>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		public ExtVehicleType GetDefaultAllowedVehicleTypes(ushort segmentId, NetInfo segmentInfo, uint laneIndex, NetInfo.Lane laneInfo) {
			// manage cached default vehicle types
			if (defaultVehicleTypeCache == null) {
				defaultVehicleTypeCache = new ExtVehicleType?[NetManager.MAX_SEGMENT_COUNT][];
			}

			ExtVehicleType?[] cachedDefaultTypes = defaultVehicleTypeCache[segmentId];
			if (cachedDefaultTypes == null || cachedDefaultTypes.Length != segmentInfo.m_lanes.Length) {
				defaultVehicleTypeCache[segmentId] = cachedDefaultTypes = new ExtVehicleType?[segmentInfo.m_lanes.Length];
			}

			ExtVehicleType? defaultVehicleType = cachedDefaultTypes[laneIndex];
			if (defaultVehicleType == null) {
				ExtVehicleType ret = ExtVehicleType.None;
				if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None)
					ret |= ExtVehicleType.Bicycle;
				if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Tram) != VehicleInfo.VehicleType.None)
					ret |= ExtVehicleType.Tram;
				if ((laneInfo.m_laneType & NetInfo.LaneType.TransportVehicle) != NetInfo.LaneType.None)
					ret |= ExtVehicleType.RoadPublicTransport | ExtVehicleType.Service | ExtVehicleType.Emergency;
				else if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None)
					ret |= ExtVehicleType.RoadVehicle;
				if ((laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro)) != VehicleInfo.VehicleType.None)
					ret |= ExtVehicleType.RailVehicle;
				if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Ship) != VehicleInfo.VehicleType.None)
					ret |= ExtVehicleType.Ship;
				if ((laneInfo.m_vehicleType & VehicleInfo.VehicleType.Plane) != VehicleInfo.VehicleType.None)
					ret |= ExtVehicleType.Plane;
				cachedDefaultTypes[laneIndex] = ret;
				return ret;
			} else {
				return (ExtVehicleType)defaultVehicleType;
			}
		}

		/// <summary>
		/// Determines the default set of allowed vehicle types for a given lane.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="segmentInfo"></param>
		/// <param name="laneIndex"></param>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		internal ExtVehicleType GetDefaultAllowedVehicleTypes(uint laneId) {
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
					return GetDefaultAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo);
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
			if (segmentId == 0 || (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None || ((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None) {
				return false;
			}

			allowedTypes &= GetBaseMask(segmentInfo.m_lanes[laneIndex]); // ensure default base mask
			Flags.setLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, allowedTypes);
			SubscribeToSegmentGeometry(segmentId);
			NotifyStartEndNode(segmentId);

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
			if (segmentId == 0 || (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None || ((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None) {
				return;
			}

			ExtVehicleType allowedTypes = GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo);
			allowedTypes |= vehicleType;
			allowedTypes &= GetBaseMask(segmentInfo.m_lanes[laneIndex]); // ensure default base mask
			Flags.setLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, allowedTypes);
			SubscribeToSegmentGeometry(segmentId);
			NotifyStartEndNode(segmentId);
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
			if (segmentId == 0 || (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None || ((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None) {
				return;
			}

			ExtVehicleType allowedTypes = GetAllowedVehicleTypes(segmentId, segmentInfo, laneIndex, laneInfo);
			allowedTypes &= ~vehicleType;
			allowedTypes &= GetBaseMask(segmentInfo.m_lanes[laneIndex]); // ensure default base mask
			Flags.setLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, allowedTypes);
			SubscribeToSegmentGeometry(segmentId);
			NotifyStartEndNode(segmentId);
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
		public ExtVehicleType GetBaseMask(NetInfo.Lane laneInfo) {
			if (IsRoadLane(laneInfo))
				return ExtVehicleType.RoadVehicle;
			else if (IsRailLane(laneInfo))
				return ExtVehicleType.RailVehicle;
			else
				return ExtVehicleType.None;
		}

		/// <summary>
		/// Determines the maximum allowed set of vehicles (the base mask) for a given lane
		/// </summary>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		public ExtVehicleType GetBaseMask(uint laneId) {
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
					return GetBaseMask(laneInfo);
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

		public bool IsRailSegment(NetInfo segmentInfo) {
			ItemClass connectionClass = segmentInfo.GetConnectionClass();
			return connectionClass.m_service == ItemClass.Service.PublicTransport && connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain;
		}

		public bool IsRoadSegment(NetInfo segmentInfo) {
			ItemClass connectionClass = segmentInfo.GetConnectionClass();
			return connectionClass.m_service == ItemClass.Service.Road;
		}

		internal void ClearCache(ushort segmentId) {
			if (defaultVehicleTypeCache != null) {
				defaultVehicleTypeCache[segmentId] = null;
			}
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
			defaultVehicleTypeCache = null;
		}

		public bool LoadData(List<Configuration.LaneVehicleTypes> data) {
			bool success = true;
			Log.Info($"Loading lane vehicle restriction data. {data.Count} elements");
			foreach (Configuration.LaneVehicleTypes laneVehicleTypes in data) {
				try {
					if (!NetUtil.IsLaneValid(laneVehicleTypes.laneId))
						continue;

					ExtVehicleType baseMask = GetBaseMask(laneVehicleTypes.laneId);
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
