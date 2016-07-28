using ColossalFramework;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;

namespace TrafficManager.Custom.AI {
	class CustomHumanAI : CitizenAI {
		public bool CustomCheckTrafficLights(ushort node, ushort segment) {
			var nodeSimulation = Options.timedLightsEnabled ? TrafficLightSimulation.GetNodeSimulation(node) : null;

			var instance = Singleton<NetManager>.instance;
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			var num = (uint)((node << 8) / 32768);
			var num2 = currentFrameIndex - num & 255u;

			// NON-STOCK CODE START //
			RoadBaseAI.TrafficLightState pedestrianLightState;
			CustomSegmentLights lights = CustomTrafficLights.GetSegmentLights(node, segment);

			if (lights == null || nodeSimulation == null || !nodeSimulation.IsSimulationActive()) {
				RoadBaseAI.TrafficLightState vehicleLightState;
				bool vehicles;
				bool pedestrians;

				RoadBaseAI.GetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if ((pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed || pedestrianLightState ==  RoadBaseAI.TrafficLightState.Red) && !pedestrians && num2 >= 196u) {
					RoadBaseAI.SetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, true);
					return true;
				}
			} else {
				if (lights.InvalidPedestrianLight) {
					pedestrianLightState = RoadBaseAI.TrafficLightState.Green;
				} else {
					pedestrianLightState = (RoadBaseAI.TrafficLightState)lights.PedestrianLightState;
				}
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
