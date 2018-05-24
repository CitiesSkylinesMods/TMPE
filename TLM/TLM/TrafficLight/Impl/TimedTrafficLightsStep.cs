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
using TrafficManager.Util;
using System.Linq;
using CSUtil.Commons;
using TrafficManager.Geometry.Impl;
using CSUtil.Commons.Benchmark;
using TrafficManager.Manager.Impl;

namespace TrafficManager.TrafficLight.Impl {
	// TODO class should be completely reworked, approx. in version 1.10
	public class TimedTrafficLightsStep : ITimedTrafficLightsStep {
		/// <summary>
		/// The number of time units this traffic light remains in the current state at least
		/// </summary>
		public int MinTime { get; set; }

		/// <summary>
		/// The number of time units this traffic light remains in the current state at most
		/// </summary>
		public int MaxTime { get; set; }

		/// <summary>
		/// Indicates if waiting vehicles should be measured
		/// </summary>
		public StepChangeMetric ChangeMetric { get; set; }

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
		public float CurrentFlow { get; private set; }

		/// <summary>
		///	maximum mean "number of cars waiting for green" / "average segment length"
		/// </summary>
		public float CurrentWait { get; private set; }

		public int PreviousStepRefIndex { get; set; } = -1;
		public int NextStepRefIndex { get; set; } = -1;

		public uint lastFlowWaitCalc = 0;

		private ITimedTrafficLights timedNode;

		public IDictionary<ushort, ICustomSegmentLights> CustomSegmentLights { get; private set; } = new TinyDictionary<ushort, ICustomSegmentLights>();
		public LinkedList<ICustomSegmentLights> InvalidSegmentLights { get; private set; } = new LinkedList<ICustomSegmentLights>();

		public float WaitFlowBalance { get; set; } = 1f;

		public override string ToString() {
			return $"[TimedTrafficLightsStep\n" +
				"\t" + $"minTime = {MinTime}\n" +
				"\t" + $"maxTime = {MaxTime}\n" +
				"\t" + $"stepChangeMode = {ChangeMetric}\n" +
				"\t" + $"startFrame = {startFrame}\n" +
				"\t" + $"stepDone = {stepDone}\n" +
				"\t" + $"endTransitionStart = {endTransitionStart}\n" +
				"\t" + $"minFlow = {CurrentFlow}\n" +
				"\t" + $"maxWait = {CurrentWait}\n" +
				"\t" + $"PreviousStepRefIndex = {PreviousStepRefIndex}\n" +
				"\t" + $"NextStepRefIndex = {NextStepRefIndex}\n" +
				"\t" + $"lastFlowWaitCalc = {lastFlowWaitCalc}\n" +
				"\t" + $"CustomSegmentLights = {CustomSegmentLights}\n" +
				"\t" + $"InvalidSegmentLights = {InvalidSegmentLights.CollectionToString()}\n" +
				"\t" + $"waitFlowBalance = {WaitFlowBalance}\n" +
				"TimedTrafficLightsStep]";
		}

		public TimedTrafficLightsStep(ITimedTrafficLights timedNode, int minTime, int maxTime, StepChangeMetric stepChangeMode, float waitFlowBalance, bool makeRed=false) {
			this.MinTime = minTime;
			this.MaxTime = maxTime;
			this.ChangeMetric = stepChangeMode;
			this.WaitFlowBalance = waitFlowBalance;
			this.timedNode = timedNode;

			CurrentFlow = Single.NaN;
			CurrentWait = Single.NaN;

			endTransitionStart = null;
			stepDone = false;

			NodeGeometry nodeGeometry = NodeGeometry.Get(timedNode.NodeId);

			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null)
					continue;

				if (! AddSegment(end.SegmentId, end.StartNode, makeRed)) {
					Log.Warning($"TimedTrafficLightsStep.ctor: Failed to add segment {end.SegmentId} @ start {end.StartNode} to node {timedNode.NodeId}");
				}
			}
		}

		private TimedTrafficLightsStep() {

		}

		/// <summary>
		/// Checks if the green-to-red (=yellow) phase is finished
		/// </summary>
		/// <returns></returns>
		public bool IsEndTransitionDone() {
			if (!timedNode.IsMasterNode()) {
				ITimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.GetStep(masterLights.CurrentStep).IsEndTransitionDone();
			}

			bool ret = endTransitionStart != null && getCurrentFrame() > endTransitionStart && stepDone; //StepDone(false);
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.isEndTransitionDone() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} endTransitionStart={endTransitionStart} stepDone={stepDone} ret={ret}");
#endif
			return ret;
		}

		/// <summary>
		/// Checks if the green-to-red (=yellow) phase is currently active
		/// </summary>
		/// <returns></returns>
		public bool IsInEndTransition() {
			if (!timedNode.IsMasterNode()) {
				ITimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.GetStep(masterLights.CurrentStep).IsInEndTransition();
			}

			bool ret = endTransitionStart != null && getCurrentFrame() <= endTransitionStart && stepDone; //StepDone(false);
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.isInEndTransition() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} endTransitionStart={endTransitionStart} stepDone={stepDone} ret={ret}");
#endif
			return ret;
		}

		public bool IsInStartTransition() {
			if (!timedNode.IsMasterNode()) {
				ITimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.GetStep(masterLights.CurrentStep).IsInStartTransition();
			}

			bool ret = getCurrentFrame() == startFrame && !stepDone; //!StepDone(false);
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.isInStartTransition() called for master NodeId={timedNode.NodeId}. CurrentStep={timedNode.CurrentStep} getCurrentFrame()={getCurrentFrame()} startFrame={startFrame} stepDone={stepDone} ret={ret}");
#endif

			return ret;
		}

		public RoadBaseAI.TrafficLightState GetLightState(ushort segmentId, ExtVehicleType vehicleType, int lightType) {
			ICustomSegmentLight segLight = CustomSegmentLights[segmentId].GetCustomLight(vehicleType);
			if (segLight != null) {
				switch (lightType) {
					case 0:
						return segLight.LightMain;
					case 1:
						return segLight.LightLeft;
					case 2:
						return segLight.LightRight;
					case 3:
						RoadBaseAI.TrafficLightState? pedState = CustomSegmentLights[segmentId].PedestrianLightState;
						return pedState == null ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)pedState;
				}
			}

			return RoadBaseAI.TrafficLightState.Green;
		}

		/// <summary>
		/// Starts the step.
		/// </summary>
		public void Start(int previousStepRefIndex=-1) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.Start: Starting step {timedNode.CurrentStep} @ {timedNode.NodeId}");
