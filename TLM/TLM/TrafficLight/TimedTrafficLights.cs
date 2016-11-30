#define DEBUGTTLx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using System.Linq;

namespace TrafficManager.TrafficLight {
	// TODO [version 1.9] define TimedTrafficLights per node group, not per individual nodes
	// TODO class marked for complete rework in version 1.9
	public class TimedTrafficLights {
		public ushort NodeId {
			get; private set;
		}
		/// <summary>
		/// In case the traffic light is set for a group of nodes, the master node decides
		/// if all member steps are done.
		/// </summary>
		internal ushort masterNodeId;

		public List<TimedTrafficLightsStep> Steps = new List<TimedTrafficLightsStep>();
		public int CurrentStep = 0;

		public List<ushort> NodeGroup;
		private bool testMode = false;

		private uint lastSimulationStep = 0;

		private bool started = false;

		public TimedTrafficLights(ushort nodeId, IEnumerable<ushort> nodeGroup) {
			this.NodeId = nodeId;
			NodeGroup = new List<ushort>(nodeGroup);
			masterNodeId = NodeGroup[0];

			SetupSegmentEnds();

			started = false;
		}

		public bool IsMasterNode() {
			return masterNodeId == NodeId;
		}

		public TimedTrafficLightsStep AddStep(int minTime, int maxTime, float waitFlowBalance, bool makeRed = false) {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			if (minTime < 0)
				minTime = 0;
			if (maxTime <= 0)
				maxTime = 1;
			if (maxTime < minTime)
				maxTime = minTime;

			TimedTrafficLightsStep step = new TimedTrafficLightsStep(this, minTime, maxTime, waitFlowBalance, makeRed);
			Steps.Add(step);
			return step;
		}

		public void Start() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			/*if (!housekeeping())
				return;*/

			CustomTrafficLightsManager customTrafficLightsManager = CustomTrafficLightsManager.Instance;

			for (int s = 0; s < 8; ++s) {
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
				if (segmentId == 0)
					continue;
				bool needsAlwaysGreenPedestrian = true;
				foreach (TimedTrafficLightsStep step in Steps) {
					if (!step.segmentLights.ContainsKey(segmentId))
						continue;
					if (step.segmentLights[segmentId].PedestrianLightState == RoadBaseAI.TrafficLightState.Green) {
						needsAlwaysGreenPedestrian = false;
						break;
					}
				}

				customTrafficLightsManager.GetOrLiveSegmentLights(NodeId, segmentId).InvalidPedestrianLight = needsAlwaysGreenPedestrian;
			}

			CurrentStep = 0;
			Steps[0].Start();
			Steps[0].SetLights();

			started = true;
		}

		internal void RemoveNodeFromGroup(ushort otherNodeId) {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			NodeGroup.Remove(otherNodeId);
			if (NodeGroup.Count <= 0) {
				TrafficLightSimulationManager.Instance.RemoveNodeFromSimulation(NodeId, true, false);
				return;
			}
			masterNodeId = NodeGroup[0];
		}

		internal bool housekeeping() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

#if TRACE
			Singleton<CodeProfiler>.instance.Start("TimedTrafficLights.housekeeping");
#endif
			if (NodeGroup == null || NodeGroup.Count <= 0) {
				Stop();
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TimedTrafficLights.housekeeping");
#endif
				return false;
			}

			/*bool mayStart = true;
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
			}*/

			// check that live lights exist (TODO refactor?)
			SetupSegmentEnds();

			/*if (NodeGroup.Count <= 0) {
				Log.Warning($"Timed housekeeping: No lights left. mayStart = false");
				mayStart = false;
				return mayStart;
			}*/
			//Log.Warning($"Timed housekeeping: Setting master node to {NodeGroup[0]}");
			masterNodeId = NodeGroup[0];

			/*if (housekeepCustomLights)
				foreach (TimedTrafficLightsStep step in Steps) {
					foreach (KeyValuePair<ushort, CustomSegmentLights> e in step.segmentLights) {
						e.Value.housekeeping(true);
					}
				}

			return mayStart;*/
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TimedTrafficLights.housekeeping");
#endif
			return true;
		}

		internal void StepHousekeeping() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

