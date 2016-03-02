using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TrafficManager.Traffic;

namespace TrafficManager.State {
	public class Flags {
		[Flags]
		public enum LaneArrows { // compatible with NetLane.Flags
			None = 0,
			Forward = 16,
			Left = 32,
			Right = 64,
			LeftForward = 48,
			LeftRight = 96,
			ForwardRight = 80,
			LeftForwardRight = 112
		}

		public static readonly uint lfr = (uint)NetLane.Flags.LeftForwardRight;

		/// <summary>
		/// For each node: Defines if a traffic light exists or not. If no entry exists for a given node id, the game's default setting is used
		/// </summary>
		private static bool?[] nodeTrafficLightFlag = null;

		/// <summary>
		/// For each lane: Defines the lane arrows which are set
		/// </summary>
		private static LaneArrows?[] laneArrowFlags = null;

		/// <summary>
		/// For each lane: Defines the currently set speed limit
		/// </summary>
		private static Dictionary<uint, ushort> laneSpeedLimit = new Dictionary<uint, ushort>();

		internal static ushort?[][] laneSpeedLimitArray; // for faster, lock-free access, 1st index: segment id, 2nd index: lane index

		/// <summary>
		/// For each lane: Defines the lane arrows which are set in highway rule mode (they are not saved)
		/// </summary>
		private static LaneArrows?[] highwayLaneArrowFlags = null;

		/// <summary>
		/// For each lane: Defines the allowed vehicle types
		/// </summary>
		private static Dictionary<uint, ExtVehicleType> laneAllowedVehicleTypes = new Dictionary<uint, ExtVehicleType>();

		internal static ExtVehicleType?[][] laneAllowedVehicleTypesArray; // for faster, lock-free access, 1st index: segment id, 2nd index: lane index

		/// <summary>
		/// For each segment and node: Defines additional flags for segments at a node
		/// </summary>
		private static Configuration.SegmentNodeFlags[][] segmentNodeFlags = null;

		private static object laneSpeedLimitLock = new object();
		private static object laneAllowedVehicleTypesLock = new object();

		private static bool initDone = false;

		public static bool IsInitDone() {
			return initDone;
		}

		public static void resetTrafficLights(bool all) {
			for (ushort i = 0; i < Singleton<NetManager>.instance.m_nodes.m_size; ++i) {
				nodeTrafficLightFlag[i] = null;
				if (! all && TrafficPriority.IsPriorityNode(i))
					continue;
				Singleton<NetManager>.instance.UpdateNodeFlags(i);
			}
		}

		public static bool mayHaveTrafficLight(ushort nodeId) {
			if (nodeId <= 0)
				return false;

			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				//Log.Message($"Flags: Node {nodeId} may not have a traffic light (not created)");
				Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags = NetNode.Flags.None;
				return false;
			}

