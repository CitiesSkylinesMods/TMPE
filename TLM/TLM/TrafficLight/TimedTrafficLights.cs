using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Custom.AI;
using TrafficManager.Traffic;
using System.Linq;

namespace TrafficManager.TrafficLight {
	public class TimedTrafficLights {
		public ushort NodeId {
			get; private set;
		}
		/// <summary>
		/// In case the traffic light is set for a group of nodes, the master node decides
		/// if all member steps are done.
		/// </summary>
		internal ushort masterNodeId;

		/// <summary>
		/// Specifies if vehicles may enter the junction even if it is blocked by other vehicles
		/// </summary>
		public bool vehiclesMayEnterBlockedJunctions = false;

		public List<TimedTrafficStep> Steps = new List<TimedTrafficStep>();
		public int CurrentStep;

		public List<ushort> NodeGroup;
		private bool testMode = false;

		private uint lastSimulationStep = 0;

		private bool started = false;

		public TimedTrafficLights(ushort nodeId, IEnumerable<ushort> nodeGroup, bool vehiclesMayEnterBlockedJunctions) {
			this.NodeId = nodeId;
			NodeGroup = new List<ushort>(nodeGroup);
			masterNodeId = NodeGroup[0];
			this.vehiclesMayEnterBlockedJunctions = vehiclesMayEnterBlockedJunctions;

			// setup priority segments & live traffic lights
			foreach (ushort slaveNodeId in nodeGroup) {
				for (int s = 0; s < 8; ++s) {
					ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[slaveNodeId].GetSegment(s);
					if (segmentId <= 0)
						continue;
					TrafficPriority.AddPrioritySegment(slaveNodeId, segmentId, PrioritySegment.PriorityType.None);
					ManualTrafficLights.AddLiveSegmentLight(slaveNodeId, segmentId);
				}
			}

			started = false;
		}

		public bool isMasterNode() {
			return masterNodeId == NodeId;
		}

		public void AddStep(int minTime, int maxTime, float waitFlowBalance, bool makeRed = false) {
			if (minTime <= 0)
				minTime = 1;
			if (maxTime <= 0)
				maxTime = 1;
			if (maxTime < minTime)
				maxTime = minTime;

			Steps.Add(new TimedTrafficStep(this, minTime, maxTime, waitFlowBalance, NodeGroup, makeRed));
		}

		public void Start() {
			if (!housekeeping())
				return;

			CurrentStep = 0;
			Steps[0].SetLights();
			Steps[0].Start();

			started = true;
		}

		internal void RemoveNodeFromGroup(ushort otherNodeId) {
			NodeGroup.Remove(otherNodeId);
			if (NodeGroup.Count <= 0) {
				TrafficLightSimulation.RemoveNodeFromSimulation(NodeId, true);
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
					if (otherNodeId == NodeId) {
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

			// check that live lights exist (TODO refactor?)
			foreach (ushort timedNodeId in NodeGroup) {
				for (int s = 0; s < 8; s++) {
					var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[timedNodeId].GetSegment(s);

					if (segmentId == 0)
						continue;
					ManualTrafficLights.AddLiveSegmentLight(timedNodeId, segmentId);
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
			started = false;
		}

		public bool IsStarted() {
			return started;
		}

		public int NumSteps() {
			return Steps.Count;
		}

		public TimedTrafficStep GetStep(int stepId) {
			return Steps[stepId];
		}

		public void SimulationStep() {
			var currentFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 5;
			if (lastSimulationStep >= currentFrame)
				return;
			lastSimulationStep = currentFrame;

			if (!isMasterNode() || !IsStarted())
				return;
			if (!housekeeping()) {
				Log.Warning($"Housekeeping detected that this timed traffic light has become invalid: {NodeId}.");
				Stop();
				return;
			}

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			if (!Steps[CurrentStep].isValid()) {
				TrafficLightSimulation.RemoveNodeFromSimulation(NodeId, false);
				return;
			}

			SetLights();

			if (!Steps[CurrentStep].StepDone(true)) {
				return;
			}
			// step is done

			SetLights();

			if (!Steps[CurrentStep].isEndTransitionDone())
				return;
			// ending transition (yellow) finished

			// change step
			var newCurrentStep = (CurrentStep + 1) % NumSteps();
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = TrafficLightSimulation.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					continue;
				}

				slaveSim.TimedLight.CurrentStep = newCurrentStep;
				slaveSim.TimedLight.Steps[newCurrentStep].Start();
				slaveSim.TimedLight.Steps[newCurrentStep].SetLights();
			}
		}

		public void SetLights() {
			// set lights
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = TrafficLightSimulation.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					//TrafficLightSimulation.RemoveNodeFromSimulation(slaveNodeId, false); // we iterate over NodeGroup!!
					continue;
				}
				slaveSim.TimedLight.Steps[CurrentStep].SetLights();
			}
		}

