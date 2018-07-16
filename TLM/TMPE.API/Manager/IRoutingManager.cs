using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.Manager {
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
		public LaneTransitionData[] transitions;

		public override string ToString() {
			return $"[LaneEndRoutingData\n" +
				"\t" + $"routed = {routed}\n" +
				"\t" + $"transitions = {(transitions == null ? "<null>" : transitions.ArrayToString())}\n" +
				"LaneEndRoutingData]";
		}

		public void Reset() {
			routed = false;
			transitions = null;
		}

		public void RemoveTransition(uint laneId) {
			if (transitions == null) {
				return;
			}

			int index = -1;
			for (int i = 0; i < transitions.Length; ++i) {
				if (transitions[i].laneId == laneId) {
					index = i;
					break;
				}
			}

			if (index < 0) {
				return;
			}

			if (transitions.Length == 1) {
				Reset();
				return;
			}

			LaneTransitionData[] newTransitions = new LaneTransitionData[transitions.Length - 1];
			if (index > 0) {
				Array.Copy(transitions, 0, newTransitions, 0, index);
			}
			if (index < transitions.Length - 1) {
				Array.Copy(transitions, index + 1, newTransitions, index, transitions.Length - index - 1);
			}
			transitions = newTransitions;
		}

		public void AddTransitions(LaneTransitionData[] transitionsToAdd) {
			if (transitions == null) {
				transitions = transitionsToAdd;
				routed = true;
				return;
			}

			LaneTransitionData[] newTransitions = new LaneTransitionData[transitions.Length + transitionsToAdd.Length];
			Array.Copy(transitions, newTransitions, transitions.Length);
			Array.Copy(transitionsToAdd, 0, newTransitions, transitions.Length, transitionsToAdd.Length);
			transitions = newTransitions;

			routed = true;
		}

		public void AddTransition(LaneTransitionData transition) {
			AddTransitions(new LaneTransitionData[1] { transition });
		}
	}

	public struct LaneTransitionData {
		public uint laneId;
		public byte laneIndex;
		public LaneEndTransitionType type;
		public byte distance;
		public ushort segmentId;
		public bool startNode;

		public override string ToString() {
			return $"[LaneTransitionData\n" +
				"\t" + $"laneId = {laneId}\n" +
				"\t" + $"laneIndex = {laneIndex}\n" +
				"\t" + $"segmentId = {segmentId}\n" +
				"\t" + $"startNode = {startNode}\n" +
				"\t" + $"type = {type}\n" +
				"\t" + $"distance = {distance}\n" +
				"LaneTransitionData]";
		}

		public void Set(uint laneId, byte laneIndex, LaneEndTransitionType type, ushort segmentId, bool startNode, byte distance) {
			this.laneId = laneId;
			this.laneIndex = laneIndex;
			this.type = type;
			this.distance = distance;
			this.segmentId = segmentId;
			this.startNode = startNode;
		}

		public void Set(uint laneId, byte laneIndex, LaneEndTransitionType type, ushort segmentId, bool startNode) {
			Set(laneId, laneIndex, type, segmentId, startNode, 0);
		}
	}

	public interface IRoutingManager {
		// TODO documentation
		void SimulationStep();
		void RequestFullRecalculation();
		void RequestRecalculation(ushort segmentId, bool propagate = true);
	}
}