			ItemClass connectionClass = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].Info.GetConnectionClass();
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Junction) == NetNode.Flags.None &&
				connectionClass.m_service != ItemClass.Service.PublicTransport
				) {
				Log._Debug($"Flags: Node {nodeId} may not have a traffic light");
				return false;
			}

			if (connectionClass == null ||
				(connectionClass.m_service != ItemClass.Service.Road &&
				connectionClass.m_service != ItemClass.Service.PublicTransport))
				return false;

			return true;
		}

		public static void setNodeTrafficLight(ushort nodeId, bool flag) {
			if (nodeId <= 0)
				return;

			Log._Debug($"Flags: Set node traffic light: {nodeId}={flag}");

			if (!mayHaveTrafficLight(nodeId)) {
				Log.Warning($"Flags: Refusing to add/delete traffic light to/from node: {nodeId} {flag}");
				return;
			}

			nodeTrafficLightFlag[nodeId] = flag;
			applyNodeTrafficLightFlag(nodeId);
		}

		internal static bool? isNodeTrafficLight(ushort nodeId) {
			if (nodeId <= 0)
				return false;

			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
				return false;

			return nodeTrafficLightFlag[nodeId];
		}

		public static void setLaneSpeedLimit(uint laneId, ushort speedLimit) {
			if (laneId <= 0)
				return;
			if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None)
				return;

			ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
			if (segmentId <= 0)
				return;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return;

			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			uint laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				if (curLaneId == laneId) {
					setLaneSpeedLimit(segmentId, laneIndex, laneId, speedLimit);
					return;
				}
				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}
		}

		public static void setLaneSpeedLimit(ushort segmentId, uint laneIndex, ushort speedLimit) {
			if (segmentId <= 0 || laneIndex < 0)
				return;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return;
			}
			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			if (laneIndex >= segmentInfo.m_lanes.Length) {
				return;
			}

			// find the lane id
			uint laneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			for (int i = 0; i < laneIndex; ++i) {
				if (laneId == 0)
					return; // no valid lane found
				laneId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_nextLane;
			}

			setLaneSpeedLimit(segmentId, laneIndex, laneId, speedLimit);
		}

		public static void setLaneSpeedLimit(ushort segmentId, uint laneIndex, uint laneId, ushort speedLimit) {
			if (segmentId <= 0 || laneIndex < 0 || laneId <= 0)
				return;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return;
			}
			if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None)
				return;
			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			if (laneIndex >= segmentInfo.m_lanes.Length) {
				return;
			}

			try {
				Monitor.Enter(laneSpeedLimitLock);
				Log._Debug($"Flags.setLaneSpeedLimit: setting speed limit of lane index {laneIndex} @ seg. {segmentId} to {speedLimit}");

				laneSpeedLimit[laneId] = speedLimit;

				// save speed limit into the fast-access array.
				// (1) ensure that the array is defined and large enough
				if (laneSpeedLimitArray[segmentId] == null) {
					laneSpeedLimitArray[segmentId] = new ushort?[segmentInfo.m_lanes.Length];
				} else if (laneSpeedLimitArray[segmentId].Length < segmentInfo.m_lanes.Length) {
					var oldArray = laneSpeedLimitArray[segmentId];
					laneSpeedLimitArray[segmentId] = new ushort?[segmentInfo.m_lanes.Length];
					Array.Copy(oldArray, laneSpeedLimitArray[segmentId], oldArray.Length);
				}
				// (2) insert the custom speed limit
				laneSpeedLimitArray[segmentId][laneIndex] = speedLimit;
			} finally {
				Monitor.Exit(laneSpeedLimitLock);
			}
		}

		public static void setLaneAllowedVehicleTypes(uint laneId, ExtVehicleType vehicleTypes) {
			if (laneId <= 0)
				return;
			if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None)
				return;

			ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
			if (segmentId <= 0)
				return;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return;

			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			uint laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				if (curLaneId == laneId) {
					setLaneAllowedVehicleTypes(segmentId, laneIndex, laneId, vehicleTypes);
					return;
				}
				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}
		}

		public static void setLaneAllowedVehicleTypes(ushort segmentId, uint laneIndex, uint laneId, ExtVehicleType vehicleTypes) {
			if (segmentId <= 0 || laneIndex < 0 || laneId <= 0)
				return;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return;
			}
			if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None)
				return;
			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			if (laneIndex >= segmentInfo.m_lanes.Length) {
				return;
			}

			try {
				Monitor.Enter(laneAllowedVehicleTypesLock);
				Log._Debug($"Flags.setLaneAllowedVehicleTypes: setting allowed vehicles of lane index {laneIndex} @ seg. {segmentId} to {vehicleTypes.ToString()}");

				laneAllowedVehicleTypes[laneId] = vehicleTypes;

				// save allowed vehicle types into the fast-access array.
				// (1) ensure that the array is defined and large enough
				if (laneAllowedVehicleTypesArray[segmentId] == null) {
					laneAllowedVehicleTypesArray[segmentId] = new ExtVehicleType?[segmentInfo.m_lanes.Length];
				} else if (laneAllowedVehicleTypesArray[segmentId].Length < segmentInfo.m_lanes.Length) {
					var oldArray = laneAllowedVehicleTypesArray[segmentId];
					laneAllowedVehicleTypesArray[segmentId] = new ExtVehicleType?[segmentInfo.m_lanes.Length];
					Array.Copy(oldArray, laneAllowedVehicleTypesArray[segmentId], oldArray.Length);
				}
				// (2) insert the custom speed limit
				laneAllowedVehicleTypesArray[segmentId][laneIndex] = vehicleTypes;
			} finally {
				Monitor.Exit(laneAllowedVehicleTypesLock);
			}
		}

		public static void setLaneArrowFlags(uint laneId, LaneArrows flags) {
			if (!mayHaveLaneArrows(laneId)) {
				removeLaneArrowFlags(laneId);
				return;
			}

			if (highwayLaneArrowFlags[laneId] != null)
				return; // disallow custom lane arrows in highway rule mode

			laneArrowFlags[laneId] = flags;
			applyLaneArrowFlags(laneId, false);
		}

		public static void setHighwayLaneArrowFlags(uint laneId, LaneArrows flags, bool check=true) {
			if (check && !mayHaveLaneArrows(laneId)) {
				removeLaneArrowFlags(laneId);
				return;
			}
			
			highwayLaneArrowFlags[laneId] = flags;
			applyLaneArrowFlags(laneId, false);
		}

		public static bool toggleLaneArrowFlags(uint laneId, LaneArrows flags) {
			if (!mayHaveLaneArrows(laneId)) {
				removeLaneArrowFlags(laneId);
				return false;
			}

			if (highwayLaneArrowFlags[laneId] != null)
				return false; // disallow custom lane arrows in highway rule mode

			LaneArrows? arrows = laneArrowFlags[laneId];
			if (arrows == null) {
				// read currently defined arrows
				uint laneFlags = (uint)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;
				laneFlags &= lfr; // filter arrows
				arrows = (LaneArrows)laneFlags;
			}

			arrows ^= flags;
			laneArrowFlags[laneId] = arrows;
			applyLaneArrowFlags(laneId, false);
			return true;
		}

		private static bool mayHaveLaneArrows(uint laneId) {
			if (laneId <= 0)
				return false;
			NetManager netManager = Singleton<NetManager>.instance;
			if (((NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags & NetLane.Flags.Created) == NetLane.Flags.None)
				return false;

			ushort segmentId = netManager.m_lanes.m_buffer[laneId].m_segment;

			var dir = NetInfo.Direction.Forward;
			var dir2 = ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = TrafficPriority.IsLeftHandDrive() ? NetInfo.InvertDirection(dir2) : dir2;

			NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;
			int laneIndex = 0;
			int wIter = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				++wIter;
				if (wIter >= 20) {
					Log.Error("Too many iterations in Flags.mayHaveLaneArrows!");
					break;
				}

				if (curLaneId == laneId) {
					NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
					ushort nodeId = (laneInfo.m_direction == dir3) ? netManager.m_segments.m_buffer[segmentId].m_endNode : netManager.m_segments.m_buffer[segmentId].m_startNode;

					if ((netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
						return false;
					return (netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
				}
				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}
			return false;
		}

		public static ushort? getLaneSpeedLimit(uint laneId) {
			try {
				Monitor.Enter(laneSpeedLimitLock);

				if (laneId <= 0 || !laneSpeedLimit.ContainsKey(laneId))
					return null;

				return laneSpeedLimit[laneId];
			} finally {
				Monitor.Exit(laneSpeedLimitLock);
			}
		}

		internal static Dictionary<uint, ushort> getAllLaneSpeedLimits() {
			Dictionary<uint, ushort> ret = new Dictionary<uint, ushort>();
			try {
				Monitor.Enter(laneSpeedLimitLock);

				ret = new Dictionary<uint, ushort>(laneSpeedLimit);

			} finally {
				Monitor.Exit(laneSpeedLimitLock);
			}
			return ret;
		}

		internal static Dictionary<uint, ExtVehicleType> getAllLaneAllowedVehicleTypes() {
			Dictionary<uint, ExtVehicleType> ret = new Dictionary<uint, ExtVehicleType>();
			try {
				Monitor.Enter(laneAllowedVehicleTypesLock);

				ret = new Dictionary<uint, ExtVehicleType>(laneAllowedVehicleTypes);

			} finally {
				Monitor.Exit(laneAllowedVehicleTypesLock);
			}
			return ret;
		}

		public static LaneArrows? getLaneArrowFlags(uint laneId) {
			return laneArrowFlags[laneId];
		}

		public static LaneArrows? getHighwayLaneArrowFlags(uint laneId) {
			return highwayLaneArrowFlags[laneId];
		}

		public static bool getUTurnAllowed(ushort segmentId, bool startNode) {
			if (!IsInitDone())
				return false;

			int index = startNode ? 0 : 1;

			Configuration.SegmentNodeFlags[] nodeFlags = segmentNodeFlags[segmentId];
			if (nodeFlags == null || nodeFlags[index] == null || nodeFlags[index].uturnAllowed == null)
				return Options.allowUTurns;
			return (bool)nodeFlags[index].uturnAllowed;
		}

		public static void setUTurnAllowed(ushort segmentId, bool startNode, bool value) {
			bool? valueToSet = value;
			if (value == Options.allowUTurns)
				valueToSet = null;

			int index = startNode ? 0 : 1;
			if (segmentNodeFlags[segmentId][index] == null) {
				if (valueToSet == null)
					return;

				segmentNodeFlags[segmentId][index] = new Configuration.SegmentNodeFlags();
			}
			segmentNodeFlags[segmentId][index].uturnAllowed = valueToSet;
		}

		public static bool getStraightLaneChangingAllowed(ushort segmentId, bool startNode) {
			if (!IsInitDone())
				return false;

			int index = startNode ? 0 : 1;

			Configuration.SegmentNodeFlags[] nodeFlags = segmentNodeFlags[segmentId];
			if (nodeFlags == null || nodeFlags[index] == null || nodeFlags[index].straightLaneChangingAllowed == null)
				return Options.allowLaneChangesWhileGoingStraight;
			return (bool)nodeFlags[index].straightLaneChangingAllowed;
		}

		public static void setStraightLaneChangingAllowed(ushort segmentId, bool startNode, bool value) {
			bool? valueToSet = value;
			if (value == Options.allowLaneChangesWhileGoingStraight)
				valueToSet = null;

			int index = startNode ? 0 : 1;
			if (segmentNodeFlags[segmentId][index] == null) {
				if (valueToSet == null)
					return;
				segmentNodeFlags[segmentId][index] = new Configuration.SegmentNodeFlags();
			}
			segmentNodeFlags[segmentId][index].straightLaneChangingAllowed = valueToSet;
		}

		public static bool getEnterWhenBlockedAllowed(ushort segmentId, bool startNode) {
			if (!IsInitDone())
				return false;

			int index = startNode ? 0 : 1;

			Configuration.SegmentNodeFlags[] nodeFlags = segmentNodeFlags[segmentId];
			if (nodeFlags == null || nodeFlags[index] == null || nodeFlags[index].enterWhenBlockedAllowed == null)
				return Options.allowEnterBlockedJunctions;
			return (bool)nodeFlags[index].enterWhenBlockedAllowed;
		}

		public static void setEnterWhenBlockedAllowed(ushort segmentId, bool startNode, bool value) {
			bool? valueToSet = value;
			if (value == Options.allowEnterBlockedJunctions)
				valueToSet = null;

			int index = startNode ? 0 : 1;
			if (segmentNodeFlags[segmentId][index] == null) {
				if (valueToSet == null)
					return;
				segmentNodeFlags[segmentId][index] = new Configuration.SegmentNodeFlags();
			}
			segmentNodeFlags[segmentId][index].enterWhenBlockedAllowed = valueToSet;
		}

		internal static void setSegmentNodeFlags(ushort segmentId, bool startNode, Configuration.SegmentNodeFlags flags) {
			if (flags == null)
				return;

			int index = startNode ? 0 : 1;
			segmentNodeFlags[segmentId][index] = flags;
		}

		internal static Configuration.SegmentNodeFlags getSegmentNodeFlags(ushort segmentId, bool startNode) {
			int index = startNode ? 0 : 1;
			return segmentNodeFlags[segmentId][index];
		}

		public static void removeHighwayLaneArrowFlags(uint laneId) {
			highwayLaneArrowFlags[laneId] = null;
		}

		public static void applyAllFlags() {
			for (ushort i = 0; i < nodeTrafficLightFlag.Length; ++i) {
				applyNodeTrafficLightFlag(i);
			}

			for (uint i = 0; i < laneArrowFlags.Length; ++i) {
				if (!applyLaneArrowFlags(i))
					laneArrowFlags[i] = null;
			}
		}

		public static void applyNodeTrafficLightFlag(ushort nodeId) {
			bool? flag = nodeTrafficLightFlag[nodeId];
			if (nodeId <= 0 || flag == null)
				return;

			bool mayHaveLight = mayHaveTrafficLight(nodeId);
			if ((bool)flag && mayHaveLight) {
				//Log.Message($"Adding traffic light @ node {nodeId}");
				Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags |= NetNode.Flags.TrafficLights;
			} else {
				//Log.Message($"Removing traffic light @ node {nodeId}");
				Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags &= ~NetNode.Flags.TrafficLights;
				if (!mayHaveLight) {
					Log.Warning($"Flags: Refusing to apply traffic light flag at node {nodeId}");
					nodeTrafficLightFlag[nodeId] = null;
				}
			}
		}

		public static bool applyLaneArrowFlags(uint laneId, bool check=true) {
			if (laneId <= 0)
				return true;

			uint laneFlags = (uint)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;

			if (check && !mayHaveLaneArrows(laneId))
				return false;

			LaneArrows? hwArrows = highwayLaneArrowFlags[laneId];
			LaneArrows? arrows = laneArrowFlags[laneId];

			if (hwArrows != null) {
				laneFlags &= ~lfr; // remove all arrows
				laneFlags |= (uint)hwArrows; // add highway arrows
			} else if (arrows != null) {
				LaneArrows flags = (LaneArrows)arrows;
				laneFlags &= ~lfr; // remove all arrows
				laneFlags |= (uint)flags; // add desired arrows
			}
			
			//Log._Debug($"Setting lane flags @ lane {laneId}, seg. {Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment} to {((NetLane.Flags)laneFlags).ToString()}");
			Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = Convert.ToUInt16(laneFlags);
			return true;
		}

		public static void removeLaneArrowFlags(uint laneId) {
			if (laneId <= 0)
				return;

			if (highwayLaneArrowFlags[laneId] != null)
				return; // modification of arrows in highway rule mode is forbidden

			laneArrowFlags[laneId] = null;
			uint laneFlags = (uint)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;

			if (((NetLane.Flags)laneFlags & NetLane.Flags.Created) == NetLane.Flags.None) {
				Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = 0;
			} else {
				Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &= (ushort)~lfr;
			}
		}

		internal static void removeHighwayLaneArrowFlagsAtSegment(ushort segmentId) {
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return;

			int i = 0;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;

			while (i < Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes.Length && curLaneId != 0u) {
				Flags.removeHighwayLaneArrowFlags(curLaneId);
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				++i;
			} // foreach lane
		}

		public static void clearHighwayLaneArrows() {
			for (uint i = 0; i < Singleton<NetManager>.instance.m_lanes.m_size; ++i) {
				highwayLaneArrowFlags[i] = null;
			}
		}

		internal static void OnLevelUnloading() {
			initDone = false;

			nodeTrafficLightFlag = null;

			try {
				Monitor.Enter(laneSpeedLimitLock);
				laneSpeedLimitArray = null;
				laneSpeedLimit.Clear();
			} finally {
				Monitor.Exit(laneSpeedLimitLock);
			}

			try {
				Monitor.Enter(laneAllowedVehicleTypesLock);
				laneAllowedVehicleTypesArray = null;
				laneAllowedVehicleTypes.Clear();
			} finally {
				Monitor.Exit(laneAllowedVehicleTypesLock);
			}

			laneArrowFlags = null;
			highwayLaneArrowFlags = null;
			segmentNodeFlags = null;
		}

		public static void OnBeforeLoadData() {
			if (initDone)
				return;

			laneSpeedLimitArray = new ushort?[Singleton<NetManager>.instance.m_segments.m_size][];
			laneArrowFlags = new LaneArrows?[Singleton<NetManager>.instance.m_lanes.m_size];
			laneAllowedVehicleTypesArray = new ExtVehicleType?[Singleton<NetManager>.instance.m_segments.m_size][];
			highwayLaneArrowFlags = new LaneArrows?[Singleton<NetManager>.instance.m_lanes.m_size];
			nodeTrafficLightFlag = new bool?[Singleton<NetManager>.instance.m_nodes.m_size];
			segmentNodeFlags = new Configuration.SegmentNodeFlags[Singleton<NetManager>.instance.m_segments.m_size][];
			for (int i = 0; i < segmentNodeFlags.Length; ++i) {
				segmentNodeFlags[i] = new Configuration.SegmentNodeFlags[2];
			}
			initDone = true;
		}
	}
}
