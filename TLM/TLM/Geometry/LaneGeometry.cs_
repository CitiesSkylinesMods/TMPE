using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Util;

namespace TrafficManager.Traffic {
	public class LaneGeometry {
		private static readonly ushort[] POW2MASKS = new ushort[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768 };

		public const NetInfo.LaneType ROUTED_LANE_TYPES = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
		public const VehicleInfo.VehicleType ROUTED_VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram;
		public const VehicleInfo.VehicleType ARROW_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

		public bool Valid {
			get {
				return Constants.ServiceFactory.NetService.IsLaneValid(LaneId);
			}
		}

		public uint LaneId {
			get {
				if (laneId == null) {
					Constants.ServiceFactory.NetService.IterateSegmentLanes(SegmentId, delegate (uint lId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segmentId, ref NetSegment segment, byte laneIndex) {
						if (laneIndex == LaneIndex) {
							laneId = lId;
							return false;
						}
						return true;
					});
				}

				if (laneId == null) {
					return 0;
				} else {
					return (uint)laneId;
				}
			}
		}
		private uint? laneId;

		public NetInfo.Lane LaneInfo {
			get {
				if (laneInfo == null) {
					Constants.ServiceFactory.NetService.IterateSegmentLanes(SegmentId, delegate (uint lId, ref NetLane lane, NetInfo.Lane info, ushort segmentId, ref NetSegment segment, byte laneIndex) {
						if (laneIndex == LaneIndex) {
							laneInfo = info;
							return false;
						}
						return true;
					});
				}

				return laneInfo;
			}
		}
		private NetInfo.Lane laneInfo;

		public int LaneIndex { get; private set; }
		public ushort SegmentId { get; private set; }

		public int InnerSimilarIndex {
			get {
				if (innerSimilarIndex == null) {
					Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segmentId, ref NetSegment segment) {
						NetInfo.Lane laneInfo = segment.Info.m_lanes[LaneIndex];
						innerSimilarIndex = (byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0 ? laneInfo.m_similarLaneIndex : laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1;
						return true;
					});
				}

				return (int)innerSimilarIndex;
			}
		}
		private int? innerSimilarIndex;

		public int OuterSimilarIndex {
			get {
				if (outerSimilarIndex == null) {
					Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segmentId, ref NetSegment segment) {
						NetInfo.Lane laneInfo = segment.Info.m_lanes[LaneIndex];
						outerSimilarIndex = (byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0 ? laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1 : laneInfo.m_similarLaneIndex;
						return true;
					});
				}

				return (int)outerSimilarIndex;
			}
		}
		private int? outerSimilarIndex;

		public LaneEndGeometry StartNodeGeometry = null;
		public LaneEndGeometry EndNodeGeometry = null;

		public override string ToString() {
			return $"[LaneGeometry (id {LaneId}, idx {LaneIndex} @ seg. {SegmentId})\n" +
				"\t" + $"Valid = {Valid}\n" +
				"\t" + $"InnerSimilarIndex = {InnerSimilarIndex}\n" +
				"\t" + $"OuterSimilarIndex = {OuterSimilarIndex}\n" +
				"\t" + $"StartNodeGeometry = {StartNodeGeometry}\n" +
				"\t" + $"EndNodeGeometry = {EndNodeGeometry}\n" +
				"\t" + $"LaneInfo.m_vehicleType = {(LaneInfo == null ? "<null>" : LaneInfo.m_vehicleType.ToString())}\n" +
				"\t" + $"LaneInfo.m_laneType = {(LaneInfo == null ? "<null>" : LaneInfo.m_laneType.ToString())}\n" +
				"LaneGeometry]";
		}

		public LaneGeometry(ushort segmentId, int laneIndex) {
			SegmentId = segmentId;
			LaneIndex = laneIndex;
			StartNodeGeometry = new LaneEndGeometry(this, true);
			EndNodeGeometry = new LaneEndGeometry(this, false);
		}

		protected void Reset() {
			laneId = null;
			innerSimilarIndex = null;
			outerSimilarIndex = null;
			laneInfo = null;
			StartNodeGeometry.Reset();
			EndNodeGeometry.Reset();
		}

		public void Recalculate() {
			Reset();

			if (!Valid) {
				return;
			}

			StartNodeGeometry.Recalculate();
			EndNodeGeometry.Recalculate();
		}

		public LaneEndGeometry GetEnd(bool startNode) {
			return startNode ? StartNodeGeometry : EndNodeGeometry;
		}
	}
}
