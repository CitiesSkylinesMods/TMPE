#define DEBUGVISUALSx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Custom.AI;
using CSUtil.Commons;
using TrafficManager.State;
using TrafficManager.Geometry.Impl;
using TrafficManager.Traffic.Enums;
using TrafficManager.Manager;
using TrafficManager.Traffic.Data;

namespace TrafficManager.TrafficLight.Impl {
	/// <summary>
	/// Represents the traffic light (left, forward, right) at a specific segment end
	/// </summary>
	public class CustomSegmentLight : ICustomSegmentLight {
		[Obsolete]
		public ushort NodeId {
			get {
				return lights.NodeId;
			}
		}

		public ushort SegmentId {
			get {
				return lights.SegmentId;
			}
		}

		public bool StartNode {
			get {
				return lights.StartNode;
			}
		}

		public LightMode CurrentMode {
			get { return InternalCurrentMode; }
			set {
				if (InternalCurrentMode == value)
					return;

				InternalCurrentMode = value;
				EnsureModeLights();
			}
		}
		public LightMode InternalCurrentMode { get; set; } = LightMode.Simple; // TODO should be private

		internal RoadBaseAI.TrafficLightState leftLight;
		internal RoadBaseAI.TrafficLightState mainLight;
		internal RoadBaseAI.TrafficLightState rightLight;

		public RoadBaseAI.TrafficLightState LightLeft {
			get { return leftLight; }
			/*private set {
				if (leftLight == value)
					return;

				leftLight = value;
				lights.OnChange();
			}*/
		}

		public RoadBaseAI.TrafficLightState LightMain {
			get { return mainLight; }
			/*private set {
				if (mainLight == value)
					return;

				mainLight = value;
				lights.OnChange();
			}*/
		}
		public RoadBaseAI.TrafficLightState LightRight {
			get { return rightLight; }
			/*private set {
				if (rightLight == value)
					return;

				rightLight = value;
				lights.OnChange();
			}*/
		}

		CustomSegmentLights lights;

		public override string ToString() {
			return $"[CustomSegmentLight seg. {SegmentId} @ node {NodeId}\n" +
			"\t" + $"CurrentMode: {CurrentMode}\n" +
			"\t" + $"LightLeft: {LightLeft}\n" +
			"\t" + $"LightMain: {LightMain}\n" +
			"\t" + $"LightRight: {LightRight}\n" +
			"CustomSegmentLight]";
		}

		private void EnsureModeLights() {
			bool changed = false;

			switch (InternalCurrentMode) {
				case LightMode.Simple:
					if (leftLight != LightMain) {
						leftLight = LightMain;
						changed = true;
					}
					if (rightLight != LightMain) {
						rightLight = LightMain;
						changed = true;
					}
					break;
				case LightMode.SingleLeft:
					if (rightLight != LightMain) {
						rightLight = LightMain;
						changed = true;
					}
					break;
				case LightMode.SingleRight:
					if (leftLight != LightMain) {
						leftLight = LightMain;
						changed = true;
					}
					break;
			}

			if (changed)
				lights.OnChange();
		}

		public CustomSegmentLight(CustomSegmentLights lights, RoadBaseAI.TrafficLightState mainLight) {
			this.lights = lights;

			SetStates(mainLight, leftLight, rightLight);
			UpdateVisuals();
		}

		public CustomSegmentLight(CustomSegmentLights lights, RoadBaseAI.TrafficLightState mainLight, RoadBaseAI.TrafficLightState leftLight, RoadBaseAI.TrafficLightState rightLight/*, RoadBaseAI.TrafficLightState pedestrianLight*/) {
			this.lights = lights;

			SetStates(mainLight, leftLight, rightLight);

			UpdateVisuals();
		}

		public void ToggleMode() {
			if (!Constants.ServiceFactory.NetService.IsSegmentValid(SegmentId)) {
				Log.Error($"CustomSegmentLight.ToggleMode: Segment {SegmentId} is invalid.");
				return;
			}

			IExtSegmentEndManager extSegMan = Constants.ManagerFactory.ExtSegmentEndManager;
			Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
				ToggleMode(ref extSegMan.ExtSegmentEnds[extSegMan.GetIndex(SegmentId, StartNode)], ref node);
				return true;
			});
		}

		private void ToggleMode(ref ExtSegmentEnd segEnd, ref NetNode node) {
			IExtSegmentManager extSegMan = Constants.ManagerFactory.ExtSegmentManager;
			IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
			bool startNode = lights.StartNode;

			bool hasLeftSegment;
			bool hasForwardSegment;
			bool hasRightSegment;
			extSegEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out hasLeftSegment, out hasForwardSegment, out hasRightSegment);
				
#if DEBUG
			Log._Debug($"ChangeMode. segment {SegmentId} @ node {NodeId}, hasLeftSegment={hasLeftSegment}, hasForwardSegment={hasForwardSegment}, hasRightSegment={hasRightSegment}");
