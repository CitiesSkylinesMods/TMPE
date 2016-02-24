using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Traffic;
using TrafficManager.Custom.AI;

namespace TrafficManager.TrafficLight {
	public class TimedTrafficLightsStep : ICloneable {
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

		public uint lastFlowWaitCalc = 0;

		private List<ushort> groupNodeIds;
		private TimedTrafficLights timedNode;

		public Dictionary<ushort, CustomSegmentLights> segmentLights = new Dictionary<ushort, CustomSegmentLights>();

		/// <summary>
		/// Maximum segment length
		/// </summary>
		float maxSegmentLength = 0f;

		private bool invalid = false; // TODO rework

		public float waitFlowBalance = 1f;

		public TimedTrafficLightsStep(TimedTrafficLights timedNode, int minTime, int maxTime, float waitFlowBalance, List<ushort> groupNodeIds, bool makeRed=false) {
			this.minTime = minTime;
			this.maxTime = maxTime;
			this.waitFlowBalance = waitFlowBalance;
			this.timedNode = timedNode;

			this.groupNodeIds = groupNodeIds;

			minFlow = Single.NaN;
			maxWait = Single.NaN;

			endTransitionStart = null;
			stepDone = false;

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[timedNode.NodeId].GetSegment(s);
				if (segmentId <= 0)
					continue;

				addSegment(segmentId, makeRed);
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

		public bool isValid() {
			return !invalid;
		}

		/// <summary>
		/// Checks if the green-to-red (=yellow) phase is finished
		/// </summary>
		/// <returns></returns>
		internal bool isEndTransitionDone() {
			return endTransitionStart != null && getCurrentFrame() > endTransitionStart && StepDone(false);
		}

		/// <summary>
		/// Checks if the green-to-red (=yellow) phase is currently active
		/// </summary>
		/// <returns></returns>
		internal bool isInEndTransition() {
			return endTransitionStart != null && getCurrentFrame() <= endTransitionStart && StepDone(false);
		}

		internal bool isInStartTransition() {
			return getCurrentFrame() == startFrame && !StepDone(false);
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
		public void Start() {
			stepDone = false;
			this.startFrame = getCurrentFrame();
			this.endTransitionStart = null;
			minFlow = Single.NaN;
			maxWait = Single.NaN;
			lastFlowWaitCalc = 0;
		}

		internal static uint getCurrentFrame() {
			return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
		}

		/// <summary>
		/// Updates "real-world" traffic light states according to the timed scripts
		/// </summary>
		public void SetLights() {
			SetLights(false);
		}
		
		public void SetLights(bool noTransition) {
			try {
				bool atEndTransition = !noTransition && isInEndTransition(); // = yellow
				bool atStartTransition = !noTransition && !atEndTransition && isInStartTransition(); // = red + yellow

				TimedTrafficLightsStep previousStep = timedNode.Steps[(timedNode.CurrentStep + timedNode.Steps.Count - 1) % timedNode.Steps.Count];
				TimedTrafficLightsStep nextStep = timedNode.Steps[(timedNode.CurrentStep + 1) % timedNode.Steps.Count];

				foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
					var segmentId = e.Key;
					var curStepSegmentLights = e.Value;
					var prevStepSegmentLights = previousStep.segmentLights[segmentId];
					var nextStepSegmentLights = nextStep.segmentLights[segmentId];

					//segLightState.makeRedOrGreen(); // TODO temporary fix

					var liveSegmentLights = CustomTrafficLights.GetSegmentLights(timedNode.NodeId, segmentId);
					if (liveSegmentLights == null)
						continue;

					if (curStepSegmentLights.PedestrianLightState != null && prevStepSegmentLights.PedestrianLightState != null && nextStepSegmentLights.PedestrianLightState != null) {
						RoadBaseAI.TrafficLightState pedLightState = calcLightState((RoadBaseAI.TrafficLightState)prevStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)curStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)nextStepSegmentLights.PedestrianLightState, atStartTransition, atEndTransition);
						//Log._Debug($"TimedStep.SetLights: Setting pedestrian light state @ seg. {segmentId} to {pedLightState} {curStepSegmentLights.ManualPedestrianMode}");
                        liveSegmentLights.ManualPedestrianMode = curStepSegmentLights.ManualPedestrianMode;
						liveSegmentLights.PedestrianLightState = pedLightState;
						//Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId}: Ped.: {liveSegmentLights.PedestrianLightState.ToString()}");
					}

					foreach (ExtVehicleType vehicleType in curStepSegmentLights.VehicleTypes) {
						CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
						if (liveSegmentLight == null) {
							Log.Warning($"Timed step @ seg. {segmentId}, node {timedNode.NodeId} has a traffic light for {vehicleType} but the live segment does not have one.");
							continue;
						}
						CustomSegmentLight curStepSegmentLight = curStepSegmentLights.GetCustomLight(vehicleType);
						CustomSegmentLight prevStepSegmentLight = prevStepSegmentLights.GetCustomLight(vehicleType);
						CustomSegmentLight nextStepSegmentLight = nextStepSegmentLights.GetCustomLight(vehicleType);

						liveSegmentLight.CurrentMode = curStepSegmentLight.CurrentMode;
						liveSegmentLight.LightMain = calcLightState(prevStepSegmentLight.LightMain, curStepSegmentLight.LightMain, nextStepSegmentLight.LightMain, atStartTransition, atEndTransition);
						liveSegmentLight.LightLeft = calcLightState(prevStepSegmentLight.LightLeft, curStepSegmentLight.LightLeft, nextStepSegmentLight.LightLeft, atStartTransition, atEndTransition);
						liveSegmentLight.LightRight = calcLightState(prevStepSegmentLight.LightRight, curStepSegmentLight.LightRight, nextStepSegmentLight.LightRight, atStartTransition, atEndTransition);

						//Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId} for vehicle type {vehicleType}: L: {liveSegmentLight.LightLeft.ToString()} F: {liveSegmentLight.LightMain.ToString()} R: {liveSegmentLight.LightRight.ToString()}");
					}

					/*if (timedNode.NodeId == 20164) {
						Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId}: {segmentLight.LightLeft.ToString()} {segmentLight.LightMain.ToString()} {segmentLight.LightRight.ToString()} {segmentLight.LightPedestrian.ToString()}");
                    }*/

					liveSegmentLights.UpdateVisuals();
				}
			} catch (Exception e) {
				Log.Error($"Exception in TimedTrafficStep.SetLights: {e.Message}");
				invalid = true;
			}
		}

