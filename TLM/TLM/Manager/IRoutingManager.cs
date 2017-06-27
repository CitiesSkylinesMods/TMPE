using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public enum LaneEndTransitionType {
		/// <summary>
		/// No connection
		/// </summary>
		Invalid,
		/// <summary>
		/// Lane arrow or regular lane connection
		/// </summary>
		Default,
		/// <summary>
		/// Custom lane connection
		/// </summary>
		LaneConnection,
		/// <summary>
		/// Relaxed connection for road vehicles [!] that do not have to follow lane arrows
		/// </summary>
		Relaxed
	}

	public struct SegmentRoutingData {
		public bool startNodeOutgoingOneWay;
		public bool endNodeOutgoingOneWay;
		public bool highway;

		public override string ToString() {
			return $"[SegmentRoutingData\n" +
				"\t" + $"startNodeOutgoingOneWay = {startNodeOutgoingOneWay}\n" +
				"\t" + $"endNodeOutgoingOneWay = {endNodeOutgoingOneWay}\n" +
				"\t" + $"highway = {highway}\n" +
				"SegmentRoutingData]";
		}

		public void Reset() {
			startNodeOutgoingOneWay = false;
			endNodeOutgoingOneWay = false;
			highway = false;
		}
	}

	public struct LaneEndRoutingData {
		public bool routed;

		public LaneTransitionData[] segment0Transitions;
		public LaneTransitionData[] segment1Transitions;
		public LaneTransitionData[] segment2Transitions;
		public LaneTransitionData[] segment3Transitions;
		public LaneTransitionData[] segment4Transitions;
		public LaneTransitionData[] segment5Transitions;
		public LaneTransitionData[] segment6Transitions;
		public LaneTransitionData[] segment7Transitions;

		public override string ToString() {
			return $"[LaneEndRoutingData\n" +
				"\t" + $"routed = {routed}\n" +
				"\t" + $"segment0Transitions = {(segment0Transitions == null ? "<null>" : segment0Transitions.ArrayToString())}\n" +
				"\t" + $"segment1Transitions = {(segment1Transitions == null ? "<null>" : segment1Transitions.ArrayToString())}\n" +
				"\t" + $"segment2Transitions = {(segment2Transitions == null ? "<null>" : segment2Transitions.ArrayToString())}\n" +
				"\t" + $"segment3Transitions = {(segment3Transitions == null ? "<null>" : segment3Transitions.ArrayToString())}\n" +
				"\t" + $"segment4Transitions = {(segment4Transitions == null ? "<null>" : segment4Transitions.ArrayToString())}\n" +
				"\t" + $"segment5Transitions = {(segment5Transitions == null ? "<null>" : segment5Transitions.ArrayToString())}\n" +
				"\t" + $"segment6Transitions = {(segment6Transitions == null ? "<null>" : segment6Transitions.ArrayToString())}\n" +
				"\t" + $"segment7Transitions = {(segment7Transitions == null ? "<null>" : segment7Transitions.ArrayToString())}\n" +
				"LaneEndRoutingData]";
		}

		public void Reset() {
			routed = false;
			segment0Transitions = null;
			segment1Transitions = null;
			segment2Transitions = null;
			segment3Transitions = null;
			segment4Transitions = null;
			segment5Transitions = null;
			segment6Transitions = null;
			segment7Transitions = null;
		}

		public LaneTransitionData[] GetTransitions(int index) {
			switch (index) {
				case 0:
					return segment0Transitions;
				case 1:
					return segment1Transitions;
				case 2:
					return segment2Transitions;
				case 3:
					return segment3Transitions;
				case 4:
					return segment4Transitions;
				case 5:
					return segment5Transitions;
				case 6:
					return segment6Transitions;
				case 7:
					return segment7Transitions;
			}
			return null;
		}

		public void SetTransitions(int index, LaneTransitionData[] transitions) {
			switch (index) {
				case 0:
					segment0Transitions = transitions;
					return;
				case 1:
					segment1Transitions = transitions;
					return;
				case 2:
					segment2Transitions = transitions;
					return;
				case 3:
					segment3Transitions = transitions;
					return;
				case 4:
					segment4Transitions = transitions;
					return;
				case 5:
					segment5Transitions = transitions;
					return;
				case 6:
					segment6Transitions = transitions;
					return;
				case 7:
					segment7Transitions = transitions;
					return;
			}
		}
	}

	public struct LaneTransitionData {
		public uint laneId;
		public byte laneIndex;
		public LaneEndTransitionType type;
		public byte distance;
		public ushort segmentId;

		public override string ToString() {
			return $"[LaneTransitionData\n" +
				"\t" + $"laneId = {laneId}\n" +
				"\t" + $"laneIndex = {laneIndex}\n" +
				"\t" + $"segmentId = {segmentId}\n" +
				"\t" + $"type = {type}\n" +
				"\t" + $"distance = {distance}\n" +
				"LaneTransitionData]";
		}

		public void Set(uint laneId, byte laneIndex, LaneEndTransitionType type, ushort segmentId, byte distance) {
			this.laneId = laneId;
			this.laneIndex = laneIndex;
			this.type = type;
			this.distance = distance;
			this.segmentId = segmentId;
		}

		public void Set(uint laneId, byte laneIndex, LaneEndTransitionType type, ushort segmentId) {
			Set(laneId, laneIndex, type, segmentId, 0);
		}
	}

	public interface IRoutingManager {
		// TODO documentation
		void SimulationStep();
		void RequestFullRecalculation(bool notify);
		void RequestRecalculation(ushort segmentId, bool propagate = true);
	}
}
