using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Traffic.Data {
	/// <summary>
	/// Segment end flags store junction restrictions
	/// </summary>
	public struct SegmentEndFlags {
		public TernaryBool uturnAllowed;
		public TernaryBool straightLaneChangingAllowed;
		public TernaryBool enterWhenBlockedAllowed;
		public TernaryBool pedestrianCrossingAllowed;

		public bool IsUturnAllowed() {
			if (uturnAllowed == TernaryBool.Undefined) {
				return Options.allowUTurns;
			}

			return TernaryBoolUtil.ToBool(uturnAllowed);
		}

		public bool IsLaneChangingAllowedWhenGoingStraight() {
			if (straightLaneChangingAllowed == TernaryBool.Undefined) {
				return Options.allowLaneChangesWhileGoingStraight;
			}

			return TernaryBoolUtil.ToBool(straightLaneChangingAllowed);
		}

		public bool IsEnteringBlockedJunctionAllowed() {
			if (enterWhenBlockedAllowed == TernaryBool.Undefined) {
				return Options.allowEnterBlockedJunctions;
			}

			return TernaryBoolUtil.ToBool(enterWhenBlockedAllowed);
		}

		public bool IsPedestrianCrossingAllowed() {
			if (pedestrianCrossingAllowed == TernaryBool.Undefined) {
				return true;
			}

			return TernaryBoolUtil.ToBool(pedestrianCrossingAllowed);
		}

		public void SetUturnAllowed(bool value) {
			uturnAllowed = TernaryBoolUtil.ToTernaryBool(value);
		}

		public void SetLaneChangingAllowedWhenGoingStraight(bool value) {
			straightLaneChangingAllowed = TernaryBoolUtil.ToTernaryBool(value);
		}

		public void SetEnteringBlockedJunctionAllowed(bool value) {
			enterWhenBlockedAllowed = TernaryBoolUtil.ToTernaryBool(value);
		}

		public void SetPedestrianCrossingAllowed(bool value) {
			pedestrianCrossingAllowed = TernaryBoolUtil.ToTernaryBool(value);
		}

		public bool IsDefault() {
			bool uturnIsDefault = uturnAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(uturnAllowed) == Options.allowUTurns;
			bool straightChangeIsDefault = straightLaneChangingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(straightLaneChangingAllowed) == Options.allowLaneChangesWhileGoingStraight;
			bool enterWhenBlockedIsDefault = enterWhenBlockedAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(enterWhenBlockedAllowed) == Options.allowEnterBlockedJunctions;
			bool pedCrossingIsDefault = pedestrianCrossingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(pedestrianCrossingAllowed);

			return uturnIsDefault && straightChangeIsDefault && enterWhenBlockedIsDefault && pedCrossingIsDefault;
		}

		public void Reset() {
			uturnAllowed = TernaryBool.Undefined;
			straightLaneChangingAllowed = TernaryBool.Undefined;
			enterWhenBlockedAllowed = TernaryBool.Undefined;
			pedestrianCrossingAllowed = TernaryBool.Undefined;
		}

		public override string ToString() {
			return $"[SegmentEndFlags\n" +
				"\t" + $"uturnAllowed = {uturnAllowed}\n" +
				"\t" + $"straightLaneChangingAllowed = {straightLaneChangingAllowed}\n" +
				"\t" + $"enterWhenBlockedAllowed = {enterWhenBlockedAllowed}\n" +
				"\t" + $"pedestrianCrossingAllowed = {pedestrianCrossingAllowed}\n" +
				"SegmentEndFlags]";
		}
	}
}
