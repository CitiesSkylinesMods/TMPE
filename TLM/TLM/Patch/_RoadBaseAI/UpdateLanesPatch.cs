using ColossalFramework;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using static RoadBaseAI;

namespace TrafficManager.Patch._RoadBaseAI {
	[HarmonyPatch(typeof(RoadBaseAI), "UpdateLanes")]
	public class UpdateLanesPatch {
		/// <summary>
		/// Update lane arrows after lane data was updated.
		/// </summary>
		[HarmonyPostfix]
		public static void Postfix(ushort segmentID, ref NetSegment data, bool loading) {
			NetManager netManager = Singleton<NetManager>.instance;	
			uint laneId = netManager.m_segments.m_buffer[segmentID].m_lanes;
			while (laneId != 0) {
				if (!Flags.applyLaneArrowFlags(laneId)) {
					Flags.removeLaneArrowFlags(laneId);
				}
				laneId = netManager.m_lanes.m_buffer[laneId].m_nextLane;
			}
		}
	}
}
