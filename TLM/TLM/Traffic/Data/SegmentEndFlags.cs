using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry.Impl;
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

		private bool defaultEnterWhenBlockedAllowed;
		private bool defaultUturnAllowed;
		//private bool defaultPedestrianCrossingAllowed;

		public void UpdateDefaults(SegmentEndGeometry segmentEndGeometry) {
			NodeGeometry nodeGeo = NodeGeometry.Get(segmentEndGeometry.NodeId());

			bool newDefaultEnterWhenBlockedAllowed = false;
			bool newDefaultUturnAllowed = false;
			//NetNode.Flags _nodeFlags = NetNode.Flags.None;
			Constants.ServiceFactory.NetService.ProcessNode(segmentEndGeometry.NodeId(), delegate (ushort nodeId, ref NetNode node) {
				//_nodeFlags = node.m_flags;
				int numOutgoing = 0;
				int numIncoming = 0;
				node.CountLanes(nodeId, 0, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, true, ref numOutgoing, ref numIncoming);
				newDefaultEnterWhenBlockedAllowed = numOutgoing == 1 || numIncoming == 1;

				if (Options.allowUTurns) {
					newDefaultUturnAllowed =
						(node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition | NetNode.Flags.Bend | NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None &&
						node.Info?.m_class?.m_service != ItemClass.Service.Beautification
					;
				} else {
					newDefaultUturnAllowed = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
				}
				return true;
			});
			defaultEnterWhenBlockedAllowed = newDefaultEnterWhenBlockedAllowed;
			defaultUturnAllowed = newDefaultUturnAllowed;
			//Log._Debug($"SegmentEndFlags.UpdateDefaults: this={this} _nodeFlags={_nodeFlags} defaultEnterWhenBlockedAllowed={defaultEnterWhenBlockedAllowed}");
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
			return Options.allowLaneChangesWhileGoingStraight;
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
			return defaultEnterWhenBlockedAllowed || Options.allowEnterBlockedJunctions;
		}

		public bool IsPedestrianCrossingAllowed() {
			if (pedestrianCrossingAllowed == TernaryBool.Undefined) {
				return GetDefaultPedestrianCrossingAllowed();
			}

			return TernaryBoolUtil.ToBool(pedestrianCrossingAllowed);
		}

		public bool GetDefaultPedestrianCrossingAllowed() {
			return true;
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
