using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;

namespace TrafficManager.Manager.Impl {
	public class JunctionRestrictionsManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<Configuration.SegmentNodeConf>>, IJunctionRestrictionsManager {
		public static JunctionRestrictionsManager Instance { get; private set; } = null;

		static JunctionRestrictionsManager() {
			Instance = new JunctionRestrictionsManager();
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"- Not implemented -");
			// TODO implement
		}

		public bool MayHaveJunctionRestrictions(ushort nodeId) {
			NetNode.Flags flags = NetNode.Flags.None;
			Services.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
				flags = node.m_flags;
				return true;
			});

			if (LogicUtil.CheckFlags((uint)flags, (uint)(NetNode.Flags.Created | NetNode.Flags.Deleted), (uint)NetNode.Flags.Created)) {
				return false;
			}

			return LogicUtil.CheckFlags((uint)flags, (uint)(NetNode.Flags.Junction | NetNode.Flags.Bend));
		}

		public bool HasJunctionRestrictions(ushort nodeId) {
			if (! Services.NetService.IsNodeValid(nodeId)) {
				return false;
			}

			bool ret = false;
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
				if (segmentId == 0) {
					return true;
				}

				Configuration.SegmentNodeFlags flags = Flags.getSegmentNodeFlags(segmentId, segment.m_startNode == nodeId);
				if (flags != null && !flags.IsDefault()) {
					ret = true;
					return false;
				}

				return true;
			});

			return ret;
		}

		public void RemoveJunctionRestrictions(ushort nodeId) {
			Services.NetService.IterateNodeSegments(nodeId, delegate (ushort segmentId, ref NetSegment segment) {
				if (segmentId == 0) {
					return true;
				}

				Flags.resetSegmentNodeFlags(segmentId, segment.m_startNode == nodeId);
				return true;
			});
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			Flags.resetSegmentNodeFlags(geometry.SegmentId, false);
			Flags.resetSegmentNodeFlags(geometry.SegmentId, true);

			ushort startNodeId = 0;
			bool removeAllAtStartNode = false;
			ushort endNodeId = 0;
			bool removeAllAtEndNode = false;

			Services.NetService.ProcessSegment(geometry.SegmentId, delegate (ushort segmentId, ref NetSegment segment) {
				if (segment.m_startNode != 0) {
					startNodeId = segment.m_startNode;
					removeAllAtStartNode = !MayHaveJunctionRestrictions(startNodeId);
				}

				if (segment.m_endNode != 0) {
					endNodeId = segment.m_endNode;
					removeAllAtEndNode = !MayHaveJunctionRestrictions(endNodeId);
				}
				return true;
			});

			if (removeAllAtStartNode) {
				RemoveJunctionRestrictions(startNodeId);
			}

			if (removeAllAtEndNode) {
				RemoveJunctionRestrictions(endNodeId);
			}
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
			if (!Services.NetService.IsSegmentValid(segmentId))
				return false;
			Flags.setStraightLaneChangingAllowed(segmentId, startNode, value);
			OnSegmentChange(segmentId);
			return true;
		}

		public bool ToggleUturnAllowed(ushort segmentId, bool startNode) {
			return SetUturnAllowed(segmentId, startNode, !IsUturnAllowed(segmentId, startNode));
		}

		public bool SetUturnAllowed(ushort segmentId, bool startNode, bool value) {
			if (!Services.NetService.IsSegmentValid(segmentId))
				return false;
			if (!value && LaneConnectionManager.Instance.HasUturnConnections(segmentId, startNode))
				return false;
			Flags.setUTurnAllowed(segmentId, startNode, value);
			OnSegmentChange(segmentId);
			return true;
		}

		public bool ToggleEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
			return SetEnteringBlockedJunctionAllowed(segmentId, startNode, !IsEnteringBlockedJunctionAllowed(segmentId, startNode));
		}

		public bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, bool value) {
			if (!Services.NetService.IsSegmentValid(segmentId))
				return false;
			Flags.setEnterWhenBlockedAllowed(segmentId, startNode, value);
			//OnSegmentChange(segmentId);
			return true;
		}

		public bool TogglePedestrianCrossingAllowed(ushort segmentId, bool startNode) {
			return SetPedestrianCrossingAllowed(segmentId, startNode, !IsPedestrianCrossingAllowed(segmentId, startNode));
		}

		public bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, bool value) {
			if (!Services.NetService.IsSegmentValid(segmentId))
				return false;
			Flags.setPedestrianCrossingAllowed(segmentId, startNode, !IsPedestrianCrossingAllowed(segmentId, startNode));
			OnSegmentChange(segmentId);
			return true;
		}

		protected void OnSegmentChange(ushort segmentId) {
			RoutingManager.Instance.RequestRecalculation(segmentId);
			SubscribeToSegmentGeometry(segmentId);
			if (OptionsManager.Instance.MayPublishSegmentChanges()) {
				Services.NetService.PublishSegmentChanges(segmentId);
			}
		}

		public bool LoadData(List<Configuration.SegmentNodeConf> data) {
			bool success = true;
			Log.Info($"Loading junction restrictions. {data.Count} elements");
			foreach (Configuration.SegmentNodeConf segNodeConf in data) {
				try {
					if (!Services.NetService.IsSegmentValid(segNodeConf.segmentId))
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
			for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; segmentId++) {
				try {
					if (!Services.NetService.IsSegmentValid((ushort)segmentId))
						continue;

					ushort startNodeId = netManager.m_segments.m_buffer[segmentId].m_startNode;
					ushort endNodeId = netManager.m_segments.m_buffer[segmentId].m_endNode;

					Configuration.SegmentNodeFlags startNodeFlags = Services.NetService.IsNodeValid(startNodeId) ? Flags.getSegmentNodeFlags((ushort)segmentId, true) : null;
					Configuration.SegmentNodeFlags endNodeFlags = Services.NetService.IsNodeValid(endNodeId) ? Flags.getSegmentNodeFlags((ushort)segmentId, false) : null;

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

					Configuration.SegmentNodeConf conf = new Configuration.SegmentNodeConf((ushort)segmentId);

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