#if TRACE
			Singleton<CodeProfiler>.instance.Start("TimedTrafficLights.StepHousekeeping");
#endif
			foreach (TimedTrafficLightsStep step in Steps) {
				foreach (KeyValuePair<ushort, CustomSegmentLights> e in step.segmentLights) {
					e.Value.housekeeping(true);
				}
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TimedTrafficLights.StepHousekeeping");
#endif
		}

		public void MoveStep(int oldPos, int newPos) {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			var oldStep = Steps[oldPos];

			Steps.RemoveAt(oldPos);
			Steps.Insert(newPos, oldStep);
		}

		public void Stop() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			started = false;
		}

		internal void Destroy() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			Stop();
			DestroySegmentEnds();
			Steps = null;
			NodeGroup = null;
		}

		public bool IsStarted() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			return started;
		}

		public int NumSteps() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			return Steps.Count;
		}

		public TimedTrafficLightsStep GetStep(int stepId) {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			return Steps[stepId];
		}

		public void SimulationStep() {
			// TODO [version 1.9] this method is currently called on each node, but should be called on the master node only

#if TRACE
			Singleton<CodeProfiler>.instance.Start("TimedTrafficLights.SimulationStep");
#endif
			if (!IsMasterNode() || !IsStarted()) {
#if DEBUGTTL
				Log._Debug($"TTL SimStep: *STOP* NodeId={NodeId} isMasterNode={isMasterNode()} IsStarted={IsStarted()}");
#endif
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TimedTrafficLights.SimulationStep");
#endif
				return;
			}
			// we are the master node

			var currentFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 5;
#if DEBUGTTL
			Log._Debug($"TTL SimStep: nodeId={NodeId} currentFrame={currentFrame} lastSimulationStep={lastSimulationStep}");
#endif
			if (lastSimulationStep >= currentFrame) {
#if DEBUGTTL
				Log._Debug($"TTL SimStep: *STOP* lastSimulationStep >= currentFrame");
#endif
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TimedTrafficLights.SimulationStep");
#endif
				return;
			}
			lastSimulationStep = currentFrame;

			/*if (!housekeeping()) {
#if DEBUGTTL
				Log.Warning($"TTL SimStep: *STOP* NodeId={NodeId} Housekeeping detected that this timed traffic light has become invalid: {NodeId}.");
#endif
				Stop();
				return;
			}*/

#if DEBUGTTL
			Log._Debug($"TTL SimStep: NodeId={NodeId} Setting lights (1)");
#endif
			SetLights();

			if (!Steps[CurrentStep].StepDone(true)) {
#if DEBUGTTL
				Log._Debug($"TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}) is not done.");
#endif
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TimedTrafficLights.SimulationStep");
#endif
				return;
			}
			// step is done

#if DEBUGTTL
			Log._Debug($"TTL SimStep: NodeId={NodeId} Setting lights (2)");
#endif
			SetLights(); // check if this is needed

			if (!Steps[CurrentStep].IsEndTransitionDone()) {
#if DEBUGTTL
				Log._Debug($"TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}): end transition is not done.");
#endif
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TimedTrafficLights.SimulationStep");
#endif
				return;
			}
			// ending transition (yellow) finished

#if DEBUGTTL
			Log._Debug($"TTL SimStep: NodeId={NodeId} ending transition done. NodeGroup={string.Join(", ", NodeGroup.Select(x => x.ToString()).ToArray())}, nodeId={NodeId}, NumSteps={NumSteps()}");
#endif

			// change step
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
			CurrentStep = (CurrentStep + 1) % NumSteps();
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					continue;
				}
				TimedTrafficLights timedLights = slaveSim.TimedLight;
				timedLights.CurrentStep = CurrentStep;

#if DEBUGTTL
			Log._Debug($"TTL SimStep: NodeId={slaveNodeId} setting lgihts of next step: {CurrentStep}");