#endif

			LightMode newMode = LightMode.Simple;
			if (CurrentMode == LightMode.Simple) {
				if (!hasLeftSegment) {
					newMode = LightMode.SingleRight;
				} else {
					newMode = LightMode.SingleLeft;
				}
			} else if (CurrentMode == LightMode.SingleLeft) {
				if (!hasForwardSegment || !hasRightSegment) {
					newMode = LightMode.Simple;
				} else {
					newMode = LightMode.SingleRight;
				}
			} else if (CurrentMode == LightMode.SingleRight) {
				if (!hasLeftSegment) {
					newMode = LightMode.Simple;
				} else {
					newMode = LightMode.All;
				}
			} else {
				newMode = LightMode.Simple;
			}

			CurrentMode = newMode;
		}

		public void ChangeMainLight() {
			var invertedLight = LightMain == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			if (CurrentMode == LightMode.Simple) {
				SetStates(invertedLight, invertedLight, invertedLight);
			} else if (CurrentMode == LightMode.SingleLeft) {
				SetStates(invertedLight, null, invertedLight);
			} else if (CurrentMode == LightMode.SingleRight) {
				SetStates(invertedLight, invertedLight, null);
			} else {
				//LightMain = invertedLight;
				SetStates(invertedLight, null, null);
			}

			UpdateVisuals();
		}

		public void ChangeLeftLight() {
			var invertedLight = LightLeft == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			//LightLeft = invertedLight;
			SetStates(null, invertedLight, null);

			UpdateVisuals();
		}

		public void ChangeRightLight() {
			var invertedLight = LightRight == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			//LightRight = invertedLight;
			SetStates(null, null, invertedLight);

			UpdateVisuals();
		}

		public RoadBaseAI.TrafficLightState GetLightState(ushort toSegmentId) {
			IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
			ArrowDirection dir = segEndMan.GetDirection(ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(SegmentId, StartNode)], toSegmentId);
			return GetLightState(dir);
		}

		public RoadBaseAI.TrafficLightState GetLightState(ArrowDirection dir) {
			switch (dir) {
				case ArrowDirection.Left:
					return LightLeft;
				case ArrowDirection.Forward:
				default:
					return LightMain;
				case ArrowDirection.Right:
					return LightRight;
				case ArrowDirection.Turn:
					return Constants.ServiceFactory.SimulationService.LeftHandDrive ? LightRight : LightLeft;
			}
		}

		public bool IsGreen(ArrowDirection dir) {
			return GetLightState(dir) == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsInTransition(ArrowDirection dir) {
			RoadBaseAI.TrafficLightState state = GetLightState(dir);
			return state == RoadBaseAI.TrafficLightState.GreenToRed || state == RoadBaseAI.TrafficLightState.RedToGreen;
		}

		public bool IsRed(ArrowDirection dir) {
			return GetLightState(dir) == RoadBaseAI.TrafficLightState.Red;
		}

		public bool IsAnyGreen() {
			return LightMain == RoadBaseAI.TrafficLightState.Green ||
				LightLeft == RoadBaseAI.TrafficLightState.Green ||
				LightRight == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsAnyInTransition() {
			return LightMain == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightLeft == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightRight == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightMain == RoadBaseAI.TrafficLightState.GreenToRed ||
				LightLeft == RoadBaseAI.TrafficLightState.GreenToRed ||
				LightRight == RoadBaseAI.TrafficLightState.GreenToRed;
		}

		public bool IsLeftGreen() {
			return LightLeft == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsMainGreen() {
			return LightMain == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsRightGreen() {
			return LightRight == RoadBaseAI.TrafficLightState.Green;
		}

		public bool IsLeftRed() {
			return LightLeft == RoadBaseAI.TrafficLightState.Red;
		}

		public bool IsMainRed() {
			return LightMain == RoadBaseAI.TrafficLightState.Red;
		}

		public bool IsRightRed() {
			return LightRight == RoadBaseAI.TrafficLightState.Red;
		}

		public void UpdateVisuals() {
			var instance = Singleton<NetManager>.instance;

			ushort nodeId = lights.NodeId;
			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			uint simGroup = (uint)nodeId >> 7;

			RoadBaseAI.TrafficLightState vehicleLightState;
			RoadBaseAI.TrafficLightState pedestrianLightState;

			RoadBaseAI.TrafficLightState mainLight = LightMain;
			RoadBaseAI.TrafficLightState leftLight = LightLeft;
			RoadBaseAI.TrafficLightState rightLight = LightRight;

			switch (CurrentMode) {
				case LightMode.Simple:
					leftLight = mainLight;
					rightLight = mainLight;
					break;
				case LightMode.SingleLeft:
					rightLight = mainLight;
					break;
				case LightMode.SingleRight:
					leftLight = mainLight;
					break;
				case LightMode.All:
				default:
					break;
			}

			vehicleLightState = GetVisualLightState();
			pedestrianLightState = lights.PedestrianLightState == null ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)lights.PedestrianLightState;

#if DEBUGVISUALS
			Log._Debug($"Setting visual traffic light state of node {NodeId}, seg. {SegmentId} to vehicleState={vehicleLightState} pedState={pedestrianLightState}");
#endif

			uint now = ((currentFrameIndex - simGroup) >> 8) & 1;
			Constants.ManagerFactory.TrafficLightSimulationManager.SetVisualState(nodeId, ref instance.m_segments.m_buffer[SegmentId], now << 8, vehicleLightState, pedestrianLightState, false, false);
			Constants.ManagerFactory.TrafficLightSimulationManager.SetVisualState(nodeId, ref instance.m_segments.m_buffer[SegmentId], (1u - now) << 8, vehicleLightState, pedestrianLightState, false, false);
		}

		public RoadBaseAI.TrafficLightState GetVisualLightState() {
			RoadBaseAI.TrafficLightState vehicleLightState;
			// any green?
			if (LightMain == RoadBaseAI.TrafficLightState.Green ||
				LightLeft == RoadBaseAI.TrafficLightState.Green ||
				LightRight == RoadBaseAI.TrafficLightState.Green) {
				vehicleLightState = RoadBaseAI.TrafficLightState.Green;
			} else // all red?
			if (LightMain == RoadBaseAI.TrafficLightState.Red &&
				LightLeft == RoadBaseAI.TrafficLightState.Red &&
				LightRight == RoadBaseAI.TrafficLightState.Red) {
				vehicleLightState = RoadBaseAI.TrafficLightState.Red;
			} else // any red+yellow?
			if (LightMain == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightLeft == RoadBaseAI.TrafficLightState.RedToGreen ||
				LightRight == RoadBaseAI.TrafficLightState.RedToGreen) {
				vehicleLightState = RoadBaseAI.TrafficLightState.RedToGreen;
			} else {
				vehicleLightState = RoadBaseAI.TrafficLightState.GreenToRed;
			}

			return vehicleLightState;
		}

		private RoadBaseAI.TrafficLightState _checkPedestrianLight() {
			if (LightLeft == RoadBaseAI.TrafficLightState.Red && LightMain == RoadBaseAI.TrafficLightState.Red &&
				LightRight == RoadBaseAI.TrafficLightState.Red) {
				return RoadBaseAI.TrafficLightState.Green;
			}
			return RoadBaseAI.TrafficLightState.Red;
		}

		public object Clone() {
			return MemberwiseClone();
		}

		public void MakeRedOrGreen() {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == NodeId)
				Log._Debug($"CustomSegmentLight.MakeRedOrGreen: called for segment {SegmentId} @ {NodeId}");
#endif

			RoadBaseAI.TrafficLightState mainState = RoadBaseAI.TrafficLightState.Green;
			RoadBaseAI.TrafficLightState leftState = RoadBaseAI.TrafficLightState.Green;
			RoadBaseAI.TrafficLightState rightState = RoadBaseAI.TrafficLightState.Green;

			if (LightLeft != RoadBaseAI.TrafficLightState.Green) {
				leftState = RoadBaseAI.TrafficLightState.Red;
			}

			if (LightMain != RoadBaseAI.TrafficLightState.Green) {
				mainState = RoadBaseAI.TrafficLightState.Red;
			}

			if (LightRight != RoadBaseAI.TrafficLightState.Green) {
				rightState = RoadBaseAI.TrafficLightState.Red;
			}

			SetStates(mainState, leftState, rightState);
		}

		public void MakeRed() {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == NodeId)
				Log._Debug($"CustomSegmentLight.MakeRed: called for segment {SegmentId} @ {NodeId}");
#endif

			SetStates(RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState.Red, RoadBaseAI.TrafficLightState.Red);
		}

		public void SetStates(RoadBaseAI.TrafficLightState? mainLight, RoadBaseAI.TrafficLightState? leftLight, RoadBaseAI.TrafficLightState? rightLight, bool calcAutoPedLight=true) {
			if ((mainLight == null || this.mainLight == mainLight) &&
				(leftLight == null || this.leftLight == leftLight) &&
				(rightLight == null || this.rightLight == rightLight))
				return;

			if (mainLight != null)
				this.mainLight = (RoadBaseAI.TrafficLightState)mainLight;
			if (leftLight != null)
				this.leftLight = (RoadBaseAI.TrafficLightState)leftLight;
			if (rightLight != null)
				this.rightLight = (RoadBaseAI.TrafficLightState)rightLight;

#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == NodeId)
				Log._Debug($"CustomSegmentLight.SetStates({mainLight}, {leftLight}, {rightLight}, {calcAutoPedLight}) for segment {SegmentId} @ {NodeId}: {this.mainLight} {this.leftLight} {this.rightLight}");
#endif

			lights.OnChange(calcAutoPedLight);
		}
	}
}
