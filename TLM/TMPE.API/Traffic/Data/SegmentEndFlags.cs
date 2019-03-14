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
#if TURNONRED
        public TernaryBool turnOnRedAllowed;
#endif
        public TernaryBool straightLaneChangingAllowed;
		public TernaryBool enterWhenBlockedAllowed;
		public TernaryBool pedestrianCrossingAllowed;

		public bool defaultUturnAllowed;
#if TURNONRED
        public bool defaultTurnOnRedAllowed;
#endif
		public bool defaultStraightLaneChangingAllowed;
		public bool defaultEnterWhenBlockedAllowed;
		public bool defaultPedestrianCrossingAllowed;

		/*public void UpdateDefaults(ushort segmentId, bool startNode, ref NetNode node) {
			IJunctionRestrictionsManager junctionRestrictionsManager = Constants.ManagerFactory.JunctionRestrictionsManager;

			if (! junctionRestrictionsManager.IsUturnAllowedConfigurable(segmentId, startNode, ref node)) {
				uturnAllowed = TernaryBool.Undefined;
			}

#if TURNONRED
            if (! junctionRestrictionsManager.IsTurnOnRedAllowedConfigurable(segmentId, startNode, ref node)) {
                turnOnRedAllowed = TernaryBool.Undefined;
            }
#endif

			if (! junctionRestrictionsManager.IsLaneChangingAllowedWhenGoingStraightConfigurable(segmentId, startNode, ref node)) {
				straightLaneChangingAllowed = TernaryBool.Undefined;
			}

			if (! junctionRestrictionsManager.IsEnteringBlockedJunctionAllowedConfigurable(segmentId, startNode, ref node)) {
				enterWhenBlockedAllowed = TernaryBool.Undefined;
			}

			if (! junctionRestrictionsManager.IsPedestrianCrossingAllowedConfigurable(segmentId, startNode, ref node)) {
				pedestrianCrossingAllowed = TernaryBool.Undefined;
			}

			defaultUturnAllowed = junctionRestrictionsManager.GetDefaultUturnAllowed(segmentId, startNode, ref node);
#if TURNONRED
			defaultTurnOnRedAllowed = junctionRestrictionsManager.GetDefaultTurnOnRedAllowed(segmentId, startNode, ref node);
#endif
			defaultStraightLaneChangingAllowed = junctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight(segmentId, startNode, ref node);
			defaultEnterWhenBlockedAllowed = junctionRestrictionsManager.GetDefaultEnteringBlockedJunctionAllowed(segmentId, startNode, ref node);
			defaultPedestrianCrossingAllowed = junctionRestrictionsManager.GetDefaultPedestrianCrossingAllowed(segmentId, startNode, ref node);
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[11])
				Log._Debug($"SegmentEndFlags.UpdateDefaults({segmentId}, {startNode}): Set defaults: defaultUturnAllowed={defaultUturnAllowed}"
#if TURNONRED
					+ $", defaultTurnOnRedAllowed={defaultTurnOnRedAllowed}"
#endif
					+ $", defaultStraightLaneChangingAllowed={defaultStraightLaneChangingAllowed}, defaultEnterWhenBlockedAllowed={defaultEnterWhenBlockedAllowed}, defaultPedestrianCrossingAllowed={defaultPedestrianCrossingAllowed}");
#endif
		}*/

		public bool IsUturnAllowed() {
			if (uturnAllowed == TernaryBool.Undefined) {
				return defaultUturnAllowed;
			}

			return TernaryBoolUtil.ToBool(uturnAllowed);
		}

#if TURNONRED
		public bool IsTurnOnRedAllowed() {
			if (turnOnRedAllowed == TernaryBool.Undefined) {
				return defaultTurnOnRedAllowed;
			}

			return TernaryBoolUtil.ToBool(turnOnRedAllowed);
		}
#endif

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

#if TURNONRED
		public void SetTurnOnRedAllowed(bool value) {
			turnOnRedAllowed = TernaryBoolUtil.ToTernaryBool(value);
		}
#endif

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
#if TURNONRED
            bool turnOnRedIsDefault = turnOnRedAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(turnOnRedAllowed) == defaultTurnOnRedAllowed;
#endif
			bool straightChangeIsDefault = straightLaneChangingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(straightLaneChangingAllowed) == defaultStraightLaneChangingAllowed;
			bool enterWhenBlockedIsDefault = enterWhenBlockedAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(enterWhenBlockedAllowed) == defaultEnterWhenBlockedAllowed;
			bool pedCrossingIsDefault = pedestrianCrossingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(pedestrianCrossingAllowed) == defaultPedestrianCrossingAllowed;

			return uturnIsDefault
#if TURNONRED
				&& turnOnRedIsDefault
#endif
				&& straightChangeIsDefault
				&& enterWhenBlockedIsDefault
				&& pedCrossingIsDefault;
		}

		public void Reset(bool resetDefaults=true) {
			uturnAllowed = TernaryBool.Undefined;
#if TURNONRED
            turnOnRedAllowed = TernaryBool.Undefined;
#endif
			straightLaneChangingAllowed = TernaryBool.Undefined;
			enterWhenBlockedAllowed = TernaryBool.Undefined;
			pedestrianCrossingAllowed = TernaryBool.Undefined;

			if (resetDefaults) {
				defaultUturnAllowed = false;
#if TURNONRED
                defaultTurnOnRedAllowed = false;
#endif
				defaultStraightLaneChangingAllowed = false;
				defaultEnterWhenBlockedAllowed = false;
				defaultPedestrianCrossingAllowed = false;
			}
		}

		public override string ToString() {
			return $"[SegmentEndFlags\n" +
				"\t" + $"uturnAllowed = {uturnAllowed}\n" +
#if TURNONRED
                "\t" + $"turnOnRedAllowed = {turnOnRedAllowed}\n" +
#endif
				"\t" + $"straightLaneChangingAllowed = {straightLaneChangingAllowed}\n" +
				"\t" + $"enterWhenBlockedAllowed = {enterWhenBlockedAllowed}\n" +
				"\t" + $"pedestrianCrossingAllowed = {pedestrianCrossingAllowed}\n" +
				"SegmentEndFlags]";
		}
	}
}