#endif

			this.startFrame = getCurrentFrame();
			Reset();
			PreviousStepRefIndex = previousStepRefIndex;

#if DEBUG
			/*if (GlobalConfig.Instance.Debug.Switches[2]) {
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

		internal void Reset() {
			this.endTransitionStart = null;
			CurrentFlow = Single.NaN;
			CurrentWait = Single.NaN;
			lastFlowWaitCalc = 0;
			PreviousStepRefIndex = -1;
			NextStepRefIndex = -1;
			stepDone = false;
		}

		internal static uint getCurrentFrame() {
			return Constants.ServiceFactory.SimulationService.CurrentFrameIndex >> 6;
		}

		/// <summary>
		/// Updates "real-world" traffic light states according to the timed scripts
		/// </summary>
		public void UpdateLiveLights() {
			UpdateLiveLights(false);
		}
		
		public void UpdateLiveLights(bool noTransition) {
			try {
				ICustomSegmentLightsManager customTrafficLightsManager = Constants.ManagerFactory.CustomSegmentLightsManager;

				bool atEndTransition = !noTransition && (IsInEndTransition() || IsEndTransitionDone()); // = yellow
				bool atStartTransition = !noTransition && !atEndTransition && IsInStartTransition(); // = red + yellow

#if DEBUGTTL
				if (timedNode == null) {
					Log.Error($"TimedTrafficLightsStep: timedNode is null!");
					return;
				}
#endif

				if (PreviousStepRefIndex >= timedNode.NumSteps())
					PreviousStepRefIndex = -1;
				if (NextStepRefIndex >= timedNode.NumSteps())
					NextStepRefIndex = -1;
				ITimedTrafficLightsStep previousStep = timedNode.GetStep(PreviousStepRefIndex >= 0 ? PreviousStepRefIndex : ((timedNode.CurrentStep + timedNode.NumSteps() - 1) % timedNode.NumSteps()));
				ITimedTrafficLightsStep nextStep = timedNode.GetStep(NextStepRefIndex >= 0 ? NextStepRefIndex : ((timedNode.CurrentStep + 1) % timedNode.NumSteps()));

#if DEBUGTTL
				if (previousStep == null) {
					Log.Error($"TimedTrafficLightsStep: previousStep is null!");
					//return;
				}

				if (nextStep == null) {
					Log.Error($"TimedTrafficLightsStep: nextStep is null!");
					//return;
				}

				if (previousStep.CustomSegmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: previousStep.segmentLights is null!");
					//return;
				}

				if (nextStep.CustomSegmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: nextStep.segmentLights is null!");
					//return;
				}

				if (CustomSegmentLights == null) {
					Log.Error($"TimedTrafficLightsStep: segmentLights is null!");
					//return;
				}
#endif

#if DEBUG
				//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition}) called for NodeId={timedNode.NodeId}. atStartTransition={atStartTransition} atEndTransition={atEndTransition}");
#endif

				foreach (KeyValuePair<ushort, ICustomSegmentLights> e in CustomSegmentLights) {
					var segmentId = e.Key;
					var curStepSegmentLights = e.Value;

#if DEBUG
					//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})   -> segmentId={segmentId} @ NodeId={timedNode.NodeId}");
#endif

					ICustomSegmentLights prevStepSegmentLights = null;
					if (!previousStep.CustomSegmentLights.TryGetValue(segmentId, out prevStepSegmentLights)) {
#if DEBUGTTL
						if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
							Log.Warning($"TimedTrafficLightsStep: previousStep does not contain lights for segment {segmentId}!");
#endif
						continue;
					}

					ICustomSegmentLights nextStepSegmentLights = null;
					if (!nextStep.CustomSegmentLights.TryGetValue(segmentId, out nextStepSegmentLights)) {
#if DEBUGTTL
						if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
							Log.Warning($"TimedTrafficLightsStep: nextStep does not contain lights for segment {segmentId}!");
#endif
						continue;
					}

					//segLightState.makeRedOrGreen(); // TODO temporary fix

					ICustomSegmentLights liveSegmentLights = customTrafficLightsManager.GetSegmentLights(segmentId, curStepSegmentLights.StartNode, false);
					if (liveSegmentLights == null) {
						Log.Warning($"TimedTrafficLightsStep.UpdateLights() @ node {timedNode.NodeId}: Could not retrieve live segment lights for segment {segmentId} @ start {curStepSegmentLights.StartNode}.");
						continue;
					}

					RoadBaseAI.TrafficLightState pedLightState = calcLightState((RoadBaseAI.TrafficLightState)prevStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)curStepSegmentLights.PedestrianLightState, (RoadBaseAI.TrafficLightState)nextStepSegmentLights.PedestrianLightState, atStartTransition, atEndTransition);
					//Log._Debug($"TimedStep.SetLights: Setting pedestrian light state @ seg. {segmentId} to {pedLightState} {curStepSegmentLights.ManualPedestrianMode}");
                    liveSegmentLights.ManualPedestrianMode = curStepSegmentLights.ManualPedestrianMode;
					liveSegmentLights.PedestrianLightState = liveSegmentLights.AutoPedestrianLightState = pedLightState;
					//Log.Warning($"Step @ {timedNode.NodeId}: Segment {segmentId}: Ped.: {liveSegmentLights.PedestrianLightState.ToString()} / {liveSegmentLights.AutoPedestrianLightState.ToString()}");

#if DEBUGTTL
					if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
						if (curStepSegmentLights.VehicleTypes == null) {
							Log.Error($"TimedTrafficLightsStep: curStepSegmentLights.VehicleTypes is null!");
							return;
						}
#endif

					foreach (ExtVehicleType vehicleType in curStepSegmentLights.VehicleTypes) {
#if DEBUG
						//Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})     -> segmentId={segmentId} @ NodeId={timedNode.NodeId} for vehicle {vehicleType}");
#endif

						ICustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
						if (liveSegmentLight == null) {
#if DEBUGTTL
							if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
								Log._Debug($"Timed step @ seg. {segmentId}, node {timedNode.NodeId} has a traffic light for {vehicleType} but the live segment does not have one.");
#endif
							continue;
						}
						ICustomSegmentLight curStepSegmentLight = curStepSegmentLights.GetCustomLight(vehicleType);
						ICustomSegmentLight prevStepSegmentLight = prevStepSegmentLights.GetCustomLight(vehicleType);
						ICustomSegmentLight nextStepSegmentLight = nextStepSegmentLights.GetCustomLight(vehicleType);
						
#if DEBUGTTL
						if (curStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: curStepSegmentLight is null!");
							//return;
						}

						if (prevStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: prevStepSegmentLight is null!");
							//return;
						}

						if (nextStepSegmentLight == null) {
							Log.Error($"TimedTrafficLightsStep: nextStepSegmentLight is null!");
							//return;
						}
#endif

						liveSegmentLight.InternalCurrentMode = curStepSegmentLight.CurrentMode; // TODO improve & remove
						/*curStepSegmentLight.EnsureModeLights();
						prevStepSegmentLight.EnsureModeLights();
						nextStepSegmentLight.EnsureModeLights();*/

						RoadBaseAI.TrafficLightState mainLight = calcLightState(prevStepSegmentLight.LightMain, curStepSegmentLight.LightMain, nextStepSegmentLight.LightMain, atStartTransition, atEndTransition);
						RoadBaseAI.TrafficLightState leftLight = calcLightState(prevStepSegmentLight.LightLeft, curStepSegmentLight.LightLeft, nextStepSegmentLight.LightLeft, atStartTransition, atEndTransition);
						RoadBaseAI.TrafficLightState rightLight = calcLightState(prevStepSegmentLight.LightRight, curStepSegmentLight.LightRight, nextStepSegmentLight.LightRight, atStartTransition, atEndTransition);
						liveSegmentLight.SetStates(mainLight, leftLight, rightLight, false);

