#define DEBUGSTEPx
#define DEBUGTTLx
#define DEBUGMETRICx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Geometry;
using TrafficManager.Custom.AI;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.TrafficLight {
	// TODO class marked for complete rework in version 1.9
	public class TimedTrafficLightsStep : ICustomSegmentLightsManager {
		/// <summary>
		/// The number of time units this traffic light remains in the current state at least
		/// </summary>
		public int minTime;
		/// <summary>
		/// The number of time units this traffic light remains in the current state at most
		/// </summary>
		public int maxTime;
		public uint startFrame;

		/// <summary>
		/// Indicates if the step is done (internal use only)
		/// </summary>
		private bool stepDone;

		/// <summary>
		/// Frame when the GreenToRed phase started
		/// </summary>
		private uint? endTransitionStart;

		/// <summary>
		/// minimum mean "number of cars passing through" / "average segment length"
		/// </summary>
		public float minFlow;
		/// <summary>
		///	maximum mean "number of cars waiting for green" / "average segment length"
		/// </summary>
		public float maxWait;

		public int PreviousStepRefIndex = -1;
		public int NextStepRefIndex = -1;

		public uint lastFlowWaitCalc = 0;

		private TimedTrafficLights timedNode;

		public Dictionary<ushort, CustomSegmentLights> segmentLights = new Dictionary<ushort, CustomSegmentLights>();

		/// <summary>
		/// Maximum segment length
		/// </summary>
		float maxSegmentLength = 0f;

		public float waitFlowBalance = 1f;

		public TimedTrafficLightsStep(TimedTrafficLights timedNode, int minTime, int maxTime, float waitFlowBalance, bool makeRed=false) {
			this.minTime = minTime;
			this.maxTime = maxTime;
			this.waitFlowBalance = waitFlowBalance;
			this.timedNode = timedNode;

			minFlow = Single.NaN;
			maxWait = Single.NaN;

			endTransitionStart = null;
			stepDone = false;

			NodeGeometry nodeGeometry = NodeGeometry.Get(timedNode.NodeId);

			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null)
					continue;
				
				addSegment(end.SegmentId, end.StartNode, makeRed);
			}
			calcMaxSegmentLength();
		}

		internal void calcMaxSegmentLength() {
			maxSegmentLength = 0;
			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[timedNode.NodeId].GetSegment(s);
				
				if (segmentId <= 0)
					continue;

				float segLength = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_averageLength;
				if (segLength > maxSegmentLength)
					maxSegmentLength = segLength;
			}
		}

		/// <summary>
		/// Checks if the green-to-red (=yellow) phase is finished
		/// </summary>
		/// <returns></returns>
		internal bool IsEndTransitionDone() {
			if (!timedNode.IsMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.Steps[masterLights.CurrentStep].IsEndTransitionDone();
			}

			bool isStepDone = StepDone(false);
			bool ret = endTransitionStart != null && getCurrentFrame() > endTransitionStart && isStepDone;
#if DEBUGTTL
			Log._Debug($"TimedTrafficLightsStep.isEndTransitionDone() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} endTransitionStart={endTransitionStart} isStepDone={isStepDone} ret={ret}");
#endif
			return ret;
		}

		/// <summary>
		/// Checks if the green-to-red (=yellow) phase is currently active
		/// </summary>
		/// <returns></returns>
		internal bool IsInEndTransition() {
			if (!timedNode.IsMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.Steps[masterLights.CurrentStep].IsInEndTransition();
			}

			bool isStepDone = StepDone(false);
			bool ret = endTransitionStart != null && getCurrentFrame() <= endTransitionStart && isStepDone;
#if DEBUGTTL
			Log._Debug($"TimedTrafficLightsStep.isInEndTransition() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} endTransitionStart={endTransitionStart} isStepDone={isStepDone} ret={ret}");
#endif
			return ret;
		}

		internal bool IsInStartTransition() {
			if (!timedNode.IsMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.Steps[masterLights.CurrentStep].IsInStartTransition();
			}

			bool isStepDone = StepDone(false);
			bool ret = getCurrentFrame() == startFrame && !isStepDone;
#if DEBUGTTL
			Log._Debug($"TimedTrafficLightsStep.isInStartTransition() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} startFrame={startFrame} isStepDone={isStepDone} ret={ret}");
#endif

			return ret;
		}

		public RoadBaseAI.TrafficLightState GetLight(ushort segmentId, ExtVehicleType vehicleType, int lightType) {
			CustomSegmentLight segLight = segmentLights[segmentId].GetCustomLight(vehicleType);
			if (segLight != null) {
				switch (lightType) {
					case 0:
						return segLight.LightMain;
					case 1:
						return segLight.LightLeft;
					case 2:
						return segLight.LightRight;
					case 3:
						RoadBaseAI.TrafficLightState? pedState = segmentLights[segmentId].PedestrianLightState;
						return pedState == null ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)pedState;
				}
			}

			return RoadBaseAI.TrafficLightState.Green;
		}

		/// <summary>
		/// Starts the step.
		/// </summary>
		public void Start(int previousStepRefIndex=-1) {
			stepDone = false;
			this.startFrame = getCurrentFrame();
			this.endTransitionStart = null;
			minFlow = Single.NaN;
			maxWait = Single.NaN;
			lastFlowWaitCalc = 0;
			PreviousStepRefIndex = previousStepRefIndex;
			NextStepRefIndex = -1;

#if DEBUG
			/*if (GlobalConfig.Instance.DebugSwitches[2]) {
				if (timedNode.NodeId == 31605) {
					Log._Debug($"===== Step {timedNode.CurrentStep} @ node {timedNode.NodeId} =====");
					Log._Debug($"minTime: {minTime} maxTime: {maxTime}");
					foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
						Log._Debug($"\tSegment {e.Key}:");
						Log._Debug($"\t{e.Value.ToString()}");
					}
				}
			}*/
#endif
		}

		internal static uint getCurrentFrame() {
			return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
		}

		/// <summary>
		/// Updates "real-world" traffic light states according to the timed scripts
		/// </summary>
		public void UpdateLiveLights() {
			UpdateLiveLights(false);
		}
		
		public void UpdateLiveLights(bool noTransition) {
			try {
				CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

				bool atEndTransition = !noTransition && (IsInEndTransition() || IsEndTransitionDone()); // = yellow
				bool atStartTransition = !noTransition && !atEndTransition && IsInStartTransition(); // = red + yellow

#if DEBUG
				if (timedNode == null) {
					Log.Error($"TimedTrafficLightsStep: timedNode is null!");
					return;
				}
#endif

				if (PreviousStepRefIndex >= timedNode.NumSteps())
					PreviousStepRefIndex = -1;
				if (NextStepRefIndex >= timedNode.NumSteps())
					NextStepRefIndex = -1;
				TimedTrafficLightsStep previousStep = timedNode.Steps[PreviousStepRefIndex >= 0 ? PreviousStepRefIndex : ((timedNode.CurrentStep + timedNode.Steps.Count - 1) % timedNode.Steps.Count)];
				TimedTrafficLightsStep nextStep = timedNode.Steps[NextStepRefIndex >= 0 ? NextStepRefIndex : ((timedNode.CurrentStep + 1) % timedNode.Steps.Count)];

#if DEBUG
				if (previousStep == null) {
					Log.Error($"TimedTrafficLightsStep: previousStep is null!");
					return;
				}

				if (nextStep == null) {
					Log.Error($"TimedTrafficLightsStep: nextStep is null!");
					return;
				}

				if (previousStep.segmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: previousStep.segmentLights is null!");
					return;
				}

				if (nextStep.segmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: nextStep.segmentLights is null!");
					return;
				}

				if (segmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: segmentLights is null!");
					return;
				}
#endif

#if DEBUG
				//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition}) called for NodeId={timedNode.NodeId}. atStartTransition={atStartTransition} atEndTransition={atEndTransition}");
#endif

				foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
					var segmentId = e.Key;
					var curStepSegmentLights = e.Value;

#if DEBUG
					//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})   -> segmentId={segmentId} @ NodeId={timedNode.NodeId}");
#endif

					if (!previousStep.segmentLights.ContainsKey(segmentId)) {
#if DEBUG
						Log._Debug($"TimedTrafficLightsStep: previousStep does not contain lights for segment {segmentId}!");
#endif
						continue;
					}

					if (!nextStep.segmentLights.ContainsKey(segmentId)) {
#if DEBUG
						Log._Debug($"TimedTrafficLightsStep: nextStep does not contain lights for segment {segmentId}!");
#endif
						continue;
					}

					var prevStepSegmentLights = previousStep.segmentLights[segmentId];
					var nextStepSegmentLights = nextStep.segmentLights[segmentId];

					//segLightState.makeRedOrGreen(); // TODO temporary fix

					var liveSegmentLights = customTrafficLightsManager.GetSegmentLights(segmentId, curStepSegmentLights.StartNode, false);
					if (liveSegmentLights == null) {
						continue;
					}

					RoadBaseAI.TrafficLightState pedLightState = calcLightState((RoadBaseAI.TrafficLightState)prevStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)curStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)nextStepSegmentLights.PedestrianLightState, atStartTransition, atEndTransition);
					//Log._Debug($"TimedStep.SetLights: Setting pedestrian light state @ seg. {segmentId} to {pedLightState} {curStepSegmentLights.ManualPedestrianMode}");
                    liveSegmentLights.ManualPedestrianMode = curStepSegmentLights.ManualPedestrianMode;
					liveSegmentLights.PedestrianLightState = liveSegmentLights.AutoPedestrianLightState = pedLightState;
					//Log.Warning($"Step @ {timedNode.NodeId}: Segment {segmentId}: Ped.: {liveSegmentLights.PedestrianLightState.ToString()} / {liveSegmentLights.AutoPedestrianLightState.ToString()}");

