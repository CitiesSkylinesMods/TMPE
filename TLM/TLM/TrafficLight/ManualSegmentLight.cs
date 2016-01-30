using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;
using UnityEngine;
using TrafficManager.Custom.AI;

namespace TrafficManager.TrafficLight {
	public class ManualSegmentLight : ICloneable {
		public enum Mode {
			Simple = 1, // <^>
			SingleLeft = 2, // <, ^>
			SingleRight = 3, // <^, >
			All = 4 // <, ^, >
		}

		public ushort nodeId;
		private ushort segmentId;

		public ushort SegmentId {
			get { return segmentId; }
			set { segmentId = value; }
		}

		public Mode CurrentMode = Mode.Simple;

		public RoadBaseAI.TrafficLightState LightLeft;
		public RoadBaseAI.TrafficLightState LightMain;
		public RoadBaseAI.TrafficLightState LightRight;
		public RoadBaseAI.TrafficLightState LightPedestrian;

		public uint LastChange;
		public uint LastChangeFrame;

		public bool PedestrianEnabled;

		public ManualSegmentLight(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState mainLight) {
			this.nodeId = nodeId;
			this.segmentId = segmentId;

			LightMain = mainLight;
			LightLeft = mainLight;
			LightRight = mainLight;
			LightPedestrian = mainLight == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			UpdateVisuals();
		}

		public ManualSegmentLight(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState mainLight, RoadBaseAI.TrafficLightState leftLight, RoadBaseAI.TrafficLightState rightLight, RoadBaseAI.TrafficLightState pedestrianLight) {
			this.nodeId = nodeId;
			this.segmentId = segmentId;

			LightMain = mainLight;
			LightLeft = leftLight;
			LightRight = rightLight;
			LightPedestrian = pedestrianLight;

			UpdateVisuals();
		}

		public RoadBaseAI.TrafficLightState GetLightMain() {
			return LightMain;
		}

		public RoadBaseAI.TrafficLightState GetLightLeft() {
			return LightLeft;
		}

		public RoadBaseAI.TrafficLightState GetLightRight() {
			return LightRight;
		}

		public RoadBaseAI.TrafficLightState GetLightPedestrian() {
			return LightPedestrian;
		}

		public void ChangeMode() {
			SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(segmentId);
			geometry.Recalculate(true, true);
			var hasLeftSegment = geometry.HasLeftSegment(nodeId);
			var hasForwardSegment = geometry.HasStraightSegment(nodeId);
			var hasRightSegment = geometry.HasRightSegment(nodeId);

			if (CurrentMode == Mode.Simple) {
				if (!hasLeftSegment) {
					CurrentMode = Mode.SingleRight;
				} else {
					CurrentMode = Mode.SingleLeft;
				}
			} else if (CurrentMode == Mode.SingleLeft) {
				if (!hasForwardSegment || !hasRightSegment) {
					CurrentMode = Mode.Simple;
				} else {
					CurrentMode = Mode.SingleRight;
				}
			} else if (CurrentMode == Mode.SingleRight) {
				if (!hasLeftSegment) {
					CurrentMode = Mode.Simple;
				} else {
					CurrentMode = Mode.All;
				}
			} else {
				CurrentMode = Mode.Simple;
			}

			if (CurrentMode == Mode.Simple) {
				LightLeft = LightMain;
				LightRight = LightMain;
				LightPedestrian = _checkPedestrianLight();
			}
		}
		
		public void ManualPedestrian() {
			PedestrianEnabled = !PedestrianEnabled;
		}

		public void ChangeLightMain() {
			var invertedLight = LightMain == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			if (CurrentMode == Mode.Simple) {
				LightLeft = invertedLight;
				LightRight = invertedLight;
				LightPedestrian = !PedestrianEnabled ? LightMain : LightPedestrian;
				LightMain = invertedLight;
			} else if (CurrentMode == Mode.SingleLeft) {
				LightRight = invertedLight;
				LightMain = invertedLight;
			} else if (CurrentMode == Mode.SingleRight) {
				LightLeft = invertedLight;
				LightMain = invertedLight;
			} else {
				LightMain = invertedLight;
			}

			if (!PedestrianEnabled) {
				LightPedestrian = _checkPedestrianLight();
			}

			UpdateVisuals();
		}

		public void ChangeLightLeft() {
			var invertedLight = LightLeft == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			LightLeft = invertedLight;

			if (!PedestrianEnabled) {
				LightPedestrian = _checkPedestrianLight();
			}

			UpdateVisuals();
		}

		public void ChangeLightRight() {
			var invertedLight = LightRight == RoadBaseAI.TrafficLightState.Green
				? RoadBaseAI.TrafficLightState.Red
				: RoadBaseAI.TrafficLightState.Green;

			LightRight = invertedLight;

			if (!PedestrianEnabled) {
				LightPedestrian = _checkPedestrianLight();
			}

			UpdateVisuals();
		}

		public bool isAnyGreen() {
			return LightMain == RoadBaseAI.TrafficLightState.Green ||
				LightLeft == RoadBaseAI.TrafficLightState.Green ||
				LightRight == RoadBaseAI.TrafficLightState.Green;
		}

		public bool isLeftGreen() {
			return LightLeft == RoadBaseAI.TrafficLightState.Green;
		}

		public bool isForwardGreen() {
			return LightMain == RoadBaseAI.TrafficLightState.Green;
		}

		public bool isRightGreen() {
			return LightRight == RoadBaseAI.TrafficLightState.Green;
		}