#if DEBUGTTL
						if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
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
				Log.Error($"Exception in TimedTrafficStep.UpdateLiveLights for node {timedNode.NodeId}: {e.ToString()}");
				//invalid = true;
			}
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
			foreach (KeyValuePair<ushort, ICustomSegmentLights> e in CustomSegmentLights) {
				var segmentId = e.Key;
				ICustomSegmentLights segLights = e.Value;

				Log._Debug($"TimedTrafficLightsStep.UpdateLights: Updating lights of timed traffic light step at seg. {e.Key} @ {timedNode.NodeId}");

				//if (segment == 0) continue;
				ICustomSegmentLights liveSegLights = Constants.ManagerFactory.CustomSegmentLightsManager.GetSegmentLights(segmentId, segLights.StartNode, false);
				if (liveSegLights == null) {
					Log.Warning($"TimedTrafficLightsStep.UpdateLights() @ node {timedNode.NodeId}: Could not retrieve live segment lights for segment {segmentId} @ start {segLights.StartNode}.");
					continue;
				}

				segLights.SetLights(liveSegLights);
				Log._Debug($"TimedTrafficLightsStep.UpdateLights: Segment {segmentId} AutoPedState={segLights.AutoPedestrianLightState} live={liveSegLights.AutoPedestrianLightState}");
			}
		}

		/// <summary>
		/// Countdown value for min. time
		/// </summary>
		/// <returns></returns>
		public long MinTimeRemaining() {
			return Math.Max(0, startFrame + MinTime - getCurrentFrame());
		}

		/// <summary>
		/// Countdown value for max. time
		/// </summary>
		/// <returns></returns>
		public long MaxTimeRemaining() {
			return Math.Max(0, startFrame + MaxTime - getCurrentFrame());
		}

		public void SetStepDone() {
			stepDone = true;
		}

		public bool StepDone(bool updateValues) {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId;
			if (debug) {
				Log._Debug($"StepDone: called for node {timedNode.NodeId} @ step {timedNode.CurrentStep}");
			}
#endif

			if (!timedNode.IsMasterNode()) {
				ITimedTrafficLights masterLights = timedNode.MasterLights();
				return masterLights.GetStep(masterLights.CurrentStep).StepDone(updateValues);
			}
			// we are the master node

			if (timedNode.IsInTestMode()) {
				return false;
			}
			if (stepDone) {
				return true;
			}

			if (getCurrentFrame() >= startFrame + MaxTime) {
				// maximum time reached. switch!
#if DEBUGTTL
				if (debug)
					Log._Debug($"StepDone: step finished @ {timedNode.NodeId}");
#endif
				if (!stepDone && updateValues) {
					stepDone = true;
					endTransitionStart = getCurrentFrame();
				}
				return stepDone;
			}

			if (getCurrentFrame() >= startFrame + MinTime) {
				float wait, flow;
				uint curFrame = getCurrentFrame();
				//Log._Debug($"TTL @ {timedNode.NodeId}: curFrame={curFrame} lastFlowWaitCalc={lastFlowWaitCalc}");
				if (lastFlowWaitCalc < curFrame) {
					//Log._Debug($"TTL @ {timedNode.NodeId}: lastFlowWaitCalc<curFrame");
#if BENCHMARK
					using (var bm = new Benchmark(null, "CalcWaitFlow")) {
#endif
						CalcWaitFlow(true, timedNode.CurrentStep, out wait, out flow);
#if BENCHMARK
					}
#endif
					if (updateValues) {
						lastFlowWaitCalc = curFrame;
						//Log._Debug($"TTL @ {timedNode.NodeId}: updated lastFlowWaitCalc=curFrame={curFrame}");
					}
				} else {
					flow = CurrentFlow;
					wait = CurrentWait;
					//Log._Debug($"TTL @ {timedNode.NodeId}: lastFlowWaitCalc>=curFrame wait={maxWait} flow={minFlow}");
				}

				float newFlow = CurrentFlow;
				float newWait = CurrentWait;

#if DEBUGMETRIC
				newFlow = flow;
				newWait = wait;
#else
				if (ChangeMetric != StepChangeMetric.Default || Single.IsNaN(newFlow)) {
					newFlow = flow;
				} else {
					newFlow = GlobalConfig.Instance.TimedTrafficLights.SmoothingFactor * newFlow + (1f - GlobalConfig.Instance.TimedTrafficLights.SmoothingFactor) * flow; // some smoothing
				}

				if (Single.IsNaN(newWait)) {
					newWait = 0;
				} else if (ChangeMetric != StepChangeMetric.Default) {
					newWait = wait;
				} else {
					newWait = GlobalConfig.Instance.TimedTrafficLights.SmoothingFactor * newWait + (1f - GlobalConfig.Instance.TimedTrafficLights.SmoothingFactor) * wait; // some smoothing
				}
#endif

				// if more cars are waiting than flowing, we change the step
				float metric;
				bool done = ShouldGoToNextStep(newFlow, newWait, out metric);// newWait > 0 && newFlow < newWait;

				//Log._Debug($"TTL @ {timedNode.NodeId}: newWait={newWait} newFlow={newFlow} updateValues={updateValues} stepDone={stepDone} done={done}");

				if (updateValues) {
					CurrentFlow = newFlow;
					CurrentWait = newWait;
					//Log._Debug($"TTL @ {timedNode.NodeId}: updated minFlow=newFlow={minFlow} maxWait=newWait={maxWait}");
				}
#if DEBUG
				//Log.Message("step finished (2) @ " + nodeId);
#endif
				if (updateValues && !stepDone && done) {
					stepDone = done;
					endTransitionStart = getCurrentFrame();
				}
				return done;
			}

			return false;
		}

		public float GetMetric(float flow, float wait) {
			switch (ChangeMetric) {
				case StepChangeMetric.Default:
				default:
					return flow - wait;
				case StepChangeMetric.FirstFlow:
					return flow <= 0 ? 1f : 0f;
				case StepChangeMetric.FirstWait:
					return wait <= 0 ? 1f : 0f;
				case StepChangeMetric.NoFlow:
					return flow > 0 ? 1f : 0f;
				case StepChangeMetric.NoWait:
					return wait > 0 ? 1f : 0f;
			}
		}

		public bool ShouldGoToNextStep(float flow, float wait, out float metric) {
			metric = GetMetric(flow, wait);
			return ChangeMetric == StepChangeMetric.Default ? metric < 0 : metric == 0f;
		}

		/// <summary>
		/// Calculates the current metrics for flowing and waiting vehicles
		/// </summary>
		/// <param name="wait"></param>
		/// <param name="flow"></param>
		/// <returns>true if the values could be calculated, false otherwise</returns>
		public void CalcWaitFlow(bool countOnlyMovingIfGreen, int stepRefIndex, out float wait, out float flow) {
			uint numFlows = 0;
			uint numWaits = 0;
			float curTotalFlow = 0;
			float curTotalWait = 0;

#if DEBUGTTL
			bool debug = GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId;
			if (debug) {
				Log.Warning($"calcWaitFlow: called for node {timedNode.NodeId} @ step {stepRefIndex}");
			}
#else
			bool debug = false;
#endif

			// TODO checking agains getCurrentFrame() is only valid if this is the current step
			if (countOnlyMovingIfGreen && getCurrentFrame() <= startFrame + MinTime + 1) { // during start phase all vehicles on "green" segments are counted as flowing
				countOnlyMovingIfGreen = false;
			}

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
			ISegmentEndManager endMan = Constants.ManagerFactory.SegmentEndManager;
			IVehicleRestrictionsManager restrMan = Constants.ManagerFactory.VehicleRestrictionsManager;

			// loop over all timed traffic lights within the node group
			foreach (ushort timedNodeId in timedNode.NodeGroup) {
				if (!tlsMan.TrafficLightSimulations[timedNodeId].IsTimedLight()) {
					continue;
				}

				ITimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[timedNodeId].TimedLight;
				ITimedTrafficLightsStep slaveStep = slaveTTL.GetStep(stepRefIndex);

				// minimum time reached. check traffic! loop over source segments
				uint numNodeFlows = 0;
				uint numNodeWaits = 0;
				float curTotalNodeFlow = 0;
				float curTotalNodeWait = 0;
				foreach (KeyValuePair<ushort, ICustomSegmentLights> e in slaveStep.CustomSegmentLights) {
					var sourceSegmentId = e.Key;
					var segLights = e.Value;

					IDictionary<ushort, ArrowDirection> directions = null;
					if (!slaveTTL.Directions.TryGetValue(sourceSegmentId, out directions)) {
#if DEBUGTTL
						if (debug) {
							Log._Debug($"calcWaitFlow: No arrow directions defined for segment {sourceSegmentId} @ {timedNodeId}");
						}
#endif
						continue;
					}

					// one of the traffic lights at this segment is green: count minimum traffic flowing through
					ISegmentEnd sourceSegmentEnd = endMan.GetSegmentEnd(sourceSegmentId, segLights.StartNode);
					if (sourceSegmentEnd == null) {
						Log.Error($"TimedTrafficLightsStep.calcWaitFlow: No segment end @ seg. {sourceSegmentId} found!");
						continue; // skip invalid segment
					}

					IDictionary<ushort, uint>[] movingVehiclesMetrics = null;
					bool countOnlyMovingIfGreenOnSegment = false;
					if (ChangeMetric == StepChangeMetric.Default) {
						countOnlyMovingIfGreenOnSegment = countOnlyMovingIfGreen;
						if (countOnlyMovingIfGreenOnSegment) {
							Constants.ServiceFactory.NetService.ProcessSegment(sourceSegmentId, delegate (ushort srcSegId, ref NetSegment segment) {
								if (restrMan.IsRailSegment(segment.Info)) {
									countOnlyMovingIfGreenOnSegment = false;
								}
								return true;
							});
						}

						movingVehiclesMetrics =	countOnlyMovingIfGreenOnSegment ? sourceSegmentEnd.MeasureOutgoingVehicles(false, debug) : null;
					}
					IDictionary<ushort, uint>[] allVehiclesMetrics = sourceSegmentEnd.MeasureOutgoingVehicles(true, debug);
					
					ExtVehicleType?[] vehTypeByLaneIndex = segLights.VehicleTypeByLaneIndex;
#if DEBUGTTL
					if (debug) {
						Log._Debug($"calcWaitFlow: Seg. {sourceSegmentId} @ {timedNodeId}, vehTypeByLaneIndex={string.Join(", ", vehTypeByLaneIndex.Select(x => x == null ? "null" : x.ToString()).ToArray())}");
					}
#endif
					uint numSegFlows = 0;
					uint numSegWaits = 0;
					float curTotalSegFlow = 0;
					float curTotalSegWait = 0;
					// loop over source lanes
					for (byte laneIndex = 0; laneIndex < vehTypeByLaneIndex.Length; ++laneIndex) {
						ExtVehicleType? vehicleType = vehTypeByLaneIndex[laneIndex];
						if (vehicleType == null) {
							continue;
						}

						ICustomSegmentLight segLight = segLights.GetCustomLight(laneIndex);
						if (segLight == null) {
#if DEBUGTTL
							Log.Warning($"Timed traffic light step: Failed to get custom light for vehicleType {vehicleType} @ seg. {sourceSegmentId}, node {timedNode.NodeId}!");
#endif
							continue;
						}

						IDictionary<ushort, uint> movingVehiclesMetric = countOnlyMovingIfGreenOnSegment ? movingVehiclesMetrics[laneIndex] : null;
						IDictionary<ushort, uint> allVehiclesMetric = allVehiclesMetrics[laneIndex];
						if (allVehiclesMetrics == null) {
#if DEBUGTTL
							if (debug) {
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: No cars on lane {laneIndex} @ seg. {sourceSegmentId}. Vehicle types: {vehicleType}");
							}
#endif
							continue;
						}

#if DEBUGTTL
						if (debug)
							Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Checking lane {laneIndex} @ seg. {sourceSegmentId}. Vehicle types: {vehicleType}");
#endif

						// loop over target segment: calculate waiting/moving traffic
						uint numLaneFlows = 0;
						uint numLaneWaits = 0;
						uint curTotalLaneFlow = 0;
						uint curTotalLaneWait = 0;
						foreach (KeyValuePair<ushort, uint> f in allVehiclesMetric) {
							ushort targetSegmentId = f.Key;
							uint numVehicles = f.Value;

							ArrowDirection dir;
							if (!directions.TryGetValue(targetSegmentId, out dir)) {
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Direction undefined for target segment {targetSegmentId} @ {timedNodeId}");
								continue;
							}

							uint numMovingVehicles = countOnlyMovingIfGreenOnSegment ? movingVehiclesMetric[f.Key] : numVehicles;

#if DEBUGTTL
							if (debug)
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Total num of cars on seg. {sourceSegmentId}, lane {laneIndex} going to seg. {targetSegmentId}: {numMovingVehicles} (all: {numVehicles})");
#endif

							bool addToFlow = false;
							switch (dir) {
								case ArrowDirection.Turn:
									addToFlow = Constants.ServiceFactory.SimulationService.LeftHandDrive ? segLight.IsRightGreen() : segLight.IsLeftGreen();
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
								curTotalLaneFlow += numMovingVehicles;
								++numLaneFlows;
#if DEBUGTTL
								if (debug)
									Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: ## Vehicles @ lane {laneIndex}, seg. {sourceSegmentId} going to seg. {targetSegmentId}: COUTING as FLOWING -- numMovingVehicles={numMovingVehicles}");
#endif
							} else {
								curTotalLaneWait += numVehicles;
								++numLaneWaits;
#if DEBUGTTL
								if (debug)
									Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: ## Vehicles @ lane {laneIndex}, seg. {sourceSegmentId} going to seg. {targetSegmentId}: COUTING as WAITING -- numVehicles={numVehicles}");
#endif
							}

#if DEBUGTTL
							if (debug)
								Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: >>>>> Vehicles @ lane {laneIndex}, seg. {sourceSegmentId} going to seg. {targetSegmentId}: curTotalLaneFlow={curTotalLaneFlow}, curTotalLaneWait={curTotalLaneWait}, numLaneFlows={numLaneFlows}, numLaneWaits={numLaneWaits}");
#endif
						} // foreach target segment

						float meanLaneFlow = 0;
						if (numLaneFlows > 0) {
							switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
								case FlowWaitCalcMode.Mean:
								default:
									++numSegFlows;
									meanLaneFlow = (float)curTotalLaneFlow / (float)numLaneFlows;
									curTotalSegFlow += meanLaneFlow;
									break;
								case FlowWaitCalcMode.Total:
									numSegFlows += numLaneFlows;
									curTotalSegFlow += curTotalLaneFlow;
									break;
							}
						}

						float meanLaneWait = 0;
						if (numLaneWaits > 0) {
							switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
								case FlowWaitCalcMode.Mean:
								default:
									++numSegWaits;
									meanLaneWait = (float)curTotalLaneWait / (float)numLaneWaits;
									curTotalSegWait += meanLaneWait;
									break;
								case FlowWaitCalcMode.Total:
									numSegWaits += numLaneWaits;
									curTotalSegWait += curTotalLaneWait;
									break;
							}
						}

#if DEBUGTTL
						if (debug)
							Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: >>>> Vehicles @ lane {laneIndex}, seg. {sourceSegmentId}: meanLaneFlow={meanLaneFlow}, meanLaneWait={meanLaneWait} // curTotalSegFlow={curTotalSegFlow}, curTotalSegWait={curTotalSegWait}, numSegFlows={numSegFlows}, numSegWaits={numSegWaits}");
#endif

					} // foreach source lane

					float meanSegFlow = 0;
					if (numSegFlows > 0) {
						switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
							case FlowWaitCalcMode.Mean:
							default:
								++numNodeFlows;
								meanSegFlow = (float)curTotalSegFlow / (float)numSegFlows;
								curTotalNodeFlow += meanSegFlow;
								break;
							case FlowWaitCalcMode.Total:
								numNodeFlows += numSegFlows;
								curTotalNodeFlow += curTotalSegFlow;
								break;
						}
					}

					float meanSegWait = 0;
					if (numSegWaits > 0) {
						switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
							case FlowWaitCalcMode.Mean:
							default:
								++numNodeWaits;
								meanSegWait = (float)curTotalSegWait / (float)numSegWaits;
								curTotalNodeWait += meanSegWait;
								break;
							case FlowWaitCalcMode.Total:
								numNodeWaits += numSegWaits;
								curTotalNodeWait += curTotalSegWait;
								break;
						}
					}