#endif

				timedLights.Steps[CurrentStep].Start();
				timedLights.Steps[CurrentStep].SetLights();
			}
		}

		public void SetLights() {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			// set lights
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					//TrafficLightSimulation.RemoveNodeFromSimulation(slaveNodeId, false); // we iterate over NodeGroup!!
					continue;
				}
				slaveSim.TimedLight.Steps[CurrentStep].SetLights();
			}
		}

		public void SkipStep(bool setLights=true) {
			if (!IsMasterNode())
				return;

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			var newCurrentStep = (CurrentStep + 1) % NumSteps();
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					continue;
				}

				slaveSim.TimedLight.Steps[CurrentStep].SetStepDone();
				slaveSim.TimedLight.CurrentStep = newCurrentStep;
				slaveSim.TimedLight.Steps[newCurrentStep].Start();
				if (setLights)
					slaveSim.TimedLight.Steps[newCurrentStep].SetLights();
			}
		}

		public long CheckNextChange(ushort segmentId, ExtVehicleType vehicleType, int lightType) {
			var curStep = CurrentStep;
			var nextStep = (CurrentStep + 1) % NumSteps();
			var numFrames = Steps[CurrentStep].MaxTimeRemaining();

			RoadBaseAI.TrafficLightState currentState;
			CustomSegmentLights segmentLights = CustomTrafficLightsManager.Instance.GetSegmentLights(NodeId, segmentId);
			if (segmentLights == null) {
				Log._Debug($"CheckNextChange: No segment lights at node {NodeId}, segment {segmentId}");
                return 99;
			}
			CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
			if (segmentLight == null) {
				Log._Debug($"CheckNextChange: No segment light at node {NodeId}, segment {segmentId}");
				return 99;
			}

			if (lightType == 0)
				currentState = segmentLight.GetLightMain();
			else if (lightType == 1)
				currentState = segmentLight.GetLightLeft();
			else if (lightType == 2)
				currentState = segmentLight.GetLightRight();
			else
				currentState = segmentLights.PedestrianLightState == null ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)segmentLights.PedestrianLightState;


			while (true) {
				if (nextStep == curStep) {
					numFrames = 99;
					break;
				}

				var light = Steps[nextStep].GetLight(segmentId, vehicleType, lightType);

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

		internal void handleNewSegments() {
			if (NumSteps() <= 0) {
				// no steps defined, just create live traffic lights
				/*for (int s = 0; s < 8; ++s) {
					ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
					if (segmentId <= 0)
						continue;
					if (! CustomTrafficLights.IsSegmentLight(NodeId, segmentId))
						CustomTrafficLights.AddSegmentLights(NodeId, segmentId);
				}*/


				return;
			}

			CustomTrafficLightsManager customTrafficLightsManager = CustomTrafficLightsManager.Instance;
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			for (int s = 0; s < 8; ++s) {
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
				if (segmentId <= 0)
					continue;

				List<ushort> invalidSegmentIds = new List<ushort>();
				bool isNewSegment = !Steps[0].segmentLights.ContainsKey(segmentId);

				if (isNewSegment) {
					// segment was created
					Log._Debug($"New segment detected: {segmentId} @ {NodeId}");

					foreach (KeyValuePair<ushort, CustomSegmentLights> e in Steps[0].segmentLights) {
						var fromSegmentId = e.Key;

						if (!prioMan.IsPrioritySegment(NodeId, fromSegmentId)) {
							Log._Debug($"Identified old segment {fromSegmentId} @ {NodeId}");
							invalidSegmentIds.Add(fromSegmentId);
						}
					}

					Log._Debug($"Setting up segment end for new segment {segmentId} @ {NodeId}");
					SetupSegmentEnd(segmentId);

					if (invalidSegmentIds.Count > 0) {
						var oldSegmentId = invalidSegmentIds[0];
						prioMan.RemovePrioritySegment(NodeId, oldSegmentId);
						Log._Debug($"Replacing old segment {oldSegmentId} @ {NodeId} with new segment {segmentId}");

						// replace the old segment with the newly created one
						for (int i = 0; i < NumSteps(); ++i) {
							if (!Steps[i].segmentLights.ContainsKey(oldSegmentId)) {
								Log.Error($"Step {i} at node {NodeId} does not contain step lights for old segment {oldSegmentId}");
								Steps[i].addSegment(segmentId, true);
								Steps[i].calcMaxSegmentLength();
								continue;
							}

							CustomSegmentLights customLights = Steps[i].segmentLights[oldSegmentId];
							Log._Debug($"Removing old segment {oldSegmentId} @ {NodeId} from step {i}");
							Steps[i].segmentLights.Remove(oldSegmentId);
							Log._Debug($"Setting new segment id {segmentId} at custom light from step {i}");
							customLights.SegmentId = segmentId;
							Steps[i].segmentLights.Add(segmentId, customLights);
							Steps[i].calcMaxSegmentLength();
							Log._Debug($"Getting live segment lights of new segment {segmentId} @ {NodeId} and applying mode @ step {i}");
							CustomSegmentLights liveSegLights = customTrafficLightsManager.GetSegmentLights(NodeId, segmentId);
							if (liveSegLights == null) {
								Log.Error($"No live segment lights for seg. {segmentId} @ node {NodeId} found!");
								customTrafficLightsManager.AddSegmentLights(NodeId, segmentId);
								liveSegLights = customTrafficLightsManager.GetSegmentLights(NodeId, segmentId);
							}

							foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in customLights.CustomLights) {
								CustomSegmentLight liveSegLight = liveSegLights.GetCustomLight(e.Key);
								if (liveSegLight == null)
									continue;
								Log._Debug($"Updating live segment light mode of new segment {segmentId} @ {NodeId} for vehicle type {e.Key} @ step {i}");
								liveSegLight.CurrentMode = e.Value.CurrentMode;
							}
							Log._Debug($"Finished applying new segment {segmentId} @ {NodeId} @ step {i}");
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

		internal TimedTrafficLights MasterLights() {
			TrafficLightSimulation masterSim = TrafficLightSimulationManager.Instance.GetNodeSimulation(masterNodeId);
			if (masterSim == null || !masterSim.IsTimedLight())
				return null;
			return masterSim.TimedLight;
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

		internal void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, CustomSegmentLight.Mode mode) {
			foreach (TimedTrafficLightsStep step in Steps) {
				step.ChangeLightMode(segmentId, vehicleType, mode);
			}
		}

		internal void Join(TimedTrafficLights otherTimedLight) {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			if (NumSteps() < otherTimedLight.NumSteps()) {
				// increase the number of steps at our timed lights
				for (int i = NumSteps(); i < otherTimedLight.NumSteps(); ++i) {
					TimedTrafficLightsStep otherStep = otherTimedLight.GetStep(i);
					foreach (ushort slaveNodeId in NodeGroup) {
						TrafficLightSimulation ourSim = tlsMan.GetNodeSimulation(slaveNodeId);
						if (ourSim == null || !ourSim.IsTimedLight())
							continue;
						TimedTrafficLights ourTimedLight = ourSim.TimedLight;
						ourTimedLight.AddStep(otherStep.minTime, otherStep.maxTime, otherStep.waitFlowBalance, true);
					}
				}
			} else {
				// increase the number of steps at their timed lights
				for (int i = otherTimedLight.NumSteps(); i < NumSteps(); ++i) {
					TimedTrafficLightsStep ourStep = GetStep(i);
					foreach (ushort slaveNodeId in otherTimedLight.NodeGroup) {
						TrafficLightSimulation theirSim = tlsMan.GetNodeSimulation(slaveNodeId);
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
				TrafficLightSimulation timedSim = tlsMan.GetNodeSimulation(timedNodeId);
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
				TrafficLightSimulation timedSim = tlsMan.GetNodeSimulation(timedNodeId);
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

		private void SetupSegmentEnds() {
			for (int s = 0; s < 8; ++s) {
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
				if (segmentId == 0)
					continue;
				SetupSegmentEnd(segmentId);
			}
		}

		private void DestroySegmentEnds() {
			for (int s = 0; s < 8; ++s) {
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
				if (segmentId == 0)
					continue;
				DestroySegmentEnd(segmentId);
			}
		}

		private void SetupSegmentEnd(ushort segmentId) {
			if (segmentId <= 0)
				return;

			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			if (!prioMan.IsPrioritySegment(NodeId, segmentId))
				prioMan.AddPrioritySegment(NodeId, segmentId, SegmentEnd.PriorityType.None);
		}

		private void DestroySegmentEnd(ushort segmentId) {
			if (segmentId <= 0)
				return;
			TrafficPriorityManager.Instance.RemovePrioritySegment(NodeId, segmentId);
		}
	}
}
