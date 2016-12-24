using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	public class JunctionRestrictionsManager : ICustomManager {
		public static JunctionRestrictionsManager Instance { get; private set; } = null;

		static JunctionRestrictionsManager() {
			Instance = new JunctionRestrictionsManager();
		}

		public bool HasJunctionRestrictions(ushort nodeId) {
			NetManager netManager = Singleton<NetManager>.instance;

			if ((netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
				return false;

			for (int i = 0; i < 8; ++i) {
				ushort segmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(i);
				if (segmentId == 0)
					continue;

				Configuration.SegmentNodeFlags flags = Flags.getSegmentNodeFlags(segmentId, netManager.m_segments.m_buffer[segmentId].m_startNode == nodeId);
				if (flags != null && !flags.IsDefault())
					return true;
			}
			return false;
		}

		public bool IsLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
			return Flags.getStraightLaneChangingAllowed(segmentId, startNode);
		}

		public bool IsUturnAllowed(ushort segmentId, bool startNode) {
			return Flags.getUTurnAllowed(segmentId, startNode);
		}

		public bool IsEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
			return Flags.getEnterWhenBlockedAllowed(segmentId, startNode);
		}

		public bool IsPedestrianCrossingAllowed(ushort segmentId, bool startNode) {
			return Flags.getPedestrianCrossingAllowed(segmentId, startNode);
		}

		public void ToggleLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
			SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, !IsLaneChangingAllowedWhenGoingStraight(segmentId, startNode));
		}

		public void SetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, bool value) {
			Flags.setStraightLaneChangingAllowed(segmentId, startNode, value);
		}

		public bool ToggleUturnAllowed(ushort segmentId, bool startNode) {
			return SetUturnAllowed(segmentId, startNode, !IsUturnAllowed(segmentId, startNode));
		}

		public bool SetUturnAllowed(ushort segmentId, bool startNode, bool value) {
			if (!value && LaneConnectionManager.Instance.HasUturnConnections(segmentId, startNode))
				return false;
			Flags.setUTurnAllowed(segmentId, startNode, value);
			return true;
		}

		public void ToggleEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
			SetEnteringBlockedJunctionAllowed(segmentId, startNode, !IsEnteringBlockedJunctionAllowed(segmentId, startNode));
		}

		public void SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, bool value) {
			Flags.setEnterWhenBlockedAllowed(segmentId, startNode, value);
		}

		public void TogglePedestrianCrossingAllowed(ushort segmentId, bool startNode) {
			SetPedestrianCrossingAllowed(segmentId, startNode, !IsPedestrianCrossingAllowed(segmentId, startNode));
		}

		public void SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, bool value) {
			Flags.setPedestrianCrossingAllowed(segmentId, startNode, !IsPedestrianCrossingAllowed(segmentId, startNode));
		}

		public void OnLevelUnloading() {
			
		}
	}
}