#if DEBUG
					if (curStepSegmentLights.VehicleTypes == null) {
						Log.Error($"TimedTrafficLightsStep: curStepSegmentLights.VehicleTypes is null!");
						return;
					}
#endif

					foreach (ExtVehicleType vehicleType in curStepSegmentLights.VehicleTypes) {
#if DEBUG
						//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})     -> segmentId={segmentId} @ NodeId={timedNode.NodeId} for vehicle {vehicleType}");
#endif

						CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
						if (liveSegmentLight == null) {
#if DEBUG
							Log._Debug($"Timed step @ seg. {segmentId}, node {timedNode.NodeId} has a traffic light for {vehicleType} but the live segment does not have one.");
#endif
							continue;
						}
						CustomSegmentLight curStepSegmentLight = curStepSegmentLights.GetCustomLight(vehicleType);
						CustomSegmentLight prevStepSegmentLight = prevStepSegmentLights.GetCustomLight(vehicleType);
						CustomSegmentLight nextStepSegmentLight = nextStepSegmentLights.GetCustomLight(vehicleType);
						
#if DEBUG
						if (curStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: curStepSegmentLight is null!");
							return;
						}

						if (prevStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: prevStepSegmentLight is null!");
							return;
						}

						if (nextStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: nextStepSegmentLight is null!");
							return;
						}
#endif

						liveSegmentLight.currentMode = curStepSegmentLight.CurrentMode;
						/*curStepSegmentLight.EnsureModeLights();
						prevStepSegmentLight.EnsureModeLights();
						nextStepSegmentLight.EnsureModeLights();*/

						RoadBaseAI.TrafficLightState mainLight = calcLightState(prevStepSegmentLight.LightMain, curStepSegmentLight.LightMain, nextStepSegmentLight.LightMain, atStartTransition, atEndTransition);
						RoadBaseAI.TrafficLightState leftLight = calcLightState(prevStepSegmentLight.LightLeft, curStepSegmentLight.LightLeft, nextStepSegmentLight.LightLeft, atStartTransition, atEndTransition);
						RoadBaseAI.TrafficLightState rightLight = calcLightState(prevStepSegmentLight.LightRight, curStepSegmentLight.LightRight, nextStepSegmentLight.LightRight, atStartTransition, atEndTransition);
						liveSegmentLight.SetStates(mainLight, leftLight, rightLight, false);

#if DEBUGTTL
						Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})     -> *SETTING* LightLeft={liveSegmentLight.LightLeft} LightMain={liveSegmentLight.LightMain} LightRight={liveSegmentLight.LightRight} for segmentId={segmentId} @ NodeId={timedNode.NodeId} for vehicle {vehicleType}");
