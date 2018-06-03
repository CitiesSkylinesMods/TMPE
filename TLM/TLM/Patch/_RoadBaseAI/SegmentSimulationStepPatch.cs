using ColossalFramework;
using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Patch._RoadBaseAI {
	// [Harmony] Manually patched because struct references are used
	public class SegmentSimulationStepPatch {
		/// <summary>
		/// Updates lane arrows and performs traffic measurement on segment.
		/// </summary>
		public static void Prefix(RoadBaseAI __instance, ushort segmentID, ref NetSegment data) {
			// TODO check if this is required *START*
			uint curLaneId = data.m_lanes;
			int numLanes = data.Info.m_lanes.Length;
			uint laneIndex = 0;

			while (laneIndex < numLanes && curLaneId != 0u) {
				Flags.applyLaneArrowFlags(curLaneId);

				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}
			// TODO check if this is required *END*

			Constants.ManagerFactory.TrafficMeasurementManager.OnBeforeSimulationStep(segmentID, ref data);
		}
	}
}
