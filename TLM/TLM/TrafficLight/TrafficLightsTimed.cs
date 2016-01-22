using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Custom.AI;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	public class TrafficLightsTimed {
		public ushort nodeId;
		/// <summary>
		/// In case the traffic light is set for a group of nodes, the master node decides
		/// if all member steps are done.
		/// </summary>
		internal ushort masterNodeId;

		/// <summary>
		/// Timed traffic light by node id
		/// </summary>
		public static Dictionary<ushort, TrafficLightsTimed> TimedScripts = new Dictionary<ushort, TrafficLightsTimed>();

		/// <summary>
		/// Specifies if vehicles may enter the junction even if it is blocked by other vehicles
		/// </summary>
		public bool vehiclesMayEnterBlockedJunctions = false;

		public List<TimedTrafficStep> Steps = new List<TimedTrafficStep>();
		public int CurrentStep;

		public List<ushort> NodeGroup;
		private bool testMode = false;

		private uint lastSimulationStep = 0;

		public TrafficLightsTimed(ushort nodeId, IEnumerable<ushort> nodeGroup, bool vehiclesMayEnterBlockedJunctions) {
			this.nodeId = nodeId;
			NodeGroup = new List<ushort>(nodeGroup);
			masterNodeId = NodeGroup[0];
			this.vehiclesMayEnterBlockedJunctions = vehiclesMayEnterBlockedJunctions;

			// setup priority segments & live traffic lights
			foreach (ushort slaveNodeId in nodeGroup) {
				NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[slaveNodeId];

				for (int s = 0; s < 8; ++s) {
					ushort segmentId = node.GetSegment(s);
					if (segmentId <= 0)
						continue;
					NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
					TrafficPriority.AddPrioritySegment(slaveNodeId, segmentId, PrioritySegment.PriorityType.None);
					TrafficLightsManual.AddLiveSegmentLight(slaveNodeId, segmentId);
				}
			}

			TrafficLightSimulation.GetNodeSimulation(nodeId).TimedTrafficLightsActive = false;
		}

		public bool isMasterNode() {
			return masterNodeId == nodeId;
		}

		public void AddStep(int minTime, int maxTime, float waitFlowBalance) {
			if (minTime <= 0)
				minTime = 1;
			if (maxTime <= 0)
				maxTime = 1;
			if (maxTime < minTime)
				maxTime = minTime;

			Steps.Add(new TimedTrafficStep(minTime, maxTime, waitFlowBalance, nodeId, NodeGroup));
		}

		public void Start() {
			if (!housekeeping())
				return;

			CurrentStep = 0;
			Steps[0].SetLights();
			Steps[0].Start();

			TrafficLightSimulation.GetNodeSimulation(nodeId).TimedTrafficLightsActive = true;
		}

		internal void RemoveNodeFromGroup(ushort otherNodeId) {
			NodeGroup.Remove(otherNodeId);
			if (NodeGroup.Count <= 0) {
				TrafficLightSimulation.RemoveNodeFromSimulation(nodeId, true);
				return;
			}
			masterNodeId = NodeGroup[0];
		}

		private bool housekeeping() {
			bool mayStart = true;
			List<ushort> nodeIdsToDelete = new List<ushort>();

			int i = 0;
			foreach (ushort otherNodeId in NodeGroup) {
				if ((Singleton<NetManager>.instance.m_nodes.m_buffer[otherNodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
					Log.Warning($"Timed housekeeping: Remove node {otherNodeId}");
                    nodeIdsToDelete.Add(otherNodeId);
					if (otherNodeId == nodeId) {
						Log.Warning($"Timed housekeeping: Other is this. mayStart = false");
						mayStart = false;
					}
				}
				++i;
			}

			foreach (ushort nodeIdToDelete in nodeIdsToDelete) {
				NodeGroup.Remove(nodeIdToDelete);
				TrafficLightSimulation.RemoveNodeFromSimulation(nodeIdToDelete, false);
			}

			// check that simulation exists (TODO refactor this whole stuff!!)
			foreach (ushort timedNodeId in NodeGroup) {
				if (TrafficLightSimulation.GetNodeSimulation(timedNodeId) == null) {
					TrafficLightSimulation.AddNodeToSimulation(timedNodeId);
					TrafficLightSimulation.GetNodeSimulation(timedNodeId).TimedTrafficLights = true;

					// check that live traffic light exists
					NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[timedNodeId];
					for (int s = 0; s < 8; s++) {
						var segmentId = node.GetSegment(s);

						if (segmentId == 0)
							continue;
						TrafficLightsManual.AddLiveSegmentLight(timedNodeId, segmentId);
					}
				}
			}

			if (NodeGroup.Count <= 0) {
				Log.Warning($"Timed housekeeping: No lights left. mayStart = false");
				mayStart = false;
				return mayStart;
			}
			//Log.Warning($"Timed housekeeping: Setting master node to {NodeGroup[0]}");
			masterNodeId = NodeGroup[0];
			return mayStart;
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

		public void SimulationStep() {
			uint currentFrame = TimedTrafficStep.getCurrentFrame();
			if (lastSimulationStep >= currentFrame)
				return;
			lastSimulationStep = currentFrame;

			if (!isMasterNode() || !IsStarted())
				return;
			if (!housekeeping()) {
				Log.Warning($"Housekeeping detected that this timed traffic light has become invalid: {nodeId}.");
				Stop();
				return;
			}

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			if (!Steps[CurrentStep].isValid()) {
				TrafficLightSimulation.RemoveNodeFromSimulation(nodeId, false);
				return;
			}
			
			// set lights
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightsTimed slaveTimedNode = GetTimedLight(slaveNodeId);
				if (slaveTimedNode == null) {
					TrafficLightSimulation.RemoveNodeFromSimulation(nodeId, false);
					continue;
				}
				slaveTimedNode.Steps[CurrentStep].SetLights();
			}
			if (!Steps[CurrentStep].StepDone(true))
				return;
			// step is done

			if (!Steps[CurrentStep].isEndTransitionDone())
				return;
			// ending transition (yellow) finished

			// change step
			var newCurrentStep = (CurrentStep + 1) % NumSteps();
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightsTimed slaveTimedNode = GetTimedLight(slaveNodeId);
				if (slaveTimedNode == null) {
					continue;
				}

				slaveTimedNode.CurrentStep = newCurrentStep;
				slaveTimedNode.Steps[newCurrentStep].Start();
				slaveTimedNode.Steps[newCurrentStep].SetLights();
			}
		}

		public void SkipStep() {
			if (!isMasterNode())
				return;

			var newCurrentStep = (CurrentStep + 1) % NumSteps();
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightsTimed slaveTimedNode = GetTimedLight(slaveNodeId);
				if (slaveTimedNode == null) {
					continue;
				}
				slaveTimedNode.Steps[CurrentStep].SetStepDone();
				slaveTimedNode.CurrentStep = newCurrentStep;
				slaveTimedNode.Steps[newCurrentStep].Start();
				slaveTimedNode.Steps[newCurrentStep].SetLights();
			}
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

		public static void AddTimedLight(ushort nodeid, List<ushort> nodeGroup, bool vehiclesMayEnterBlockedJunctions) {
			TimedScripts.Add(nodeid, new TrafficLightsTimed(nodeid, nodeGroup, vehiclesMayEnterBlockedJunctions));
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
			if (NumSteps() <= 0) {
				// no steps defined, just create live traffic lights
				for (int s = 0; s < 8; ++s) {
					ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);
					if (segmentId <= 0)
						continue;
					TrafficLightsManual.AddLiveSegmentLight(nodeId, segmentId);
				}

				return;
			}
			
			for (int s = 0; s < 8; ++s) {
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);
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
					Log._Debug($"New segment detected: {segmentId} @ {nodeId}");
					// segment was created
					TrafficLightsManual.AddLiveSegmentLight(nodeId, segmentId);
					TrafficPriority.AddPrioritySegment(nodeId, segmentId, PrioritySegment.PriorityType.None);

					if (invalidSegmentIds.Count > 0) {
						var oldSegmentId = invalidSegmentIds[0];
						TrafficPriority.RemovePrioritySegment(nodeId, oldSegmentId);
						Log._Debug($"Replacing old segment {oldSegmentId} @ {nodeId} with new segment {segmentId}");

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
						Log._Debug($"Adding new segment {segmentId} to node {nodeId}");

						// create a new manual light
						for (int i = 0; i < NumSteps(); ++i) {
							Steps[i].addSegment(segmentId);
							Steps[i].calcMaxSegmentLength();
						}
					}
				}
			}
		}

		internal void SetTestMode(bool testMode) {
			this.testMode = false;
			if (!IsStarted())
				return;
			this.testMode = testMode;
		}

		internal bool IsInTestMode() {
			if (!IsStarted())
				testMode = false;
			return testMode;
		}

		internal void ChangeLightMode(ushort segmentId, ManualSegmentLight.Mode mode) {
			foreach (TimedTrafficStep step in Steps) {
				step.ChangeLightMode(segmentId, mode);
			}
		}
	}
}