#if DEBUGTTL
					if (debug)
						Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: >>> Vehicles @ seg. {sourceSegmentId}: meanSegFlow={meanSegFlow}, meanSegWait={meanSegWait} // curTotalNodeFlow={curTotalNodeFlow}, curTotalNodeWait={curTotalNodeWait}, numNodeFlows={numNodeFlows}, numNodeWaits={numNodeWaits}");
#endif

				} // foreach source segment

				float meanNodeFlow = 0;
				if (numNodeFlows > 0) {
					switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
						case FlowWaitCalcMode.Mean:
						default:
							++numFlows;
							meanNodeFlow = (float)curTotalNodeFlow / (float)numNodeFlows;
							curTotalFlow += meanNodeFlow;
							break;
						case FlowWaitCalcMode.Total:
							numFlows += numNodeFlows;
							curTotalFlow += curTotalNodeFlow;
							break;
					}
				}

				float meanNodeWait = 0;
				if (numNodeWaits > 0) {
					switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
						case FlowWaitCalcMode.Mean:
						default:
							++numWaits;
							meanNodeWait = (float)curTotalNodeWait / (float)numNodeWaits;
							curTotalWait += meanNodeWait;
							break;
						case FlowWaitCalcMode.Total:
							numWaits += numNodeWaits;
							curTotalWait += curTotalNodeWait;
							break;
					}
				}

