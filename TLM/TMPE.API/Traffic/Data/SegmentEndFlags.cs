using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Data {
	/// <summary>
	/// Segment end flags store junction restrictions
	/// </summary>
	public struct SegmentEndFlags {
		public TernaryBool uturnAllowed;
		public TernaryBool straightLaneChangingAllowed;
		public TernaryBool enterWhenBlockedAllowed;
		public TernaryBool pedestrianCrossingAllowed;

		public bool defaultUturnAllowed;
		public bool defaultEnterWhenBlockedAllowed;
		public bool defaultStraightLaneChangingAllowed;
		public bool defaultPedestrianCrossingAllowed;

		public void SetDefaults(bool defaultUturnAllowed, bool defaultStraightLaneChangingAllowed, bool defaultEnterWhenBlockedAllowed, bool defaultPedestrianCrossingAllowed) {
			this.defaultUturnAllowed = defaultUturnAllowed;
			this.defaultStraightLaneChangingAllowed = defaultStraightLaneChangingAllowed;
			this.defaultEnterWhenBlockedAllowed = defaultEnterWhenBlockedAllowed;
			this.defaultPedestrianCrossingAllowed = defaultPedestrianCrossingAllowed;
		}

		public bool IsUturnAllowed() {
			if (uturnAllowed == TernaryBool.Undefined) {
				return GetDefaultUturnAllowed();
			}

			return TernaryBoolUtil.ToBool(uturnAllowed);
		}

		public bool GetDefaultUturnAllowed() {
			return defaultUturnAllowed;
		}

		public bool IsLaneChangingAllowedWhenGoingStraight() {
			if (straightLaneChangingAllowed == TernaryBool.Undefined) {
				return GetDefaultLaneChangingAllowedWhenGoingStraight();
			}

			return TernaryBoolUtil.ToBool(straightLaneChangingAllowed);
		}

		public bool GetDefaultLaneChangingAllowedWhenGoingStraight() {
			return defaultStraightLaneChangingAllowed;
		}

		public bool IsEnteringBlockedJunctionAllowed() {
			//Log._Debug($"SegmentEndFlags.IsEnteringBlockedJunctionAllowed: this={this} enterWhenBlockedAllowed={enterWhenBlockedAllowed}");
			if (enterWhenBlockedAllowed == TernaryBool.Undefined) {
				//Log._Debug($"SegmentEndFlags.IsEnteringBlockedJunctionAllowed: returning default: {GetDefaultEnteringBlockedJunctionAllowed()}");
				return GetDefaultEnteringBlockedJunctionAllowed();
			}

			//Log._Debug($"SegmentEndFlags.IsEnteringBlockedJunctionAllowed: returning custom: {TernaryBoolUtil.ToBool(enterWhenBlockedAllowed)}");
			return TernaryBoolUtil.ToBool(enterWhenBlockedAllowed);
		}

		public bool GetDefaultEnteringBlockedJunctionAllowed() {
			return defaultEnterWhenBlockedAllowed;
		}

		public bool IsPedestrianCrossingAllowed() {
			if (pedestrianCrossingAllowed == TernaryBool.Undefined) {
				return GetDefaultPedestrianCrossingAllowed();
			}

			return TernaryBoolUtil.ToBool(pedestrianCrossingAllowed);
		}

		public bool GetDefaultPedestrianCrossingAllowed() {
			return defaultPedestrianCrossingAllowed;
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
			bool uturnIsDefault = uturnAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(uturnAllowed) == GetDefaultUturnAllowed();
			bool straightChangeIsDefault = straightLaneChangingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(straightLaneChangingAllowed) == GetDefaultLaneChangingAllowedWhenGoingStraight();
			bool enterWhenBlockedIsDefault = enterWhenBlockedAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(enterWhenBlockedAllowed) == GetDefaultEnteringBlockedJunctionAllowed();
			bool pedCrossingIsDefault = pedestrianCrossingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(pedestrianCrossingAllowed) == GetDefaultPedestrianCrossingAllowed();

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
