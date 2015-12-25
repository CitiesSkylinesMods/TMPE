using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	public class TrafficLightsTimed {
		public ushort NodeId;
		private ushort masterNodeId;

		public static Dictionary<ushort, TrafficLightsTimed> TimedScripts = new Dictionary<ushort, TrafficLightsTimed>();

		public List<TimedTrafficStep> Steps = new List<TimedTrafficStep>();
		public int CurrentStep;

		public List<ushort> NodeGroup;

		public TrafficLightsTimed(ushort nodeId, IEnumerable<ushort> nodeGroup) {
			NodeId = nodeId;
			NodeGroup = new List<ushort>(nodeGroup);
			masterNodeId = NodeGroup[0];

			CustomRoadAI.GetNodeSimulation(nodeId).TimedTrafficLightsActive = false;
		}

		public void AddStep(int minTime, int maxTime) {
			if (minTime < 0)
				minTime = 0;
			if (maxTime <= 0)
				maxTime = 1;
			if (maxTime < minTime)
				maxTime = minTime;

			Steps.Add(new TimedTrafficStep(minTime, maxTime, NodeId, masterNodeId, NodeGroup));
		}

		public void Start() {
			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			CurrentStep = 0;
			Steps[0].SetLights();
			Steps[0].Start(currentFrameIndex >> 6);

			CustomRoadAI.GetNodeSimulation(NodeId).TimedTrafficLightsActive = true;
		}

		public void MoveStep(int oldPos, int newPos) {
			var oldStep = Steps[oldPos];

			Steps.RemoveAt(oldPos);
			Steps.Insert(newPos, oldStep);
		}

		public void Stop() {
			CustomRoadAI.GetNodeSimulation(NodeId).TimedTrafficLightsActive = false;
		}

		public bool IsStarted() {
			return CustomRoadAI.GetNodeSimulation(NodeId).TimedTrafficLightsActive;
		}

		public int NumSteps() {
			return Steps.Count;
		}

		public TimedTrafficStep GetStep(int stepId) {
			return Steps[stepId];
		}

		public bool CheckCurrentStep() {
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			if (!Steps[CurrentStep].StepDone(currentFrameIndex >> 6)) return false;

			CurrentStep = (CurrentStep + 1) % Steps.Count;

			Steps[CurrentStep].Start(currentFrameIndex >> 6);
			Steps[CurrentStep].SetLights();
			return true;
		}

		public void SkipStep() {
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			CurrentStep = (CurrentStep + 1) % Steps.Count;

			Steps[CurrentStep].Start(currentFrameIndex >> 6);
			Steps[CurrentStep].SetLights();
		}

		public long CheckNextChange(int segmentId, int lightType) {
			var curStep = CurrentStep;
			var nextStep = CurrentStep + 1;
			var numFrames = Steps[CurrentStep].MaxTimeRemaining();

			RoadBaseAI.TrafficLightState currentState;

			if (lightType == 0)
				currentState = TrafficLightsManual.GetSegmentLight(NodeId, segmentId).GetLightMain();
			else if (lightType == 1)
				currentState = TrafficLightsManual.GetSegmentLight(NodeId, segmentId).GetLightLeft();
			else if (lightType == 2)
				currentState = TrafficLightsManual.GetSegmentLight(NodeId, segmentId).GetLightRight();
			else
				currentState = TrafficLightsManual.GetSegmentLight(NodeId, segmentId).GetLightPedestrian();


			while (true) {
				if (nextStep == curStep) {
					numFrames = 99;
					break;
				}

				var light = Steps[nextStep].GetLight(segmentId, lightType);

				if (light != currentState) {
					break;
				} else {
					numFrames += Steps[nextStep].maxTime;
				}

				nextStep = (nextStep + 1) % NumSteps();
			}

			return numFrames;
		}

		public void ResetSteps() {
			Steps.Clear();
		}

		public void RemoveStep(int id) {
			Steps.RemoveAt(id);
		}

		public static void AddTimedLight(ushort nodeid, List<ushort> nodeGroup) {
			TimedScripts.Add(nodeid, new TrafficLightsTimed(nodeid, nodeGroup));
		}

		public static void RemoveTimedLight(ushort nodeid) {
			TimedScripts.Remove(nodeid);
		}

		public static bool IsTimedLight(ushort nodeid) {
			return TimedScripts.ContainsKey(nodeid);
		}

		public static TrafficLightsTimed GetTimedLight(ushort nodeid) {
			if (!IsTimedLight(nodeid))
				return null;
			return TimedScripts[nodeid];
		}
	}
}
