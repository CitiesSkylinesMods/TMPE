using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;

namespace TrafficManager.Traffic.Data {
	/// <summary>
	/// Segment flags hold both segment end flags
	/// </summary>
	public struct SegmentFlags {
		public SegmentEndFlags startNodeFlags;
		public SegmentEndFlags endNodeFlags;

		public void UpdateDefaults(SegmentGeometry segmentGeometry) {
			startNodeFlags.UpdateDefaults(segmentGeometry.StartNodeGeometry);
			endNodeFlags.UpdateDefaults(segmentGeometry.EndNodeGeometry);
		}

		public bool IsUturnAllowed(bool startNode) {
			return startNode ? startNodeFlags.IsUturnAllowed() : endNodeFlags.IsUturnAllowed();
		}

		public bool IsLaneChangingAllowedWhenGoingStraight(bool startNode) {
			return startNode ? startNodeFlags.IsLaneChangingAllowedWhenGoingStraight() : endNodeFlags.IsLaneChangingAllowedWhenGoingStraight();
		}

		public bool IsEnteringBlockedJunctionAllowed(bool startNode) {
			return startNode ? startNodeFlags.IsEnteringBlockedJunctionAllowed() : endNodeFlags.IsEnteringBlockedJunctionAllowed();
		}

		public bool IsPedestrianCrossingAllowed(bool startNode) {
			return startNode ? startNodeFlags.IsPedestrianCrossingAllowed() : endNodeFlags.IsPedestrianCrossingAllowed();
		}

		public TernaryBool GetUturnAllowed(bool startNode) {
			return startNode ? startNodeFlags.uturnAllowed : endNodeFlags.uturnAllowed;
		}

		public TernaryBool GetLaneChangingAllowedWhenGoingStraight(bool startNode) {
			return startNode ? startNodeFlags.straightLaneChangingAllowed : endNodeFlags.straightLaneChangingAllowed;
		}

		public TernaryBool GetEnteringBlockedJunctionAllowed(bool startNode) {
			return startNode ? startNodeFlags.enterWhenBlockedAllowed : endNodeFlags.enterWhenBlockedAllowed;
		}

		public TernaryBool GetPedestrianCrossingAllowed(bool startNode) {
			return startNode ? startNodeFlags.pedestrianCrossingAllowed : endNodeFlags.pedestrianCrossingAllowed;
		}

		public void SetUturnAllowed(bool startNode, bool value) {
			if (startNode) {
				startNodeFlags.SetUturnAllowed(value);
			} else {
				endNodeFlags.SetUturnAllowed(value);
			}
		}

		public void SetLaneChangingAllowedWhenGoingStraight(bool startNode, bool value) {
			if (startNode) {
				startNodeFlags.SetLaneChangingAllowedWhenGoingStraight(value);
			} else {
				endNodeFlags.SetLaneChangingAllowedWhenGoingStraight(value);
			}
		}

		public void SetEnteringBlockedJunctionAllowed(bool startNode, bool value) {
			if (startNode) {
				startNodeFlags.SetEnteringBlockedJunctionAllowed(value);
			} else {
				endNodeFlags.SetEnteringBlockedJunctionAllowed(value);
			}
		}

		public void SetPedestrianCrossingAllowed(bool startNode, bool value) {
			if (startNode) {
				startNodeFlags.SetPedestrianCrossingAllowed(value);
			} else {
				endNodeFlags.SetPedestrianCrossingAllowed(value);
			}
		}

		public bool IsDefault() {
			return startNodeFlags.IsDefault() && endNodeFlags.IsDefault();
		}

		public void Reset(bool? startNode=null) {
			if (startNode == null || (bool)startNode) {
				startNodeFlags.Reset();
			}

			if (startNode == null || ! (bool)startNode) {
				endNodeFlags.Reset();
			}
		}

		public override string ToString() {
			return $"[SegmentFlags\n" +
				"\t" + $"startNodeFlags = {startNodeFlags}\n" +
				"\t" + $"endNodeFlags = {endNodeFlags}\n" +
				"SegmentFlags]";
		}
	}
}