		/// <summary>
		/// Adds a new segment to this step. After adding all steps the method `rebuildSegmentIds` must be called.
		/// </summary>
		/// <param name="segmentId"></param>
		internal void addSegment(ushort segmentId, bool makeRed) {
			segmentLights.Add(segmentId, (CustomSegmentLights)CustomTrafficLights.GetOrLiveSegmentLights(timedNode.NodeId, segmentId).Clone());
			if (makeRed)
				segmentLights[segmentId].MakeRed();
			else
				segmentLights[segmentId].MakeRedOrGreen();
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
			foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
				var segmentId = e.Key;
				var segLights = e.Value;
				
				//if (segment == 0) continue;
				var liveSegLights = CustomTrafficLights.GetSegmentLights(timedNode.NodeId, segmentId);
				if (liveSegLights == null)
					continue;

				segLights.SetLights(liveSegLights);
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
			if (timedNode.IsInTestMode()) {
				return false;
			}
			if (stepDone)
				return true;

			if (getCurrentFrame() >= startFrame + maxTime) {
				// maximum time reached. switch!
#if DEBUG
				//Log.Message("step finished @ " + nodeId);
#endif
				stepDone = true;
				endTransitionStart = getCurrentFrame();
				return stepDone;
			}

			if (getCurrentFrame() >= startFrame + minTime) {
				if (timedNode.masterNodeId != timedNode.NodeId) {
					TrafficLightSimulation masterSim = TrafficLightSimulation.GetNodeSimulation(timedNode.masterNodeId);

					if (masterSim == null || !masterSim.IsTimedLight()) {
						invalid = true;
						stepDone = true;
						endTransitionStart = getCurrentFrame();
						return true;
					}
					TimedTrafficLights masterTimedNode = masterSim.TimedLight;
					bool done = masterTimedNode.Steps[masterTimedNode.CurrentStep].StepDone(updateValues);
#if DEBUG
					//Log.Message("step finished (1) @ " + nodeId);
#endif
					stepDone = done;
					if (stepDone)
						endTransitionStart = getCurrentFrame();
					return stepDone;
				} else {
					// we are the master node
					float wait, flow;
					uint curFrame = getCurrentFrame();
					if (lastFlowWaitCalc < curFrame) {
						if (!calcWaitFlow(out wait, out flow)) {
							stepDone = true;
							endTransitionStart = getCurrentFrame();
							return stepDone;
						} else {
							lastFlowWaitCalc = curFrame;
						}
					} else {
						flow = minFlow;
						wait = maxWait;
					}
					float newFlow = minFlow;
					float newWait = maxWait;

					if (Single.IsNaN(newFlow))
						newFlow = flow;
					else
						newFlow = 0.1f * newFlow + 0.9f * flow; // some smoothing

					if (Single.IsNaN(newWait))
						newWait = 0;
					else
						newWait = 0.1f * newWait + 0.9f * wait; // some smoothing

					// if more cars are waiting than flowing, we change the step
					bool done = newWait > 0 && newFlow < newWait;
					if (updateValues) {
						minFlow = newFlow;
						maxWait = newWait;
					}
#if DEBUG
					//Log.Message("step finished (2) @ " + nodeId);
#endif
					if (updateValues)
						stepDone = done;
					if (stepDone)
						endTransitionStart = getCurrentFrame();
					return stepDone;
				}
			}

			return false;
		}