#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: Calculated flow for source node {timedNodeId}: meanNodeFlow={meanNodeFlow} meanNodeWait={meanNodeWait} // curTotalFlow={curTotalFlow}, curTotalWait={curTotalWait}, numFlows={numFlows}, numWaits={numWaits}");
#endif
			} // foreach timed node

			float meanFlow = numFlows > 0 ? (float)curTotalFlow / (float)numFlows : 0;
			float meanWait = numWaits > 0 ? (float)curTotalWait / (float)numWaits : 0;
			meanFlow /= WaitFlowBalance; // a value smaller than 1 rewards steady traffic currents

			wait = (float)meanWait;
			flow = meanFlow;

#if DEBUGTTL
			if (debug)
				Log._Debug($"TimedTrafficLightsStep.calcWaitFlow: ***CALCULATION FINISHED*** for master node {timedNode.NodeId}: flow={flow} wait={wait}");
#endif
		}

		internal void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, LightMode mode) {
			ICustomSegmentLight light = CustomSegmentLights[segmentId].GetCustomLight(vehicleType);
			if (light != null) {
				light.CurrentMode = mode;
			}
		}

		public ICustomSegmentLights RemoveSegmentLights(ushort segmentId) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.RemoveSegmentLights({segmentId}) called.");
#endif

			ICustomSegmentLights ret = null;
			if (CustomSegmentLights.TryGetValue(segmentId, out ret)) {
				CustomSegmentLights.Remove(segmentId);
			}
			return ret;
		}

		public ICustomSegmentLights GetSegmentLights(ushort segmentId) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.GetSegmentLights({segmentId}) called.");
