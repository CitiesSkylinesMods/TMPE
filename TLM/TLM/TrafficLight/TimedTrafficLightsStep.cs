#define DEBUGMETRICx

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

		private TimedTrafficLights timedNode;

		public Dictionary<ushort, CustomSegmentLights> segmentLights = new Dictionary<ushort, CustomSegmentLights>();

		/// <summary>
		/// Maximum segment length
		/// </summary>
		float maxSegmentLength = 0f;

		private bool invalid = false; // TODO rework

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
			if (!timedNode.isMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				if (masterLights != null)
					return masterLights.Steps[timedNode.CurrentStep].isInEndTransition();
			}
			return endTransitionStart != null && getCurrentFrame() <= endTransitionStart && StepDone(false);
		}

		internal bool isInStartTransition() {
			if (!timedNode.isMasterNode()) {
				TimedTrafficLights masterLights = timedNode.MasterLights();
				if (masterLights != null)
					return timedNode.MasterLights().Steps[timedNode.CurrentStep].isInStartTransition();
			}
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
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TimedTrafficLightsStep.SetLights");
#endif
			try {
				bool atEndTransition = !noTransition && isInEndTransition(); // = yellow
				bool atStartTransition = !noTransition && !atEndTransition && isInStartTransition(); // = red + yellow

#if DEBUG
				if (timedNode == null) {
					Log.Error($"TimedTrafficLightsStep: timedNode is null!");
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
					return;
				}
#endif

				TimedTrafficLightsStep previousStep = timedNode.Steps[(timedNode.CurrentStep + timedNode.Steps.Count - 1) % timedNode.Steps.Count];
				TimedTrafficLightsStep nextStep = timedNode.Steps[(timedNode.CurrentStep + 1) % timedNode.Steps.Count];

#if DEBUG
				if (previousStep == null) {
					Log.Error($"TimedTrafficLightsStep: previousStep is null!");
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
					return;
				}

				if (nextStep == null) {
					Log.Error($"TimedTrafficLightsStep: nextStep is null!");
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
					return;
				}

				if (previousStep.segmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: previousStep.segmentLights is null!");
					return;
				}

				if (nextStep.segmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: nextStep.segmentLights is null!");
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
					return;
				}

				if (segmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: segmentLights is null!");
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
					return;
				}
#endif

				foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
					var segmentId = e.Key;
					var curStepSegmentLights = e.Value;

#if DEBUG
					if (!previousStep.segmentLights.ContainsKey(segmentId)) {
						Log.Error($"TimedTrafficLightsStep: previousStep does not contain lights for segment {segmentId}!");
#if TRACE
						Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
						return;
					}

					if (!nextStep.segmentLights.ContainsKey(segmentId)) {
						Log.Error($"TimedTrafficLightsStep: nextStep does not contain lights for segment {segmentId}!");
#if TRACE
						Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
						return;
					}
#endif

					var prevStepSegmentLights = previousStep.segmentLights[segmentId];
					var nextStepSegmentLights = nextStep.segmentLights[segmentId];

					//segLightState.makeRedOrGreen(); // TODO temporary fix

					var liveSegmentLights = CustomTrafficLights.GetOrLiveSegmentLights(timedNode.NodeId, segmentId);
					if (liveSegmentLights == null) {
						continue;
					}

					if (curStepSegmentLights.PedestrianLightState != null && prevStepSegmentLights.PedestrianLightState != null && nextStepSegmentLights.PedestrianLightState != null) {
						RoadBaseAI.TrafficLightState pedLightState = calcLightState((RoadBaseAI.TrafficLightState)prevStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)curStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)nextStepSegmentLights.PedestrianLightState, atStartTransition, atEndTransition);
						//Log._Debug($"TimedStep.SetLights: Setting pedestrian light state @ seg. {segmentId} to {pedLightState} {curStepSegmentLights.ManualPedestrianMode}");
                        liveSegmentLights.ManualPedestrianMode = curStepSegmentLights.ManualPedestrianMode;
						liveSegmentLights.PedestrianLightState = pedLightState;
						//Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId}: Ped.: {liveSegmentLights.PedestrianLightState.ToString()}");
					}

#if DEBUG
					if (curStepSegmentLights.VehicleTypes == null) {
						Log.Error($"TimedTrafficLightsStep: curStepSegmentLights.VehicleTypes is null!");
#if TRACE
						Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
						return;
					}
