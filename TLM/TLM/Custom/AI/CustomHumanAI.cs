using ColossalFramework;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;

namespace TrafficManager.Custom.AI {
	class CustomHumanAI {
		public bool CustomCheckTrafficLights(ushort node, ushort segment) {
			var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(node);

			var instance = Singleton<NetManager>.instance;
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			var num = (uint)((node << 8) / 32768);
			var num2 = currentFrameIndex - num & 255u;

			// NON-STOCK CODE START //
			RoadBaseAI.TrafficLightState pedestrianLightState;
			ManualSegmentLight light = TrafficLightsManual.GetSegmentLight(node, segment);

			if (light == null || nodeSimulation == null || (nodeSimulation.TimedTrafficLights && !nodeSimulation.TimedTrafficLightsActive)) {
				RoadBaseAI.TrafficLightState vehicleLightState;
				bool vehicles;
				bool pedestrians;

				RoadBaseAI.GetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if ((pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed || pedestrianLightState ==  RoadBaseAI.TrafficLightState.Red) && !pedestrians && num2 >= 196u) {
					RoadBaseAI.SetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, true);
					return true;
				}
			} else {
				pedestrianLightState = light.GetLightPedestrian();
			}
			// NON-STOCK CODE END //

			switch (pedestrianLightState) {
				case RoadBaseAI.TrafficLightState.RedToGreen:
					if (num2 < 60u) {
						return false;
					}
					break;
				case RoadBaseAI.TrafficLightState.Red:
				case RoadBaseAI.TrafficLightState.GreenToRed:
					return false;
			}
			return true;
		}
	}
}
