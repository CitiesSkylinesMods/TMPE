using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.TrafficLight {
	public interface ICustomSegmentLight : ICloneable {
		// TODO documentation
		ushort NodeId { get; }
		ushort SegmentId { get; }
		bool StartNode { get; }
		LightMode CurrentMode { get; set; }
		LightMode InternalCurrentMode { get; set; } // TODO should not be defined here
		RoadBaseAI.TrafficLightState LightLeft { get; }
		RoadBaseAI.TrafficLightState LightMain { get; }
		RoadBaseAI.TrafficLightState LightRight { get; }

		RoadBaseAI.TrafficLightState GetLightState(ArrowDirection dir);
		bool IsAnyGreen();
		bool IsGreen(ArrowDirection dir);
		bool IsAnyInTransition();
		bool IsInTransition(ArrowDirection dir);
		bool IsLeftGreen();
		bool IsMainGreen();
		bool IsRightGreen();
		bool IsLeftRed();
		bool IsMainRed();
		bool IsRightRed();
		bool IsRed(ArrowDirection dir);
		void UpdateVisuals();
		void MakeRed();
		void MakeRedOrGreen();
		void ToggleMode();
		void ChangeMainLight();
		void ChangeLeftLight();
		void ChangeRightLight();
		void SetStates(RoadBaseAI.TrafficLightState? mainLight, RoadBaseAI.TrafficLightState? leftLight, RoadBaseAI.TrafficLightState? rightLight, bool calcAutoPedLight = true);
	}
}
