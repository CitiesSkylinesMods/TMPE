using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;

namespace TrafficManager.Traffic.Data {
	/// <summary>
	/// Segment end flags store junction restrictions
	/// </summary>
	public struct SegmentEndFlags {
		public TernaryBool uturnAllowed;
        public TernaryBool nearTurnOnRedAllowed;
		public TernaryBool farTurnOnRedAllowed;
		public TernaryBool straightLaneChangingAllowed;
		public TernaryBool enterWhenBlockedAllowed;
		public TernaryBool pedestrianCrossingAllowed;

		public bool defaultUturnAllowed;
		public bool defaultNearTurnOnRedAllowed;
		public bool defaultFarTurnOnRedAllowed;
		public bool defaultStraightLaneChangingAllowed;
		public bool defaultEnterWhenBlockedAllowed;
		public bool defaultPedestrianCrossingAllowed;

		public bool IsUturnAllowed() {
			if (uturnAllowed == TernaryBool.Undefined) {
				return defaultUturnAllowed;
			}

			return TernaryBoolUtil.ToBool(uturnAllowed);
		}

		public bool IsNearTurnOnRedAllowed() {
			if (nearTurnOnRedAllowed == TernaryBool.Undefined) {
				return defaultNearTurnOnRedAllowed;
			}

			return TernaryBoolUtil.ToBool(nearTurnOnRedAllowed);
		}

		public bool IsFarTurnOnRedAllowed() {
			if (farTurnOnRedAllowed == TernaryBool.Undefined) {
				return defaultFarTurnOnRedAllowed;
			}

			return TernaryBoolUtil.ToBool(farTurnOnRedAllowed);
		}

		public bool IsLaneChangingAllowedWhenGoingStraight() {
			if (straightLaneChangingAllowed == TernaryBool.Undefined) {
				return defaultStraightLaneChangingAllowed;
			}

			return TernaryBoolUtil.ToBool(straightLaneChangingAllowed);
		}

		public bool IsEnteringBlockedJunctionAllowed() {
			if (enterWhenBlockedAllowed == TernaryBool.Undefined) {
				return defaultEnterWhenBlockedAllowed;
			}

			return TernaryBoolUtil.ToBool(enterWhenBlockedAllowed);
		}
		
		public bool IsPedestrianCrossingAllowed() {
			if (pedestrianCrossingAllowed == TernaryBool.Undefined) {
				return defaultPedestrianCrossingAllowed;
			}

			return TernaryBoolUtil.ToBool(pedestrianCrossingAllowed);
		}

		public void SetUturnAllowed(bool value) {
			uturnAllowed = TernaryBoolUtil.ToTernaryBool(value);
		}

		public void SetNearTurnOnRedAllowed(bool value) {
			nearTurnOnRedAllowed = TernaryBoolUtil.ToTernaryBool(value);
		}

		public void SetFarTurnOnRedAllowed(bool value) {
			farTurnOnRedAllowed = TernaryBoolUtil.ToTernaryBool(value);
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
			bool uturnIsDefault = uturnAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(uturnAllowed) == defaultUturnAllowed;
            bool nearTurnOnRedIsDefault = nearTurnOnRedAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(nearTurnOnRedAllowed) == defaultNearTurnOnRedAllowed;
			bool farTurnOnRedIsDefault = farTurnOnRedAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(farTurnOnRedAllowed) == defaultFarTurnOnRedAllowed;
			bool straightChangeIsDefault = straightLaneChangingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(straightLaneChangingAllowed) == defaultStraightLaneChangingAllowed;
			bool enterWhenBlockedIsDefault = enterWhenBlockedAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(enterWhenBlockedAllowed) == defaultEnterWhenBlockedAllowed;
			bool pedCrossingIsDefault = pedestrianCrossingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(pedestrianCrossingAllowed) == defaultPedestrianCrossingAllowed;

			return uturnIsDefault && nearTurnOnRedIsDefault && farTurnOnRedIsDefault && straightChangeIsDefault && enterWhenBlockedIsDefault && pedCrossingIsDefault;
		}

		public void Reset(bool resetDefaults=true) {
			uturnAllowed = TernaryBool.Undefined;
            nearTurnOnRedAllowed = TernaryBool.Undefined;
			farTurnOnRedAllowed = TernaryBool.Undefined;
			straightLaneChangingAllowed = TernaryBool.Undefined;
			enterWhenBlockedAllowed = TernaryBool.Undefined;
			pedestrianCrossingAllowed = TernaryBool.Undefined;

			if (resetDefaults) {
				defaultUturnAllowed = false;
                defaultNearTurnOnRedAllowed = false;
				defaultFarTurnOnRedAllowed = false;
				defaultStraightLaneChangingAllowed = false;
				defaultEnterWhenBlockedAllowed = false;
				defaultPedestrianCrossingAllowed = false;
			}
		}

		public override string ToString() {
			return $"[SegmentEndFlags\n" +
				"\t" + $"uturnAllowed = {uturnAllowed}\n" +
                "\t" + $"nearTurnOnRedAllowed = {nearTurnOnRedAllowed}\n" +
				"\t" + $"farTurnOnRedAllowed = {farTurnOnRedAllowed}\n" +
				"\t" + $"straightLaneChangingAllowed = {straightLaneChangingAllowed}\n" +
				"\t" + $"enterWhenBlockedAllowed = {enterWhenBlockedAllowed}\n" +
				"\t" + $"pedestrianCrossingAllowed = {pedestrianCrossingAllowed}\n" +
				"SegmentEndFlags]";
		}
	}
}