#endif

			return GetSegmentLights(timedNode.NodeId, segmentId);
		}

		public ICustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.GetSegmentLights({nodeId}, {segmentId}) called.");
#endif

			if (nodeId != timedNode.NodeId) {
				Log.Warning($"TimedTrafficLightsStep.GetSegmentLights({nodeId}, {segmentId}): TTL @ node {timedNode.NodeId} does not handle custom traffic lights for node {nodeId}");
				return null;
			}

			ICustomSegmentLights customLights;
			if (CustomSegmentLights.TryGetValue(segmentId, out customLights)) {
				return customLights;
			} else {
				Log.Info($"TimedTrafficLightsStep.GetSegmentLights({nodeId}, {segmentId}): TTL @ node {timedNode.NodeId} does not know segment {segmentId}");
				return null;
			}
		}

		public bool RelocateSegmentLights(ushort sourceSegmentId, ushort targetSegmentId) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, {targetSegmentId}) called.");
#endif

			ICustomSegmentLights sourceLights = null;
			if (! CustomSegmentLights.TryGetValue(sourceSegmentId, out sourceLights)) {
				Log.Error($"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, {targetSegmentId}): Timed traffic light does not know source segment {sourceSegmentId}. Cannot relocate to {targetSegmentId}.");
				return false;
			}

			SegmentGeometry segGeo = SegmentGeometry.Get(targetSegmentId);
			if (segGeo == null) {
				Log.Error($"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, {targetSegmentId}): No geometry information available for target segment {targetSegmentId}");
				return false;
			}

			if (segGeo.StartNodeId() != timedNode.NodeId && segGeo.EndNodeId() != timedNode.NodeId) {
				Log.Error($"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, {targetSegmentId}): Target segment {targetSegmentId} is not connected to node {timedNode.NodeId}");
				return false;
			}

			bool startNode = segGeo.StartNodeId() == timedNode.NodeId;
			CustomSegmentLights.Remove(sourceSegmentId);
			Constants.ManagerFactory.CustomSegmentLightsManager.GetOrLiveSegmentLights(targetSegmentId, startNode).Housekeeping(true, true);
			sourceLights.Relocate(targetSegmentId, startNode, this);
			CustomSegmentLights[targetSegmentId] = sourceLights;

			Log._Debug($"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, {targetSegmentId}): Relocated lights: {sourceSegmentId} -> {targetSegmentId} @ node {timedNode.NodeId}");
			return true;
		}

		/// <summary>
		/// Adds a new segment to this step. It is cloned from the live custom traffic light.
		/// </summary>
		/// <param name="segmentId"></param>
		internal bool AddSegment(ushort segmentId, bool startNode, bool makeRed) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.AddSegment({segmentId}, {startNode}, {makeRed}) called @ node {timedNode.NodeId}.");
