using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;

namespace TrafficManager.State {
	public class Flags {
		[Flags]
		public enum LaneArrows { // compatible with NetLane.Flags
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
		private static Dictionary<ushort, bool> nodeTrafficLightFlag = new Dictionary<ushort, bool>();

		/// <summary>
		/// For each lane: Defines the lane arrows which are set
		/// </summary>
		private static Dictionary<uint, LaneArrows> laneArrowFlags = new Dictionary<uint, LaneArrows>();

		public static void resetTrafficLights(bool all) {
			nodeTrafficLightFlag.Clear();
			for (ushort i = 0; i < Singleton<NetManager>.instance.m_nodes.m_size; ++i) {
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

			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Junction) == NetNode.Flags.None) {
				//Log.Message($"Flags: Node {nodeId} may not have a traffic light (not a junction)");
				return false;
			}

			ItemClass connectionClass = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].Info.GetConnectionClass();

			if (connectionClass == null ||
				(connectionClass.m_service != ItemClass.Service.Road &&
				connectionClass.m_service != ItemClass.Service.PublicTransport))
				return false;

			return true;
		}

		public static void setNodeTrafficLight(ushort nodeId, bool flag) {
			if (nodeId <= 0)
				return;

			Log.Message($"Flags: Set node traffic light: {nodeId}={flag}");

			if (!mayHaveTrafficLight(nodeId)) {
				Log.Warning($"Flags: Refusing to add/delete traffic light to/from node: {nodeId} {flag}");
				return;
			}

			nodeTrafficLightFlag[nodeId] = flag;
			applyNodeTrafficLightFlag(nodeId);
		}

		internal static bool isNodeTrafficLightDefined(ushort nodeId) {
			return nodeId > 0 && nodeTrafficLightFlag.ContainsKey(nodeId);
		}

		internal static bool isNodeTrafficLight(ushort nodeId) {
			if (nodeId <= 0)
				return false;

			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
				return false;

			if (!nodeTrafficLightFlag.ContainsKey(nodeId))
				return (Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;

			return nodeTrafficLightFlag[nodeId];
		}

		public static void setLaneArrowFlags(uint laneId, LaneArrows flags) {
			if (laneId <= 0)
				return;

			if (!mayHaveLaneArrows(laneId)) {
				removeLaneArrowFlags(laneId);
				return;
			}

			laneArrowFlags[laneId] = flags;
			applyLaneArrowFlags(laneId);
		}

		public static LaneArrows? getLaneArrowFlags(uint laneId) {
			if (laneId <= 0 || ! laneArrowFlags.ContainsKey(laneId))
				return null;

			return laneArrowFlags[laneId];
		}

		public static void toggleLaneArrowFlags(uint laneId, LaneArrows flags) {
			if (laneId <= 0)
				return;

			if (!mayHaveLaneArrows(laneId)) {
				removeLaneArrowFlags(laneId);
				return;
			}

			if (!laneArrowFlags.ContainsKey(laneId)) {
				// read currently defined arrows
				uint laneFlags = (uint)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;
				laneFlags &= lfr; // filter arrows
				laneArrowFlags[laneId] = (LaneArrows)laneFlags;
			}

			laneArrowFlags[laneId] ^= flags;
			applyLaneArrowFlags(laneId);
		}

		private static bool mayHaveLaneArrows(uint laneId) {
			if (!isLaneValid(laneId))
				return false;

			NetManager netManager = Singleton<NetManager>.instance;

			NetLane lane = netManager.m_lanes.m_buffer[laneId];
			ushort segmentId = lane.m_segment;
			NetSegment segment = netManager.m_segments.m_buffer[segmentId];

			var dir = NetInfo.Direction.Forward;
			var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = TrafficPriority.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

			NetInfo segmentInfo = segment.Info;
			uint curLaneId = segment.m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;
			int laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				if (curLaneId == laneId) {
					NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
					ushort nodeId = (laneInfo.m_direction == dir3) ? segment.m_endNode : segment.m_startNode;
					
					NetNode node = netManager.m_nodes.m_buffer[nodeId];
					if ((node.m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
						return false;
					return (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
				}
				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}
			return false;
		}

		private static bool isLaneValid(uint laneId) {
			if (laneId <= 0)
				return false;

			NetManager netManager = Singleton<NetManager>.instance;

			NetLane lane = netManager.m_lanes.m_buffer[laneId];
			if ((lane.m_flags & (ushort)NetLane.Flags.Created) == 0)
				return false;

			ushort segmentId = lane.m_segment;
			if (segmentId <= 0)
				return false;
			NetSegment segment = netManager.m_segments.m_buffer[segmentId];

			if ((segment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return false;

			return true;
			/*NetInfo segmentInfo = segment.Info;
			uint curLaneId = segment.m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;
			int laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				if (curLaneId == laneId) {
					return true;
				}
				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}
			return false;*/
		}

		public static void applyAllFlags() {
			foreach (ushort nodeId in nodeTrafficLightFlag.Keys) {
				applyNodeTrafficLightFlag(nodeId);
			}

			List<uint> laneIdsToReset = new List<uint>();
			foreach (uint laneId in laneArrowFlags.Keys) {
				if (!applyLaneArrowFlags(laneId))
					laneIdsToReset.Add(laneId);
			}

			foreach (uint laneId in laneIdsToReset)
				removeLaneArrowFlags(laneId);
		}

		public static void applyNodeTrafficLightFlag(ushort nodeId) {
			if (nodeId <= 0 || !nodeTrafficLightFlag.ContainsKey(nodeId))
				return;

			bool flag = nodeTrafficLightFlag[nodeId];
			if (flag) {
				//Log.Message($"Adding traffic light @ node {nodeId}");
				Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags |= NetNode.Flags.TrafficLights;
			} else {
				//Log.Message($"Removing traffic light @ node {nodeId}");
				Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags &= ~NetNode.Flags.TrafficLights;
			}
		}

		public static bool applyLaneArrowFlags(uint laneId) {
			if (laneId <= 0 || !laneArrowFlags.ContainsKey(laneId))
				return true;

			LaneArrows flags = laneArrowFlags[laneId];
			uint laneFlags = (uint)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;

			if (!mayHaveLaneArrows(laneId))
				return false;

			laneFlags &= ~lfr; // remove all arrows
			laneFlags |= (uint)flags; // add desired arrows
			//Log.Message($"Setting lane flags @ lane {laneId}, seg. {Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment} to {((NetLane.Flags)laneFlags).ToString()}");
			Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = Convert.ToUInt16(laneFlags);
			return true;
		}

		public static void removeLaneArrowFlags(uint laneId) {
			if (laneId <= 0)
				return;

			laneArrowFlags.Remove(laneId);
			uint laneFlags = (uint)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;

			if (((NetLane.Flags)laneFlags & NetLane.Flags.Created) == NetLane.Flags.None) {
				Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = 0;
			} else {
				Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags &= (ushort)~lfr;
			}
		}

		internal static void clearAll() {
			nodeTrafficLightFlag.Clear();
			laneArrowFlags.Clear();
		}

		internal static void OnLevelUnloading() {
			clearAll();
		}
	}
}