		public void SkipStep() {
			if (!isMasterNode())
				return;

			var newCurrentStep = (CurrentStep + 1) % NumSteps();
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = TrafficLightSimulation.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					continue;
				}

				slaveSim.TimedLight.Steps[CurrentStep].SetStepDone();
				slaveSim.TimedLight.CurrentStep = newCurrentStep;
				slaveSim.TimedLight.Steps[newCurrentStep].Start();
				slaveSim.TimedLight.Steps[newCurrentStep].SetLights();
			}
		}

		public long CheckNextChange(ushort segmentId, int lightType) {
			var curStep = CurrentStep;
			var nextStep = (CurrentStep + 1) % NumSteps();
			var numFrames = Steps[CurrentStep].MaxTimeRemaining();

			RoadBaseAI.TrafficLightState currentState;

			if (lightType == 0)
				currentState = ManualTrafficLights.GetSegmentLight(NodeId, segmentId).GetLightMain();
			else if (lightType == 1)
				currentState = ManualTrafficLights.GetSegmentLight(NodeId, segmentId).GetLightLeft();
			else if (lightType == 2)
				currentState = ManualTrafficLights.GetSegmentLight(NodeId, segmentId).GetLightRight();
			else
				currentState = ManualTrafficLights.GetSegmentLight(NodeId, segmentId).GetLightPedestrian();


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

		/*public static TimedTrafficLights AddTimedLight(ushort nodeid, List<ushort> nodeGroup, bool vehiclesMayEnterBlockedJunctions) {
			TimedScripts.Add(nodeid, new TimedTrafficLights(nodeid, nodeGroup, vehiclesMayEnterBlockedJunctions));
			return TimedScripts[nodeid];
		}

		public static void RemoveTimedLight(ushort nodeid) {
			TimedScripts.Remove(nodeid);
		}

		public static bool IsTimedLight(ushort nodeid) {
			return TimedScripts.ContainsKey(nodeid);
		}

		public static TimedTrafficLights GetTimedLight(ushort nodeid) {
			if (!IsTimedLight(nodeid))
				return null;
			return TimedScripts[nodeid];
		}

		internal static void OnLevelUnloading() {
			TimedScripts.Clear();
		}*/

		internal void handleNewSegments() {
			if (NumSteps() <= 0) {
				// no steps defined, just create live traffic lights
				for (int s = 0; s < 8; ++s) {
					ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
					if (segmentId <= 0)
						continue;
					ManualTrafficLights.AddLiveSegmentLight(NodeId, segmentId);
				}

				return;
			}

			for (int s = 0; s < 8; ++s) {
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
				if (segmentId <= 0)
					continue;

				List<ushort> invalidSegmentIds = new List<ushort>();
				bool isNewSegment = true;

				foreach (KeyValuePair<ushort, ManualSegmentLight> e in Steps[0].segmentLightStates) {
					var fromSegmentId = e.Key;
					var segLightState = e.Value;

					if (fromSegmentId == segmentId)
						isNewSegment = false;

					if (!TrafficPriority.IsPrioritySegment(NodeId, fromSegmentId))
						invalidSegmentIds.Add(fromSegmentId);
				}

				if (isNewSegment) {
					Log._Debug($"New segment detected: {segmentId} @ {NodeId}");
					// segment was created
					ManualTrafficLights.AddLiveSegmentLight(NodeId, segmentId);
					TrafficPriority.AddPrioritySegment(NodeId, segmentId, PrioritySegment.PriorityType.None);

					if (invalidSegmentIds.Count > 0) {
						var oldSegmentId = invalidSegmentIds[0];
						TrafficPriority.RemovePrioritySegment(NodeId, oldSegmentId);
						Log._Debug($"Replacing old segment {oldSegmentId} @ {NodeId} with new segment {segmentId}");

						// replace the old segment with the newly created one
						for (int i = 0; i < NumSteps(); ++i) {
							ManualSegmentLight segmentLight = Steps[i].segmentLightStates[oldSegmentId];
							Steps[i].segmentLightStates.Remove(oldSegmentId);
							segmentLight.SegmentId = segmentId;
							Steps[i].segmentLightStates.Add(segmentId, segmentLight);
							Steps[i].calcMaxSegmentLength();
							ManualTrafficLights.GetSegmentLight(NodeId, segmentId).CurrentMode = segmentLight.CurrentMode;
						}
					} else {
						Log._Debug($"Adding new segment {segmentId} to node {NodeId}");

						// create a new manual light
						for (int i = 0; i < NumSteps(); ++i) {
							Steps[i].addSegment(segmentId, true);
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

		internal void Join(TimedTrafficLights otherTimedLight) {
			if (NumSteps() < otherTimedLight.NumSteps()) {
				// increase the number of steps at our timed lights
				for (int i = NumSteps(); i < otherTimedLight.NumSteps(); ++i) {
					TimedTrafficStep otherStep = otherTimedLight.GetStep(i);
					foreach (ushort slaveNodeId in NodeGroup) {
						TrafficLightSimulation ourSim = TrafficLightSimulation.GetNodeSimulation(slaveNodeId);
						if (ourSim == null || !ourSim.IsTimedLight())
							continue;
						TimedTrafficLights ourTimedLight = ourSim.TimedLight;
						ourTimedLight.AddStep(otherStep.minTime, otherStep.maxTime, otherStep.waitFlowBalance, true);
					}
				}
			} else {
				// increase the number of steps at their timed lights
				for (int i = otherTimedLight.NumSteps(); i < NumSteps(); ++i) {
					TimedTrafficStep ourStep = GetStep(i);
					foreach (ushort slaveNodeId in otherTimedLight.NodeGroup) {
						TrafficLightSimulation theirSim = TrafficLightSimulation.GetNodeSimulation(slaveNodeId);
						if (theirSim == null || !theirSim.IsTimedLight())
							continue;
						TimedTrafficLights theirTimedLight = theirSim.TimedLight;
						theirTimedLight.AddStep(ourStep.minTime, ourStep.maxTime, ourStep.waitFlowBalance, true);
					}
				}
			}

			// join groups and re-defined master node, determine mean min/max times & mean wait-flow-balances
			HashSet<ushort> newNodeGroupSet = new HashSet<ushort>();
			newNodeGroupSet.UnionWith(NodeGroup);
			newNodeGroupSet.UnionWith(otherTimedLight.NodeGroup);
			List<ushort> newNodeGroup = new List<ushort>(newNodeGroupSet);
			ushort newMasterNodeId = newNodeGroup[0];

			int[] minTimes = new int[NumSteps()];
			int[] maxTimes = new int[NumSteps()];
			float[] waitFlowBalances = new float[NumSteps()];

			foreach (ushort timedNodeId in newNodeGroup) {
				TrafficLightSimulation timedSim = TrafficLightSimulation.GetNodeSimulation(timedNodeId);
				if (timedSim == null || !timedSim.IsTimedLight())
					continue;
				TimedTrafficLights timedLight = timedSim.TimedLight;
				for (int i = 0; i < NumSteps(); ++i) {
					minTimes[i] += timedLight.GetStep(i).minTime;
					maxTimes[i] += timedLight.GetStep(i).maxTime;
					waitFlowBalances[i] += timedLight.GetStep(i).waitFlowBalance;
				}

				timedLight.NodeGroup = newNodeGroup;
				timedLight.masterNodeId = newMasterNodeId;
			}

			// build means
			if (NumSteps() > 0) {
				for (int i = 0; i < NumSteps(); ++i) {
					minTimes[i] = Math.Max(1, minTimes[i] / newNodeGroup.Count);
					maxTimes[i] = Math.Max(1, maxTimes[i] / newNodeGroup.Count);
					waitFlowBalances[i] = Math.Max(0.001f, waitFlowBalances[i] / (float)newNodeGroup.Count);
				}
			}

			// apply means & reset
			foreach (ushort timedNodeId in newNodeGroup) {
				TrafficLightSimulation timedSim = TrafficLightSimulation.GetNodeSimulation(timedNodeId);
				if (timedSim == null || !timedSim.IsTimedLight())
					continue;
				TimedTrafficLights timedLight = timedSim.TimedLight;
				timedLight.Stop();
				timedLight.testMode = false;
				timedLight.lastSimulationStep = 0;
				for (int i = 0; i < NumSteps(); ++i) {
					timedLight.GetStep(i).minTime = minTimes[i];
					timedLight.GetStep(i).maxTime = maxTimes[i];
					timedLight.GetStep(i).waitFlowBalance = waitFlowBalances[i];
				}
			}
		}
	}
}