#endif

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"TimedTrafficLightsStep.AddSegment({segmentId}, {startNode}, {makeRed}): No geometry information available for segment {segmentId}");
				return false;
			}

			SegmentEndGeometry endGeo = segGeo.GetEnd(startNode);
			if (endGeo == null) {
				Log.Error($"TimedTrafficLightsStep.AddSegment({segmentId}, {startNode}, {makeRed}): No end geometry information available for segment {segmentId} @ {startNode}");
				return false;
			}

			if (endGeo.NodeId() != timedNode.NodeId) {
				Log.Error($"TimedTrafficLightsStep.AddSegment({segmentId}, {startNode}, {makeRed}): Segment {segmentId} is not connected to node {timedNode.NodeId} @ start {startNode}");
				return false;
			}

			ICustomSegmentLightsManager customSegLightsMan = Constants.ManagerFactory.CustomSegmentLightsManager;

			ICustomSegmentLights liveLights = customSegLightsMan.GetOrLiveSegmentLights(segmentId, startNode);
			liveLights.Housekeeping(true, true);

			ICustomSegmentLights clonedLights = liveLights.Clone(this);

			CustomSegmentLights.Add(segmentId, clonedLights);
			if (makeRed)
				CustomSegmentLights[segmentId].MakeRed();
			else
				CustomSegmentLights[segmentId].MakeRedOrGreen();
			return customSegLightsMan.ApplyLightModes(segmentId, startNode, clonedLights);
		}

		public bool SetSegmentLights(ushort nodeId, ushort segmentId, ICustomSegmentLights lights) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.SetSegmentLights({nodeId}, {segmentId}, {lights}) called.");
