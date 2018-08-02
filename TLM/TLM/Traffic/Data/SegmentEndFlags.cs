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
		public TernaryBool straightLaneChangingAllowed;
		public TernaryBool enterWhenBlockedAllowed;
		public TernaryBool pedestrianCrossingAllowed;

		bool defaultUturnAllowed;
		bool defaultStraightLaneChangingAllowed;
		bool defaultEnterWhenBlockedAllowed;
		bool defaultPedestrianCrossingAllowed;

		public void UpdateDefaults(SegmentEndGeometry endGeo) {
			IJunctionRestrictionsManager junctionRestrictionsManager = Constants.ManagerFactory.JunctionRestrictionsManager;
			defaultUturnAllowed = junctionRestrictionsManager.GetDefaultUturnAllowed(endGeo.SegmentId, endGeo.StartNode);
			defaultStraightLaneChangingAllowed = junctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight(endGeo.SegmentId, endGeo.StartNode);
			defaultEnterWhenBlockedAllowed = junctionRestrictionsManager.GetDefaultEnteringBlockedJunctionAllowed(endGeo.SegmentId, endGeo.StartNode);
			defaultPedestrianCrossingAllowed = junctionRestrictionsManager.GetDefaultPedestrianCrossingAllowed(endGeo.SegmentId, endGeo.StartNode);
		}

		public bool IsUturnAllowed() {
			if (uturnAllowed == TernaryBool.Undefined) {
				return defaultUturnAllowed;
			}

			return TernaryBoolUtil.ToBool(uturnAllowed);
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
			bool straightChangeIsDefault = straightLaneChangingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(straightLaneChangingAllowed) == defaultStraightLaneChangingAllowed;
			bool enterWhenBlockedIsDefault = enterWhenBlockedAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(enterWhenBlockedAllowed) == defaultEnterWhenBlockedAllowed;
			bool pedCrossingIsDefault = pedestrianCrossingAllowed == TernaryBool.Undefined || TernaryBoolUtil.ToBool(pedestrianCrossingAllowed) == defaultPedestrianCrossingAllowed;

			return uturnIsDefault && straightChangeIsDefault && enterWhenBlockedIsDefault && pedCrossingIsDefault;
		}

		public void Reset(bool resetDefaults=true) {
			uturnAllowed = TernaryBool.Undefined;
			straightLaneChangingAllowed = TernaryBool.Undefined;
			enterWhenBlockedAllowed = TernaryBool.Undefined;
			pedestrianCrossingAllowed = TernaryBool.Undefined;

			if (resetDefaults) {
				defaultUturnAllowed = false;
				defaultStraightLaneChangingAllowed = false;
				defaultEnterWhenBlockedAllowed = false;
				defaultPedestrianCrossingAllowed = false;
			}
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
