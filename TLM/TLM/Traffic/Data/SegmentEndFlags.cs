using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry.Impl;
using TrafficManager.Manager;
using TrafficManager.State;

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

		bool defaultUturnAllowed;
        bool defaultNearTurnOnRedAllowed;
		bool defaultFarTurnOnRedAllowed;
		bool defaultStraightLaneChangingAllowed;
		bool defaultEnterWhenBlockedAllowed;
		bool defaultPedestrianCrossingAllowed;

		public void UpdateDefaults(ushort segmentId, bool startNode, ref NetNode node) {
			IJunctionRestrictionsManager junctionRestrictionsManager = Constants.ManagerFactory.JunctionRestrictionsManager;

			if (! junctionRestrictionsManager.IsUturnAllowedConfigurable(segmentId, startNode, ref node)) {
				uturnAllowed = TernaryBool.Undefined;
			}

            if (! junctionRestrictionsManager.IsNearTurnOnRedAllowedConfigurable(segmentId, startNode, ref node)) {
                nearTurnOnRedAllowed = TernaryBool.Undefined;
            }

			if (!junctionRestrictionsManager.IsFarTurnOnRedAllowedConfigurable(segmentId, startNode, ref node)) {
				farTurnOnRedAllowed = TernaryBool.Undefined;
			}

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
			defaultNearTurnOnRedAllowed = junctionRestrictionsManager.GetDefaultNearTurnOnRedAllowed(segmentId, startNode, ref node);
			defaultFarTurnOnRedAllowed = junctionRestrictionsManager.GetDefaultFarTurnOnRedAllowed(segmentId, startNode, ref node);
			defaultStraightLaneChangingAllowed = junctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight(segmentId, startNode, ref node);
			defaultEnterWhenBlockedAllowed = junctionRestrictionsManager.GetDefaultEnteringBlockedJunctionAllowed(segmentId, startNode, ref node);
			defaultPedestrianCrossingAllowed = junctionRestrictionsManager.GetDefaultPedestrianCrossingAllowed(segmentId, startNode, ref node);
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[11])
				Log._Debug($"SegmentEndFlags.UpdateDefaults({segmentId}, {startNode}): Set defaults: defaultUturnAllowed={defaultUturnAllowed}, defaultNearTurnOnRedAllowed={defaultNearTurnOnRedAllowed}, defaultFarTurnOnRedAllowed={defaultFarTurnOnRedAllowed}, defaultStraightLaneChangingAllowed={defaultStraightLaneChangingAllowed}, defaultEnterWhenBlockedAllowed={defaultEnterWhenBlockedAllowed}, defaultPedestrianCrossingAllowed={defaultPedestrianCrossingAllowed}");
#endif
		}

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
			if (nearTurnOnRedAllowed == TernaryBool.Undefined) {
				return defaultFarTurnOnRedAllowed;
			}

			return TernaryBoolUtil.ToBool(nearTurnOnRedAllowed);
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