#endif

			if (nodeId != timedNode.NodeId) {
				Log.Warning($"TimedTrafficLightsStep.SetSegmentLights({nodeId}, {segmentId}, {lights}): TTL @ node {timedNode.NodeId} does not handle custom traffic lights for node {nodeId}");
				return false;
			}

			return SetSegmentLights(segmentId, lights);
		}

		public bool SetSegmentLights(ushort segmentId, ICustomSegmentLights lights) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.SetSegmentLights({segmentId}, {lights}) called.");
#endif

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
				Log.Error($"TimedTrafficLightsStep.SetSegmentLights: No geometry information available for target segment {segmentId}");
				return false;
			}

			if (segGeo.StartNodeId() != timedNode.NodeId && segGeo.EndNodeId() != timedNode.NodeId) {
				Log.Error($"TimedTrafficLightsStep.SetSegmentLights: Segment {segmentId} is not connected to node {timedNode.NodeId}");
				return false;
			}

			bool startNode = segGeo.StartNodeId() == timedNode.NodeId;
			Constants.ManagerFactory.CustomSegmentLightsManager.GetOrLiveSegmentLights(segmentId, startNode).Housekeeping(true, true);
			lights.Relocate(segmentId, startNode, this);
			CustomSegmentLights[segmentId] = lights;
			Log._Debug($"TimedTrafficLightsStep.SetSegmentLights: Set lights @ seg. {segmentId}, node {timedNode.NodeId}");
			return true;
		}

		public short ClockwiseIndexOfSegmentEnd(ISegmentEndId endId) {
#if DEBUGTTL
			if (GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == timedNode.NodeId)
				Log._Debug($"TimedTrafficLightsStep.ClockwiseIndexOfSegmentEnd({endId}) called.");
#endif
			SegmentEndGeometry endGeo = SegmentEndGeometry.Get(endId);

			if (endGeo == null) {
				Log.Warning($"TimedTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {timedNode.NodeId}: No segment end geometry found for end id {endId}");
				return -1;
			}

			if (endGeo.NodeId() != timedNode.NodeId) {
				Log.Warning($"TimedTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {timedNode.NodeId} does not handle custom traffic lights for node {endGeo.NodeId()}");
				return -1;
			}

			if (CustomSegmentLights.ContainsKey(endId.SegmentId)) {
				Log.Warning($"TimedTrafficLightsStep.ClockwiseIndexOfSegmentEnd: @ node {timedNode.NodeId} does not handle custom traffic lights for segment {endId.SegmentId}");
				return -1;
			}

			short index = Constants.ManagerFactory.CustomSegmentLightsManager.ClockwiseIndexOfSegmentEnd(endId);
			index += timedNode.RotationOffset;
			return (short)(index % (endGeo.NumConnectedSegments + 1));
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		public ICustomSegmentLights GetSegmentLights(ushort segmentId, bool startNode, bool add = true, RoadBaseAI.TrafficLightState lightState = RoadBaseAI.TrafficLightState.Red) {
			throw new NotImplementedException();
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		public ICustomSegmentLights GetOrLiveSegmentLights(ushort segmentId, bool startNode) {
			throw new NotImplementedException();
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		public bool ApplyLightModes(ushort segmentId, bool startNode, ICustomSegmentLights otherLights) {
			throw new NotImplementedException();
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		public void SetLightMode(ushort segmentId, bool startNode, ExtVehicleType vehicleType, LightMode mode) {
			throw new NotImplementedException();
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		public void AddNodeLights(ushort nodeId) {
			throw new NotImplementedException();
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		public void RemoveNodeLights(ushort nodeId) {
			throw new NotImplementedException();
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		void ICustomSegmentLightsManager.RemoveSegmentLights(ushort segmentId) {
			throw new NotImplementedException();
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		public void RemoveSegmentLight(ushort segmentId, bool startNode) {
			throw new NotImplementedException();
		}

		// TODO IMPROVE THIS! Liskov substitution principle must hold.
		public bool IsSegmentLight(ushort segmentId, bool startNode) {
			throw new NotImplementedException();
		}
	}
}