#endif

					foreach (ExtVehicleType vehicleType in curStepSegmentLights.VehicleTypes) {
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
#if TRACE
							Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
							return;
						}

						if (prevStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: prevStepSegmentLight is null!");
#if TRACE
							Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
							return;
						}

						if (nextStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: nextStepSegmentLight is null!");
#if TRACE
							Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
							return;
						}
#endif

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
				Log.Error($"Exception in TimedTrafficStep.SetLights: {e.ToString()}");
				invalid = true;
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.SetLights");
#endif
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
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TimedTrafficLightsStep.UpdateLights");
#endif
			foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
				var segmentId = e.Key;
				var segLights = e.Value;
				
				//if (segment == 0) continue;
				var liveSegLights = CustomTrafficLights.GetSegmentLights(timedNode.NodeId, segmentId);
				if (liveSegLights == null)
					continue;

				segLights.SetLights(liveSegLights);
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.UpdateLights");
#endif
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
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TimedTrafficLightsStep.StepDone");
#endif
			if (timedNode.IsInTestMode()) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.StepDone");
#endif
				return false;
			}
			if (stepDone) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.StepDone");
#endif
				return true;
			}

			if (getCurrentFrame() >= startFrame + maxTime) {
				// maximum time reached. switch!
#if DEBUG
				//Log.Message("step finished @ " + nodeId);
#endif
				stepDone = true;
				endTransitionStart = getCurrentFrame();
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.StepDone");
#endif
				return stepDone;
			}

			if (getCurrentFrame() >= startFrame + minTime) {
				if (!timedNode.isMasterNode()) {
					TimedTrafficLights masterLights = timedNode.MasterLights();

					if (masterLights == null) {
						invalid = true;
						stepDone = true;
						endTransitionStart = getCurrentFrame();
#if TRACE
						Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.StepDone");
#endif
						return true;
					}
					bool done = masterLights.Steps[masterLights.CurrentStep].StepDone(updateValues);
#if DEBUG
					//Log.Message("step finished (1) @ " + nodeId);
#endif
					stepDone = done;
					if (stepDone)
						endTransitionStart = getCurrentFrame();
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.StepDone");
#endif
					return stepDone;
				} else {
					// we are the master node
					float wait, flow;
					uint curFrame = getCurrentFrame();
					if (lastFlowWaitCalc < curFrame) {
						if (!calcWaitFlow(out wait, out flow)) {
							stepDone = true;
							endTransitionStart = getCurrentFrame();
#if TRACE
							Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.StepDone");
#endif
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
#if DEBUGMETRIC
					bool done = false;
#else
					bool done = newWait > 0 && newFlow < newWait;
#endif
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
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.StepDone");
#endif
					return stepDone;
				}
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.StepDone");
#endif
			return false;
		}

		/// <summary>
		/// Calculates the current metrics for flowing and waiting vehicles
		/// </summary>
		/// <param name="wait"></param>
		/// <param name="flow"></param>
		/// <returns>true if the values could be calculated, false otherwise</returns>
		public bool calcWaitFlow(out float wait, out float flow) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TimedTrafficLightsStep.calcWaitFlow");
#endif

#if DEBUGMETRIC
			bool debug = timedNode.NodeId == 3201;
#else
			bool debug = false;
#endif

#if DEBUGMETRIC
			if (debug)
				Log.Warning($"TimedTrafficLightsStep.calcWaitFlow: ***START*** @ node {timedNode.NodeId}");
#endif

			uint numFlows = 0;
			uint numWaits = 0;
			uint curMeanFlow = 0;
			uint curMeanWait = 0;

			// we are the master node. calculate traffic data
			foreach (ushort timedNodeId in timedNode.NodeGroup) {
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
#if DEBUGMETRIC
						if (debug)
							Log.Warning($"TimedTrafficLightsStep.calcWaitFlow: No priority segment @ seg. {fromSegmentId} found!");
#endif
						//Log.Warning("stepDone(): prioSeg is null");
						//segmentIdsToDelete.Add(fromSegmentId);
						continue; // skip invalid segment
					}

					//bool startPhase = getCurrentFrame() <= startFrame + minTime + 2; // during start phase all vehicles on "green" segments are counted as flowing
					ExtVehicleType validVehicleTypes = VehicleRestrictionsManager.Instance().GetAllowedVehicleTypes(fromSegmentId, timedNode.NodeId);

					foreach (KeyValuePair<byte, ExtVehicleType> e2 in segLights.VehicleTypeByLaneIndex) {
						byte laneIndex = e2.Key;
						ExtVehicleType vehicleType = e2.Value;
						if (vehicleType != ExtVehicleType.None && (validVehicleTypes & vehicleType) == ExtVehicleType.None)
							continue;
						CustomSegmentLight segLight = segLights.GetCustomLight(laneIndex);
						if (segLight == null) {
							Log.Warning($"Timed traffic light step: Failed to get custom light for vehicleType {vehicleType} @ seg. {fromSegmentId}, node {timedNode.NodeId}!");
							continue;
						}

#if DEBUGMETRIC
						if (debug)
							Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Checking lane {laneIndex} @ seg. {fromSegmentId}. Vehicle types: {vehicleType}");
#endif

						Dictionary<ushort, uint> carsFlowingToSegmentMetric = null;
						Dictionary<ushort, uint> allCarsToSegmentMetric = null;
						try {
							carsFlowingToSegmentMetric = fromSeg.GetVehicleMetricGoingToSegment(false, laneIndex, debug);
						} catch (Exception ex) {
							Log.Warning("calcWaitFlow (1): " + ex.ToString());
						}

						try {
							allCarsToSegmentMetric = fromSeg.GetVehicleMetricGoingToSegment(true, laneIndex, debug);
						} catch (Exception ex) {
							Log.Warning("calcWaitFlow (2): " + ex.ToString());
						}

						if (carsFlowingToSegmentMetric == null)
							continue;

						// build directions from toSegment to fromSegment
						Dictionary<ushort, Direction> directions = new Dictionary<ushort, Direction>();
						foreach (KeyValuePair<ushort, uint> f in allCarsToSegmentMetric) {
							var toSegmentId = f.Key;
							SegmentGeometry geometry = SegmentGeometry.Get(fromSegmentId);
							Direction dir = geometry.GetDirection(toSegmentId, timedNodeId == geometry.StartNodeId());
							directions[toSegmentId] = dir;
#if DEBUGMETRIC
							if (debug)
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Calculated direction for seg. {fromSegmentId} -> seg. {toSegmentId}: {dir}");
#endif
						}

						// calculate waiting/flowing traffic
						foreach (KeyValuePair<ushort, uint> f in allCarsToSegmentMetric) {
							ushort toSegmentId = f.Key;
							uint totalNormCarLength = f.Value;
							uint totalFlowingNormCarLength = carsFlowingToSegmentMetric[f.Key];

#if DEBUGMETRIC
							if (debug)
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Total norm. car length of vehicles on lane {laneIndex} going to seg. {toSegmentId}: {totalNormCarLength}");
#endif

							bool addToFlow = false;
							switch (directions[toSegmentId]) {
								case Direction.Turn:
									addToFlow = TrafficPriority.IsLeftHandDrive() ? segLight.isRightGreen() : segLight.isLeftGreen();
									break;
								case Direction.Left:
									addToFlow = segLight.isLeftGreen();
									break;
								case Direction.Right:
									addToFlow = segLight.isRightGreen();
									break;
								case Direction.Forward:
								default:
									addToFlow = segLight.isForwardGreen();
									break;
							}

							if (addToFlow) {
								++numFlows;
								curMeanFlow += totalFlowingNormCarLength;
							} else {
								++numWaits;
								curMeanWait += totalNormCarLength;
							}

#if DEBUGMETRIC
							if (debug)
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Vehicles on lane {laneIndex} on seg. {fromSegmentId} going to seg. {toSegmentId} flowing? {addToFlow} curMeanFlow={curMeanFlow}, curMeanWait={curMeanWait}");
#endif
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
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.calcWaitFlow");
#endif
					return false;
				}
			}

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: ### Calculation completed. numFlows={numFlows}, numWaits={numWaits}, curMeanFlow={curMeanFlow}, curMeanWait={curMeanWait}");

			wait = curMeanWait;
			flow = curMeanFlow;
#else
			if (numFlows > 0)
				curMeanFlow /= numFlows;
			if (numWaits > 0)
				curMeanWait /= numWaits;

			float fCurMeanFlow = curMeanFlow;
			fCurMeanFlow /= waitFlowBalance; // a value smaller than 1 rewards steady traffic currents

			wait = (float)curMeanWait;
			flow = fCurMeanFlow;
#endif
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TimedTrafficLightsStep.calcWaitFlow");
#endif
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
