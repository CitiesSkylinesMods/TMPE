using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
		/// For each node: Defines if a crossing exists or not. If no entry exists for a given node id, the game's default setting is used
		/// </summary>
		private static Dictionary<ushort, bool> nodeCrossingFlag = new Dictionary<ushort, bool>();

		/// <summary>
		/// For each lane: Defines the lane arrows which are set
		/// </summary>
		private static Dictionary<uint, LaneArrows> laneArrowFlags = new Dictionary<uint, LaneArrows>();

		public static void setNodeTrafficLight(ushort nodeId,  bool flag) {
			if (nodeId <= 0)
				return;

			nodeTrafficLightFlag[nodeId] = flag;
			applyNodeTrafficLightFlag(nodeId);
		}

		public static void setNodeCrossingFlag(ushort nodeId, bool flag) {
			if (nodeId <= 0)
				return;

			nodeCrossingFlag[nodeId] = flag;
			applyNodeCrossingFlag(nodeId);
		}

		public static void setLaneArrowFlags(uint laneId, LaneArrows flags) {
			if (laneId <= 0)
				return;

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

			if (!laneArrowFlags.ContainsKey(laneId)) {
				// read currently defined arrows
				uint laneFlags = (uint)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;
				laneFlags &= lfr; // filter arrows
				laneArrowFlags[laneId] = (LaneArrows)laneFlags;
			}

			laneArrowFlags[laneId] ^= flags;
			applyLaneArrowFlags(laneId);
		}

		public static void applyAllFlags() {
			foreach (ushort nodeId in nodeTrafficLightFlag.Keys) {
				applyNodeTrafficLightFlag(nodeId);
				applyNodeCrossingFlag(nodeId);
			}

			foreach (uint laneId in laneArrowFlags.Keys) {
				applyLaneArrowFlags(laneId);
			}
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

		public static void applyNodeCrossingFlag(ushort nodeId) {
			if (nodeId <= 0 || !nodeCrossingFlag.ContainsKey(nodeId))
				return;

			bool flag = nodeCrossingFlag[nodeId];
			if (flag) {
				//Log.Message($"Adding crosswalk @ node {nodeId}");
				Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags |= NetNode.Flags.Junction;
			} else {
				//Log.Message($"Removing crosswalk @ node {nodeId}");
				Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags &= ~NetNode.Flags.Junction;
			}
		}

		public static void applyLaneArrowFlags(uint laneId) {
			if (laneId <= 0 || !laneArrowFlags.ContainsKey(laneId))
				return;

			LaneArrows flags = laneArrowFlags[laneId];
			uint laneFlags = (uint)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;
			laneFlags &= ~lfr; // remove all arrows
			laneFlags |= (uint)flags; // add desired arrows
			Log.Message($"Setting lane flags @ lane {laneId}, seg. {Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment} to {((NetLane.Flags)laneFlags).ToString()}");
			Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = Convert.ToUInt16(laneFlags);
		}

		internal static void clearAll() {
			nodeTrafficLightFlag.Clear();
			nodeCrossingFlag.Clear();
			laneArrowFlags.Clear();
		}

		internal static void OnLevelUnloading() {
			clearAll();
		}
	}
}