		public void ChangeLightPedestrian() {
			if (PedestrianEnabled) {
				var invertedLight = LightPedestrian == RoadBaseAI.TrafficLightState.Green
					? RoadBaseAI.TrafficLightState.Red
					: RoadBaseAI.TrafficLightState.Green;

				LightPedestrian = invertedLight;
				UpdateVisuals();
			}
		}

		/*RoadBaseAI.TrafficLightState lastVehicleLightState = RoadBaseAI.TrafficLightState.Red;
		RoadBaseAI.TrafficLightState lastPedestrianLightState = RoadBaseAI.TrafficLightState.Red;*/

		public void UpdateVisuals() {
			var instance = Singleton<NetManager>.instance;

			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			uint num = (uint)(((int)nodeId << 8) / 32768);

			LastChange = 0u;
			LastChangeFrame = currentFrameIndex >> 6;

			RoadBaseAI.TrafficLightState vehicleLightState;
			RoadBaseAI.TrafficLightState pedestrianLightState;

			RoadBaseAI.TrafficLightState mainLight = LightMain;
			RoadBaseAI.TrafficLightState leftLight = LightLeft;
			RoadBaseAI.TrafficLightState rightLight = LightRight;

			switch (CurrentMode) {
				case Mode.Simple:
					leftLight = mainLight;
					rightLight = mainLight;
					break;
				case Mode.SingleLeft:
					rightLight = mainLight;
					break;
				case Mode.SingleRight:
					leftLight = mainLight;
					break;
				case Mode.All:
				default:
					break;
			}

			// any green?
			if (mainLight == RoadBaseAI.TrafficLightState.Green ||
				leftLight == RoadBaseAI.TrafficLightState.Green ||
				rightLight == RoadBaseAI.TrafficLightState.Green) {
				vehicleLightState = RoadBaseAI.TrafficLightState.Green;
			} else // all red?
			if (mainLight == RoadBaseAI.TrafficLightState.Red &&
				leftLight == RoadBaseAI.TrafficLightState.Red &&
				rightLight == RoadBaseAI.TrafficLightState.Red) {
				vehicleLightState = RoadBaseAI.TrafficLightState.Red;
			} else // any red+yellow?
			if (mainLight == RoadBaseAI.TrafficLightState.RedToGreen ||
				leftLight == RoadBaseAI.TrafficLightState.RedToGreen ||
				rightLight == RoadBaseAI.TrafficLightState.RedToGreen) {
				vehicleLightState = RoadBaseAI.TrafficLightState.RedToGreen;
			} else {
				vehicleLightState = RoadBaseAI.TrafficLightState.GreenToRed;
			}

			pedestrianLightState = LightPedestrian;

			uint now = ((currentFrameIndex - num) >> 8) & 1;
			RoadBaseAI.SetTrafficLightState(nodeId, ref instance.m_segments.m_buffer[segmentId], now << 8, vehicleLightState, pedestrianLightState, false, false);
			RoadBaseAI.SetTrafficLightState(nodeId, ref instance.m_segments.m_buffer[segmentId], (1u-now) << 8, vehicleLightState, pedestrianLightState, false, false);

			/*if (vehicleLightState != lastVehicleLightState || pedestrianLightState != lastPedestrianLightState) {
				// force rendering
				Singleton<NetManager>.instance.UpdateSegmentRenderer(segmentId, true);
			}

			lastVehicleLightState = vehicleLightState;
			lastPedestrianLightState = pedestrianLightState;*/
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

		public void invert() {
			LightMain = invertLight(LightMain);
			LightLeft = invertLight(LightLeft);
			LightRight = invertLight(LightRight);
			LightPedestrian = invertLight(LightPedestrian);
		}

		private RoadBaseAI.TrafficLightState invertLight(RoadBaseAI.TrafficLightState light) {
			switch (light) {
				case RoadBaseAI.TrafficLightState.Red:
				case RoadBaseAI.TrafficLightState.GreenToRed:
					return RoadBaseAI.TrafficLightState.Green;
				case RoadBaseAI.TrafficLightState.Green:
				case RoadBaseAI.TrafficLightState.RedToGreen:
				default:
					return RoadBaseAI.TrafficLightState.Red;
			}
		}

		internal void makeRedOrGreen() {
			if (LightLeft == RoadBaseAI.TrafficLightState.RedToGreen) {
				LightLeft = RoadBaseAI.TrafficLightState.Green;
			} else if (LightLeft == RoadBaseAI.TrafficLightState.GreenToRed) {
				LightLeft = RoadBaseAI.TrafficLightState.Red;
			}

			if (LightMain == RoadBaseAI.TrafficLightState.RedToGreen) {
				LightMain = RoadBaseAI.TrafficLightState.Green;
			} else if (LightMain == RoadBaseAI.TrafficLightState.GreenToRed) {
				LightMain = RoadBaseAI.TrafficLightState.Red;
			}

			if (LightRight == RoadBaseAI.TrafficLightState.RedToGreen) {
				LightRight = RoadBaseAI.TrafficLightState.Green;
			} else if (LightRight == RoadBaseAI.TrafficLightState.GreenToRed) {
				LightRight = RoadBaseAI.TrafficLightState.Red;
			}
		}

		internal void makeRed() {
			LightLeft = RoadBaseAI.TrafficLightState.Red;
			LightMain = RoadBaseAI.TrafficLightState.Red;
			LightRight = RoadBaseAI.TrafficLightState.Red;
		}
	}
}
