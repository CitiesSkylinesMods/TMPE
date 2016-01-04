using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Custom.AI;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	public class TrafficLightsTimed {
		public ushort nodeId;
		private ushort masterNodeId;

		/// <summary>
		/// Timed traffic light by node id
		/// </summary>
		public static Dictionary<ushort, TrafficLightsTimed> TimedScripts = new Dictionary<ushort, TrafficLightsTimed>();

		public List<TimedTrafficStep> Steps = new List<TimedTrafficStep>();
		public int CurrentStep;

		public List<ushort> NodeGroup;

		public TrafficLightsTimed(ushort nodeId, IEnumerable<ushort> nodeGroup) {
			this.nodeId = nodeId;
			NodeGroup = new List<ushort>(nodeGroup);
			masterNodeId = NodeGroup[0];

			TrafficLightSimulation.GetNodeSimulation(nodeId).TimedTrafficLightsActive = false;
		}

		public void AddStep(int minTime, int maxTime) {
			if (minTime <= 0)
				minTime = 1;
			if (maxTime <= 0)
				maxTime = 1;
			if (maxTime < minTime)
				maxTime = minTime;

			Steps.Add(new TimedTrafficStep(minTime, maxTime, nodeId, masterNodeId, NodeGroup));
		}

		public void Start() {
			CurrentStep = 0;
			Steps[0].SetLights();
			Steps[0].Start();

			TrafficLightSimulation.GetNodeSimulation(nodeId).TimedTrafficLightsActive = true;
		}

		public void MoveStep(int oldPos, int newPos) {
			var oldStep = Steps[oldPos];

			Steps.RemoveAt(oldPos);
			Steps.Insert(newPos, oldStep);
		}

		public void Stop() {
			TrafficLightSimulation.GetNodeSimulation(nodeId).TimedTrafficLightsActive = false;
		}

		public bool IsStarted() {
			return TrafficLightSimulation.GetNodeSimulation(nodeId).TimedTrafficLightsActive;
		}

		public int NumSteps() {
			return Steps.Count;
		}

		public TimedTrafficStep GetStep(int stepId) {
			return Steps[stepId];
		}

		public bool CheckCurrentStep() {
			if (!IsStarted())
				return true;

			if (! Steps[CurrentStep].isValid()) {
				TrafficLightSimulation.RemoveNodeFromSimulation(nodeId);
				return false;
			}

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			Steps[CurrentStep].SetLights();
			if (!Steps[CurrentStep].StepDone()) {
				return false;
			}
			// step is done
			if (!Steps[CurrentStep].isEndTransitionDone()) return false;
			// ending transition (yellow) finished
			var oldCurrentStep = CurrentStep;
			CurrentStep = (CurrentStep + 1) % NumSteps();

			Steps[CurrentStep].Start();
			Steps[CurrentStep].SetLights();
			return true;
		}

		public void SkipStep() {
			Steps[CurrentStep].SetStepDone();

			CurrentStep = (CurrentStep + 1) % NumSteps();

			Steps[CurrentStep].Start();
			Steps[CurrentStep].SetLights();
		}

		public long CheckNextChange(ushort segmentId, int lightType) {
			var curStep = CurrentStep;
			var nextStep = (CurrentStep + 1) % NumSteps();
			var numFrames = Steps[CurrentStep].MaxTimeRemaining();

			RoadBaseAI.TrafficLightState currentState;

			if (lightType == 0)
				currentState = TrafficLightsManual.GetSegmentLight(nodeId, segmentId).GetLightMain();
			else if (lightType == 1)
				currentState = TrafficLightsManual.GetSegmentLight(nodeId, segmentId).GetLightLeft();
			else if (lightType == 2)
				currentState = TrafficLightsManual.GetSegmentLight(nodeId, segmentId).GetLightRight();
			else
				currentState = TrafficLightsManual.GetSegmentLight(nodeId, segmentId).GetLightPedestrian();


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

		internal static void OnLevelUnloading() {
			TimedScripts.Clear();
		}

		internal void handleNewSegments() {
			if (NumSteps() <= 0)
				return;

			NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
			
			for (int s = 0; s < 8; ++s) {
				ushort segmentId = node.GetSegment(s);
				if (segmentId <= 0)
					continue;
				NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

				List<ushort> invalidSegmentIds = new List<ushort>();
				bool isNewSegment = true;

				foreach (KeyValuePair<ushort, ManualSegmentLight> e in Steps[0].segmentLightStates) {
					var fromSegmentId = e.Key;
					var segLightState = e.Value;

					if (fromSegmentId == segmentId)
						isNewSegment = false;

					if (!TrafficPriority.IsPrioritySegment(nodeId, fromSegmentId))
						invalidSegmentIds.Add(fromSegmentId);
				}

				if (isNewSegment) {
					Log.Message($"New segment detected: {segmentId} @ {nodeId}");
					// segment was created
					TrafficLightsManual.AddLiveSegmentLight(nodeId, segmentId);
					TrafficPriority.AddPrioritySegment(nodeId, segmentId, PrioritySegment.PriorityType.None);

					if (invalidSegmentIds.Count > 0) {
						var oldSegmentId = invalidSegmentIds[0];
						TrafficPriority.RemovePrioritySegment(nodeId, oldSegmentId);
						Log.Message($"Replacing old segment {oldSegmentId} @ {nodeId} with new segment {segmentId}");

						// replace the old segment with the newly created one
						for (int i = 0; i < NumSteps(); ++i) {
							ManualSegmentLight segmentLight = Steps[i].segmentLightStates[oldSegmentId];
							Steps[i].segmentLightStates.Remove(oldSegmentId);
							segmentLight.SegmentId = segmentId;
							Steps[i].segmentLightStates.Add(segmentId, segmentLight);
							Steps[i].calcMaxSegmentLength();
							TrafficLightsManual.GetSegmentLight(nodeId, segmentId).CurrentMode = segmentLight.CurrentMode;
						}
					} else {
						Log.Message($"Adding new segment {segmentId} to node {nodeId}");

						// create a new manual light
						for (int i = 0; i < NumSteps(); ++i) {
							Steps[i].addSegment(segmentId);
							Steps[i].calcMaxSegmentLength();
						}
					}
				}
			}
		}
	}
}
