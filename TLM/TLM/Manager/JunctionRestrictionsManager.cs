using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	public class JunctionRestrictionsManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<Configuration.SegmentNodeConf>> {
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

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			Flags.resetSegmentNodeFlags(geometry.SegmentId, false);
			Flags.resetSegmentNodeFlags(geometry.SegmentId, true);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			
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

		public bool ToggleLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
			return SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, !IsLaneChangingAllowedWhenGoingStraight(segmentId, startNode));
		}

		public bool SetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, bool value) {
			if (!NetUtil.IsSegmentValid(segmentId))
				return false;
			Flags.setStraightLaneChangingAllowed(segmentId, startNode, value);
			SubscribeToSegmentGeometry(segmentId);
			return true;
		}

		public bool ToggleUturnAllowed(ushort segmentId, bool startNode) {
			return SetUturnAllowed(segmentId, startNode, !IsUturnAllowed(segmentId, startNode));
		}

		public bool SetUturnAllowed(ushort segmentId, bool startNode, bool value) {
			if (!NetUtil.IsSegmentValid(segmentId))
				return false;
			if (!value && LaneConnectionManager.Instance.HasUturnConnections(segmentId, startNode))
				return false;
			Flags.setUTurnAllowed(segmentId, startNode, value);
			SubscribeToSegmentGeometry(segmentId);
			return true;
		}

		public bool ToggleEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
			return SetEnteringBlockedJunctionAllowed(segmentId, startNode, !IsEnteringBlockedJunctionAllowed(segmentId, startNode));
		}

		public bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, bool value) {
			if (!NetUtil.IsSegmentValid(segmentId))
				return false;
			Flags.setEnterWhenBlockedAllowed(segmentId, startNode, value);
			SubscribeToSegmentGeometry(segmentId);
			return true;
		}

		public bool TogglePedestrianCrossingAllowed(ushort segmentId, bool startNode) {
			return SetPedestrianCrossingAllowed(segmentId, startNode, !IsPedestrianCrossingAllowed(segmentId, startNode));
		}

		public bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, bool value) {
			if (!NetUtil.IsSegmentValid(segmentId))
				return false;
			Flags.setPedestrianCrossingAllowed(segmentId, startNode, !IsPedestrianCrossingAllowed(segmentId, startNode));
			SubscribeToSegmentGeometry(segmentId);
			return true;
		}

		public bool LoadData(List<Configuration.SegmentNodeConf> data) {
			bool success = true;
			Log.Info($"Loading junction restrictions. {data.Count} elements");
			foreach (Configuration.SegmentNodeConf segNodeConf in data) {
				try {
					if (!NetUtil.IsSegmentValid(segNodeConf.segmentId))
						continue;

					Flags.setSegmentNodeFlags(segNodeConf.segmentId, true, segNodeConf.startNodeFlags);
					Flags.setSegmentNodeFlags(segNodeConf.segmentId, false, segNodeConf.endNodeFlags);
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning($"Error loading junction restrictions @ segment {segNodeConf.segmentId}: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		public List<Configuration.SegmentNodeConf> SaveData(ref bool success) {
			List<Configuration.SegmentNodeConf> ret = new List<Configuration.SegmentNodeConf>();

			NetManager netManager = Singleton<NetManager>.instance;
			for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; segmentId++) {
				try {
					if (!NetUtil.IsSegmentValid(segmentId))
						continue;

					ushort startNodeId = netManager.m_segments.m_buffer[segmentId].m_startNode;
					ushort endNodeId = netManager.m_segments.m_buffer[segmentId].m_endNode;

					Configuration.SegmentNodeFlags startNodeFlags = NetUtil.IsNodeValid(startNodeId) ? Flags.getSegmentNodeFlags(segmentId, true) : null;
					Configuration.SegmentNodeFlags endNodeFlags = NetUtil.IsNodeValid(endNodeId) ? Flags.getSegmentNodeFlags(segmentId, false) : null;

					if (startNodeFlags == null && endNodeFlags == null)
						continue;

					bool isDefaultConfiguration = true;
					if (startNodeFlags != null) {
						if (!startNodeFlags.IsDefault())
							isDefaultConfiguration = false;
					}

					if (endNodeFlags != null) {
						if (!endNodeFlags.IsDefault())
							isDefaultConfiguration = false;
					}

					if (isDefaultConfiguration)
						continue;

					Configuration.SegmentNodeConf conf = new Configuration.SegmentNodeConf(segmentId);

					conf.startNodeFlags = startNodeFlags;
					conf.endNodeFlags = endNodeFlags;

					Log._Debug($"Saving segment-at-node flags for seg. {segmentId}");
					ret.Add(conf);
				} catch (Exception e) {
					Log.Error($"Exception occurred while saving segment node flags @ {segmentId}: {e.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}