		/// <summary>
		/// Calculates the current metrics for flowing and waiting vehicles
		/// </summary>
		/// <param name="wait"></param>
		/// <param name="flow"></param>
		/// <returns>true if the values could be calculated, false otherwise</returns>
		public bool calcWaitFlow(out float wait, out float flow) {
#if DEBUG
			bool debug = timedNode.NodeId == 17857;
#else
			bool debug = false;
#endif

			uint numFlows = 0;
			uint numWaits = 0;
			uint curMeanFlow = 0;
			uint curMeanWait = 0;

			// we are the master node. calculate traffic data
			foreach (ushort timedNodeId in groupNodeIds) {
				TrafficLightSimulation sim = TrafficLightSimulation.GetNodeSimulation(timedNodeId);
				if (sim == null || !sim.IsTimedLight())
					continue;
				TimedTrafficLights slaveTimedNode = sim.TimedLight;
				if (slaveTimedNode.NumSteps() <= timedNode.CurrentStep) {
					for (int i = 0; i < slaveTimedNode.NumSteps(); ++i)
						slaveTimedNode.GetStep(i).invalid = true;
					continue;
				}
				TimedTrafficLightsStep slaveStep = slaveTimedNode.Steps[timedNode.CurrentStep];

				//List<int> segmentIdsToDelete = new List<int>();

				// minimum time reached. check traffic!
				foreach (KeyValuePair<ushort, CustomSegmentLights> e in slaveStep.segmentLights) {
					var fromSegmentId = e.Key;
					var segLights = e.Value;

					// one of the traffic lights at this segment is green: count minimum traffic flowing through
					SegmentEnd fromSeg = TrafficPriority.GetPrioritySegment(timedNodeId, fromSegmentId);
					if (fromSeg == null) {
						//Log.Warning("stepDone(): prioSeg is null");
						//segmentIdsToDelete.Add(fromSegmentId);
						continue; // skip invalid segment
					}

					bool startPhase = getCurrentFrame() <= startFrame + minTime + 2; // during start phase all vehicles on "green" segments are counted as flowing
					ExtVehicleType validVehicleTypes = VehicleRestrictionsManager.GetAllowedVehicleTypes(fromSegmentId, timedNode.NodeId);

					foreach (ExtVehicleType vehicleType in segLights.VehicleTypes) {
						if (vehicleType != ExtVehicleType.None && (validVehicleTypes & vehicleType) == ExtVehicleType.None)
							continue;
						CustomSegmentLight segLight = segLights.GetCustomLight(vehicleType);
						if (segLight == null) {
							Log.Warning($"Timed traffic light step: Failed to get custom light for vehicleType {vehicleType} @ seg. {fromSegmentId}, node {timedNode.NodeId}!");
							continue;
						}

						Dictionary<ushort, uint>[] carsToSegmentMetrics = new Dictionary<ushort, uint>[startPhase ? 1 : 2];
						try {
							carsToSegmentMetrics[0] = fromSeg.GetVehicleMetricGoingToSegment(null, vehicleType, segLights.SeparateVehicleTypes, debug);
						} catch (Exception ex) {
							Log.Warning("calcWaitFlow: " + ex.ToString());
						}
						if (!startPhase) {
							try {
								carsToSegmentMetrics[1] = fromSeg.GetVehicleMetricGoingToSegment(0.1f, vehicleType, segLights.SeparateVehicleTypes, debug);
							} catch (Exception ex) {
								Log.Warning("calcWaitFlow: " + ex.ToString());
							}
						}

						if (carsToSegmentMetrics[0] == null)
							continue;

						// build directions from toSegment to fromSegment
						Dictionary<ushort, Direction> directions = new Dictionary<ushort, Direction>();
						foreach (KeyValuePair<ushort, uint> f in carsToSegmentMetrics[0]) {
							var toSegmentId = f.Key;
							SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(fromSegmentId);
							Direction dir = geometry.GetDirection(toSegmentId, timedNodeId);
							directions[toSegmentId] = dir;
						}

						// calculate waiting/flowing traffic
						for (int i = 0; i < carsToSegmentMetrics.Length; ++i) {
							if (carsToSegmentMetrics[i] == null)
								continue;

							foreach (KeyValuePair<ushort, uint> f in carsToSegmentMetrics[i]) {
								ushort toSegmentId = f.Key;
								uint totalNormCarLength = f.Value;

								bool addToFlow = false;
								switch (directions[toSegmentId]) {
									case Direction.Left:
										if (segLight.isLeftGreen())
											addToFlow = true;
										break;
									case Direction.Right:
										if (segLight.isRightGreen())
											addToFlow = true;
										break;
									case Direction.Forward:
									default:
										if (segLight.isForwardGreen())
											addToFlow = true;
										break;
								}

								if (addToFlow) {
									if (i > 0 || startPhase) {
										++numFlows;
										curMeanFlow += totalNormCarLength;
									}
								} else if (i == 0) {
									++numWaits;
									curMeanWait += totalNormCarLength;
								}
							}
						}
					}
				}

				// delete invalid segments from step
				/*foreach (int segmentId in segmentIdsToDelete) {
					slaveStep.segmentLightStates.Remove(segmentId);
				}*/

				if (slaveStep.segmentLights.Count <= 0) {
					invalid = true;
					flow = 0f;
					wait = 0f;
					return false;
				}
			}

			if (numFlows > 0)
				curMeanFlow /= numFlows;
			if (numWaits > 0)
				curMeanWait /= numWaits;

			float fCurMeanFlow = curMeanFlow;
			fCurMeanFlow /= 100f * waitFlowBalance; // a value smaller than 1 rewards steady traffic currents

			wait = (float)curMeanWait / 100f;
			flow = fCurMeanFlow;
			return true;
		}

		internal void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, CustomSegmentLight.Mode mode) {
			CustomSegmentLight light = segmentLights[segmentId].GetCustomLight(vehicleType);
			if (light != null)
				light.CurrentMode = mode;
		}

		public object Clone() {
			return MemberwiseClone();
		}
	}
}
