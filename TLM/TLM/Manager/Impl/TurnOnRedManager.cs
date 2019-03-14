using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Impl;
using TrafficManager.Util;
using static TrafficManager.Geometry.Impl.NodeGeometry;

namespace TrafficManager.Manager.Impl {
	public class TurnOnRedManager : AbstractGeometryObservingManager, ITurnOnRedManager {
		public static TurnOnRedManager Instance { get; private set; } = new TurnOnRedManager();

		public TurnOnRedSegments[] TurnOnRedSegments { get; private set; }

		private TurnOnRedManager() {
			TurnOnRedSegments = new TurnOnRedSegments[2 * NetManager.MAX_SEGMENT_COUNT];
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Turn-on-red segments:");
			for (int i = 0; i < TurnOnRedSegments.Length; ++i) {
				Log._Debug($"Segment end {i}: {TurnOnRedSegments[i]}");
			}
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			UpdateSegment(geometry);
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			ResetSegment(geometry.SegmentId);
		}

		protected void UpdateSegment(SegmentGeometry geometry) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[25];
			if (debug) {
				Log._Debug($"TurnOnRedManager.UpdateSegment({geometry.SegmentId}) called.");
			}
#endif

			ResetSegment(geometry.SegmentId);
			SegmentEndGeometry startEndGeo = geometry.GetEnd(true);
			if (startEndGeo != null) {
				UpdateSegmentEnd(startEndGeo);
			}

			SegmentEndGeometry endEndGeo = geometry.GetEnd(false);
			if (endEndGeo != null) {
				UpdateSegmentEnd(endEndGeo);
			}
		}

		protected void UpdateSegmentEnd(SegmentEndGeometry endGeo) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[25];
			if (debug) {
				Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}) called.");
			}
#endif

			// check if traffic can flow to the node and that there is at least one outgoing segment
			if (endGeo.OutgoingOneWay || endGeo.NumOutgoingSegments <= 0) {
#if DEBUG
				if (debug) {
					Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): outgoing one-way or insufficient number of outgoing segments.");
				}
#endif
				return;
			}

			bool lhd = Services.SimulationService.LeftHandDrive;
			ushort nodeId = endGeo.NodeId();

			// check node
			// note that we must not check for the `TrafficLights` flag here because the flag might not be loaded yet
			bool nodeValid = false;
			Services.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
				nodeValid =
					(node.m_flags & NetNode.Flags.LevelCrossing) == NetNode.Flags.None &&
					node.Info?.m_class?.m_service != ItemClass.Service.Beautification;
				return true;
			});

			if (! nodeValid) {
#if DEBUG
				if (debug) {
					Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): node invalid");
				}
#endif
				return;
			}

			// get left/right segments
			ushort leftSegmentId = 0;
			ushort rightSegmentId = 0;
			Services.NetService.ProcessSegment(endGeo.SegmentId, delegate (ushort segId, ref NetSegment seg) {
				seg.GetLeftAndRightSegments(nodeId, out leftSegmentId, out rightSegmentId);
				return true;
			});

#if DEBUG
			if (debug) {
				Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): got left/right segments: {leftSegmentId}/{rightSegmentId}");
			}
#endif

			// validate left/right segments according to geometric properties
			if (leftSegmentId != 0 && !endGeo.IsLeftSegment(leftSegmentId)) {
#if DEBUG
				if (debug) {
					Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): left segment is not geometrically left");
				}
#endif
				leftSegmentId = 0;
			}

			if (rightSegmentId != 0 && !endGeo.IsRightSegment(rightSegmentId)) {
#if DEBUG
				if (debug) {
					Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): right segment is not geometrically right");
				}
#endif
				rightSegmentId = 0;
			}

			// check for incoming one-ways
			if (leftSegmentId != 0 && SegmentGeometry.Get(leftSegmentId).GetEnd(nodeId).IncomingOneWay) {
#if DEBUG
				if (debug) {
					Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): left segment is incoming one-way");
				}
#endif
				leftSegmentId = 0;
			}

			if (rightSegmentId != 0 && SegmentGeometry.Get(rightSegmentId).GetEnd(nodeId).IncomingOneWay) {
#if DEBUG
				if (debug) {
					Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): right segment is incoming one-way");
				}
#endif
				rightSegmentId = 0;
			}

			if (endGeo.IncomingOneWay) {
				if (lhd && rightSegmentId != 0 || !lhd && leftSegmentId != 0) {
					// special case: one-way to one-way in non-preferred direction
#if DEBUG
					if (debug) {
						Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): source is incoming one-way. checking for one-way in non-preferred direction");
					}
#endif
					ushort targetSegmentId = lhd ? rightSegmentId : leftSegmentId;
					SegmentEndGeometry targetEndGeo = SegmentGeometry.Get(targetSegmentId)?.GetEnd(nodeId);
					if (targetEndGeo == null || !targetEndGeo.OutgoingOneWay) {
						if (targetEndGeo == null) {
							Log.Error($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): One-way to one-way: Target segment end geometry not found for segment id {targetSegmentId} @ {nodeId}");
						}

						// disallow turn in non-preferred direction
#if DEBUG
						if (debug) {
							Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): turn in non-preferred direction {(lhd ? "right" : "left")} disallowed");
						}
#endif
						if (lhd) {
							rightSegmentId = 0;
						} else {
							leftSegmentId = 0;
						}
					}
				}
			} else if (lhd) {
				// default case (LHD): turn in preferred direction
				rightSegmentId = 0;
			} else {
				// default case (RHD): turn in preferred direction
				leftSegmentId = 0;
			}

			int index = GetIndex(endGeo.SegmentId, endGeo.StartNode);
			TurnOnRedSegments[index].leftSegmentId = leftSegmentId;
			TurnOnRedSegments[index].rightSegmentId = rightSegmentId;

#if DEBUG
			if (debug) {
				Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({endGeo.SegmentId}, {endGeo.StartNode}): Finished calculation. leftSegmentId={leftSegmentId}, rightSegmentId={rightSegmentId}");
			}
#endif
		}

		protected void ResetSegment(ushort segmentId) {
			TurnOnRedSegments[GetIndex(segmentId, true)].Reset();
			TurnOnRedSegments[GetIndex(segmentId, false)].Reset();
		}

		public override void OnBeforeLoadData() {
			base.OnBeforeLoadData();

			// JunctionRestrictionsManager requires our data during loading of custom data
			for (uint i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
				SegmentGeometry geo = SegmentGeometry.Get((ushort)i);
				if (geo != null && geo.IsValid()) {
					HandleValidSegment(geo);
				}
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < TurnOnRedSegments.Length; ++i) {
				TurnOnRedSegments[i].Reset();
			}
		}

		public int GetIndex(ushort segmentId, bool startNode) {
			return (int)segmentId + (startNode ? 0 : NetManager.MAX_SEGMENT_COUNT);
		}
	}
}