#endif

						//Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId} for vehicle type {vehicleType}: L: {liveSegmentLight.LightLeft.ToString()} F: {liveSegmentLight.LightMain.ToString()} R: {liveSegmentLight.LightRight.ToString()}");
					}

					/*if (timedNode.NodeId == 20164) {
						Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId}: {segmentLight.LightLeft.ToString()} {segmentLight.LightMain.ToString()} {segmentLight.LightRight.ToString()} {segmentLight.LightPedestrian.ToString()}");
                    }*/

					liveSegmentLights.UpdateVisuals();
				}
			} catch (Exception e) {
				Log.Error($"Exception in TimedTrafficStep.SetLights for node {timedNode.NodeId}: {e.ToString()}");
				//invalid = true;
			}
		}

		/// <summary>
		/// Adds a new segment to this step. It is cloned from the live custom traffic light. After adding all steps the method `rebuildSegmentIds` must be called.
		/// </summary>
		/// <param name="segmentId"></param>
		internal void addSegment(ushort segmentId, bool startNode, bool makeRed) {
			CustomSegmentLights clonedLights = (CustomSegmentLights)CustomSegmentLightsManager.Instance.GetOrLiveSegmentLights(segmentId, startNode).Clone();

			segmentLights.Add(segmentId, clonedLights);
			if (makeRed)
				segmentLights[segmentId].MakeRed();
			else
				segmentLights[segmentId].MakeRedOrGreen();
			clonedLights.LightsManager = this;
		}

		private RoadBaseAI.TrafficLightState calcLightState(RoadBaseAI.TrafficLightState previousState, RoadBaseAI.TrafficLightState currentState, RoadBaseAI.TrafficLightState nextState, bool atStartTransition, bool atEndTransition) {
			if (atStartTransition && currentState == RoadBaseAI.TrafficLightState.Green && previousState == RoadBaseAI.TrafficLightState.Red)
				return RoadBaseAI.TrafficLightState.RedToGreen;
			else if (atEndTransition && currentState == RoadBaseAI.TrafficLightState.Green && nextState == RoadBaseAI.TrafficLightState.Red)
				return RoadBaseAI.TrafficLightState.GreenToRed;
			else
				return currentState;
		}

		/// <summary>
		/// Updates timed segment lights according to "real-world" traffic light states
		/// </summary>
		public void UpdateLights() {
			Log._Debug($"TimedTrafficLightsStep.UpdateLights: Updating lights of timed traffic light step @ {timedNode.NodeId}");
			foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
				var segmentId = e.Key;
				var segLights = e.Value;

				Log._Debug($"TimedTrafficLightsStep.UpdateLights: Updating lights of timed traffic light step at seg. {e.Key} @ {timedNode.NodeId}");

				//if (segment == 0) continue;
				var liveSegLights = CustomSegmentLightsManager.Instance.GetSegmentLights(segmentId, segLights.StartNode, false);
				if (liveSegLights == null)
					continue;

				segLights.SetLights(liveSegLights);
				Log._Debug($"TimedTrafficLightsStep.UpdateLights: Segment {segmentId} AutoPedState={segLights.AutoPedestrianLightState} live={liveSegLights.AutoPedestrianLightState}");
			}
		}

		/// <summary>
		/// Countdown value for min. time
		/// </summary>
		/// <returns></returns>
		public long MinTimeRemaining() {
			return Math.Max(0, startFrame + minTime - getCurrentFrame());
		}

		/// <summary>
		/// Countdown value for max. time
		/// </summary>
		/// <returns></returns>
		public long MaxTimeRemaining() {
			return Math.Max(0, startFrame + maxTime - getCurrentFrame());
		}

		public void SetStepDone() {
			stepDone = true;
		}

		public bool StepDone(bool updateValues) {
			if (!timedNode.IsMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.Steps[masterLights.CurrentStep].StepDone(updateValues);
			}
			// we are the master node

			if (timedNode.IsInTestMode()) {
				return false;
			}
			if (stepDone) {
				return true;
			}

			if (getCurrentFrame() >= startFrame + maxTime) {
				// maximum time reached. switch!
#if DEBUG
				//Log.Message("step finished @ " + nodeId);
#endif
				if (!stepDone && updateValues) {
					stepDone = true;
					endTransitionStart = getCurrentFrame();
				}
				return stepDone;
			}

			if (getCurrentFrame() >= startFrame + minTime) {
				
					
				float wait, flow;
				uint curFrame = getCurrentFrame();
				//Log._Debug($"TTL @ {timedNode.NodeId}: curFrame={curFrame} lastFlowWaitCalc={lastFlowWaitCalc}");
				if (lastFlowWaitCalc < curFrame) {
					//Log._Debug($"TTL @ {timedNode.NodeId}: lastFlowWaitCalc<curFrame");
					if (!calcWaitFlow(true, timedNode.CurrentStep, out wait, out flow)) {
						//Log._Debug($"TTL @ {timedNode.NodeId}: calcWaitFlow failed!");
						if (!stepDone && updateValues) {
							//Log._Debug($"TTL @ {timedNode.NodeId}: !stepDone && updateValues");
							stepDone = true;
							endTransitionStart = getCurrentFrame();
						}
						return stepDone;
					} else {
						if (updateValues) {
							lastFlowWaitCalc = curFrame;
							//Log._Debug($"TTL @ {timedNode.NodeId}: updated lastFlowWaitCalc=curFrame={curFrame}");
						}
					}
				} else {
					flow = minFlow;
					wait = maxWait;
					//Log._Debug($"TTL @ {timedNode.NodeId}: lastFlowWaitCalc>=curFrame wait={maxWait} flow={minFlow}");
				}

				float newFlow = minFlow;
				float newWait = maxWait;

#if DEBUGMETRIC
				newFlow = flow;
				newWait = wait;
#else
				if (Single.IsNaN(newFlow))
					newFlow = flow;
				else
					newFlow = 0.1f * newFlow + 0.9f * flow; // some smoothing

				if (Single.IsNaN(newWait))
					newWait = 0;
				else
					newWait = 0.1f * newWait + 0.9f * wait; // some smoothing
#endif

				// if more cars are waiting than flowing, we change the step
				bool done = newWait > 0 && newFlow < newWait;

				//Log._Debug($"TTL @ {timedNode.NodeId}: newWait={newWait} newFlow={newFlow} updateValues={updateValues} stepDone={stepDone} done={done}");

				if (updateValues) {
					minFlow = newFlow;
					maxWait = newWait;
					//Log._Debug($"TTL @ {timedNode.NodeId}: updated minFlow=newFlow={minFlow} maxWait=newWait={maxWait}");
				}
#if DEBUG
				//Log.Message("step finished (2) @ " + nodeId);
#endif
				if (updateValues && !stepDone && done) {
					stepDone = done;
					endTransitionStart = getCurrentFrame();
				}
				return stepDone;
			}

			return false;
		}

		/// <summary>
		/// Calculates the current metrics for flowing and waiting vehicles
		/// </summary>
		/// <param name="wait"></param>
		/// <param name="flow"></param>
		/// <returns>true if the values could be calculated, false otherwise</returns>
		public bool calcWaitFlow(bool onlyMoving, int stepRefIndex, out float wait, out float flow) {
			uint numFlows = 0;
			uint numWaits = 0;
			uint curMeanFlow = 0;
			uint curMeanWait = 0;

			// TODO checking agains getCurrentFrame() is only valid if this is the current step
			if (onlyMoving && getCurrentFrame() <= startFrame + minTime + 1) { // during start phase all vehicles on "green" segments are counted as flowing
				onlyMoving = false;
			}

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			foreach (ushort timedNodeId in timedNode.NodeGroup) {
				TrafficLightSimulation sim = tlsMan.GetNodeSimulation(timedNodeId);
				if (sim == null || !sim.IsTimedLight())
					continue;
				TimedTrafficLights slaveTimedNode = sim.TimedLight;
				TimedTrafficLightsStep slaveStep = slaveTimedNode.Steps[stepRefIndex];

				//List<int> segmentIdsToDelete = new List<int>();

				// minimum time reached. check traffic!
				foreach (KeyValuePair<ushort, CustomSegmentLights> e in slaveStep.segmentLights) {
					var sourceSegmentId = e.Key;
					var segLights = e.Value;

#if DEBUGMETRIC
					bool debug = sourceSegmentId == 20857 && GlobalConfig.Instance.DebugSwitches[1];
#elif DEBUG
					bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == timedNodeId;
#else
					bool debug = false;
#endif

					Dictionary<ushort, ArrowDirection> directions = null;
					if (slaveStep.timedNode.Directions.ContainsKey(sourceSegmentId)) {
						directions = slaveStep.timedNode.Directions[sourceSegmentId];
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"calcWaitFlow: No arrow directions defined for segment {sourceSegmentId} @ {timedNodeId}");
						}
#endif
						continue;
					}

					// one of the traffic lights at this segment is green: count minimum traffic flowing through
					SegmentEnd sourceSegmentEnd = prioMan.GetPrioritySegment(timedNodeId, sourceSegmentId);
					if (sourceSegmentEnd == null) {
						Log.Warning($"TimedTrafficLightsStep.calcWaitFlow: No priority segment @ seg. {sourceSegmentId} found!");
						continue; // skip invalid segment
					}

					ExtVehicleType validVehicleTypes = VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(sourceSegmentId, timedNode.NodeId);

					foreach (KeyValuePair<byte, ExtVehicleType> e2 in segLights.VehicleTypeByLaneIndex) {
						byte laneIndex = e2.Key;
						ExtVehicleType vehicleType = e2.Value;
						if (vehicleType != ExtVehicleType.None && (validVehicleTypes & vehicleType) == ExtVehicleType.None)
							continue;
						CustomSegmentLight segLight = segLights.GetCustomLight(laneIndex);
						if (segLight == null) {
							Log.Warning($"Timed traffic light step: Failed to get custom light for vehicleType {vehicleType} @ seg. {sourceSegmentId}, node {timedNode.NodeId}!");
							continue;
						}

#if DEBUG
						if (debug)
							Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Checking lane {laneIndex} @ seg. {sourceSegmentId}. Vehicle types: {vehicleType}");
#endif

						Dictionary<ushort, uint> carsFlowingToSegmentMetric = null;
						Dictionary<ushort, uint> allCarsToSegmentMetric = null;
						bool evalFlowingVehicles = segLight.IsAnyGreen(); // flowing vehicle need only to be evaluated if a light is green
						if (evalFlowingVehicles && onlyMoving) {
							carsFlowingToSegmentMetric = sourceSegmentEnd.GetVehicleMetricGoingToSegment(false, laneIndex, debug);
						}
						allCarsToSegmentMetric = sourceSegmentEnd.GetVehicleMetricGoingToSegment(true, laneIndex, debug);

						// calculate waiting/flowing traffic
						foreach (KeyValuePair<ushort, uint> f in allCarsToSegmentMetric) {
							ushort targetSegmentId = f.Key;
							uint totalNumCars = f.Value;

							if (!directions.ContainsKey(targetSegmentId)) {
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Direction undefined for target segment {targetSegmentId} @ {timedNodeId}");
								continue;
							}

							if (evalFlowingVehicles) {
								uint totalNumFlowingCars = onlyMoving ? carsFlowingToSegmentMetric[f.Key] : totalNumCars;

#if DEBUG
								if (debug)
									Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Total num of flowing cars on seg. {sourceSegmentId}, lane {laneIndex} going to seg. {targetSegmentId}: {totalNumFlowingCars}");
#endif

								bool addToFlow = false;
								switch (directions[targetSegmentId]) {
									case ArrowDirection.Turn:
										addToFlow = TrafficPriorityManager.IsLeftHandDrive() ? segLight.IsRightGreen() : segLight.IsLeftGreen();
										break;
									case ArrowDirection.Left:
										addToFlow = segLight.IsLeftGreen();
										break;
									case ArrowDirection.Right:
										addToFlow = segLight.IsRightGreen();
										break;
									case ArrowDirection.Forward:
									default:
										addToFlow = segLight.IsMainGreen();
										break;
								}

								if (addToFlow) {
									curMeanFlow += totalNumFlowingCars;
									++numFlows;
								} else {
									curMeanWait += totalNumCars;
									++numWaits;
								}
							} else {
								curMeanWait += totalNumCars;
								++numWaits;
							}

#if DEBUG
							if (debug)
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Vehicles on lane {laneIndex} on seg. {sourceSegmentId} going to seg. {targetSegmentId} curMeanFlow={curMeanFlow}, curMeanWait={curMeanWait}, numFlows={numFlows}, numWaits={numWaits}");
#endif
						}
					}
				}
			}

			/*if (numFlows > 0)
				curMeanFlow /= numFlows;
			if (numWaits > 0)
				curMeanWait /= numWaits;*/
			
			float fCurMeanFlow = numFlows > 0 ? (float)curMeanFlow / (float)numFlows : 0;
			float fCurMeanWait = numWaits > 0 ? (float)curMeanWait / (float)numWaits : 0;
			fCurMeanFlow /= waitFlowBalance; // a value smaller than 1 rewards steady traffic currents

			wait = (float)fCurMeanWait;
			flow = fCurMeanFlow;

			return true;
		}

		internal void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, CustomSegmentLight.Mode mode) {
			CustomSegmentLight light = segmentLights[segmentId].GetCustomLight(vehicleType);
			if (light != null)
				light.CurrentMode = mode;
		}

		public CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
			if (nodeId != timedNode.NodeId) {
				Log._Debug($"TimedTrafficLightsStep @ node {timedNode.NodeId} does not handle custom traffic lights for node {nodeId}");
				return null;
			}

			if (segmentLights.ContainsKey(segmentId)) {
				return segmentLights[segmentId];
			} else {
				Log._Debug($"TimedTrafficLightsStep @ node {timedNode.NodeId} does not know segment {segmentId}");
				return null;
			}
		}
	}
}
