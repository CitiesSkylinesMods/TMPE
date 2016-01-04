using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	public class TimedTrafficStep : ICloneable {
		public ushort nodeId;
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
		private int endTransitionStart;

		/// <summary>
		/// minimum mean "number of cars passing through" / "average segment length"
		/// </summary>
		public float minFlow;
		/// <summary>
		///	maximum mean "number of cars waiting for green" / "average segment length"
		/// </summary>
		public float maxWait;

		/// <summary>
		/// In case the traffic light is set for a group of nodes, the master node decides
		/// if all member steps are done. If it is `null` then we are the master node.
		/// </summary>
		private ushort? masterNodeId;
		private List<ushort> groupNodeIds;
		private TrafficLightsTimed timedNode;

		public Dictionary<ushort, ManualSegmentLight> segmentLightStates = new Dictionary<ushort, ManualSegmentLight>();

		/// <summary>
		/// Maximum segment length
		/// </summary>
		float maxSegmentLength = 0f;

		private bool invalid = false; // TODO rework

		private static readonly float?[] speedsToLookup = new float?[] {null, 0.1f};

		public TimedTrafficStep(int minTime, int maxTime, ushort nodeId, ushort masterNodeId, List<ushort> groupNodeIds) {
			this.nodeId = nodeId;
			this.minTime = minTime;
			this.maxTime = maxTime;
			this.timedNode = TrafficLightsTimed.GetTimedLight(nodeId);

			if (nodeId == masterNodeId)
				this.masterNodeId = null;
			else
				this.masterNodeId = masterNodeId;
			this.groupNodeIds = groupNodeIds;

			var node = TrafficLightTool.GetNetNode(nodeId);
			minFlow = Single.NaN;
			maxWait = Single.NaN;

			endTransitionStart = -1;
			stepDone = false;

			for (var s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);
				if (segmentId <= 0)
					continue;

				addSegment(segmentId);
			}
			calcMaxSegmentLength();
		}

		internal void calcMaxSegmentLength() {
			var node = TrafficLightTool.GetNetNode(nodeId);

			maxSegmentLength = 0;
			for (var s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);
				
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
			return endTransitionStart > -1 && getCurrentFrame() > endTransitionStart && StepDone();
		}

		/// <summary>
		/// Checks if the green-to-red (=yellow) phase is currently active
		/// </summary>
		/// <returns></returns>
		internal bool isInEndTransition() {
			return endTransitionStart > -1 && getCurrentFrame() <= endTransitionStart && StepDone();
		}

		internal bool isInStartTransition() {
			return getCurrentFrame() == startFrame && !StepDone();
		}

		public RoadBaseAI.TrafficLightState GetLight(ushort segment, int lightType) {
			ManualSegmentLight segLight = segmentLightStates[segment];
			if (segLight != null) {
				switch (lightType) {
					case 0:
						return segLight.LightMain;
					case 1:
						return segLight.LightLeft;
					case 2:
						return segLight.LightRight;
					case 3:
						return segLight.LightPedestrian;
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
			this.endTransitionStart = -1;
			minFlow = Single.NaN;
			maxWait = Single.NaN;
		}

		private uint getCurrentFrame() {
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

				TimedTrafficStep previousStep = timedNode.Steps[(timedNode.CurrentStep + timedNode.Steps.Count - 1) % timedNode.Steps.Count];
				TimedTrafficStep nextStep = timedNode.Steps[(timedNode.CurrentStep + 1) % timedNode.Steps.Count];

				foreach (KeyValuePair<ushort, ManualSegmentLight> e in segmentLightStates) {
					var segmentId = e.Key;
					var segLightState = e.Value;
					var prevLightState = previousStep.segmentLightStates[segmentId];
					var nextLightState = nextStep.segmentLightStates[segmentId];

					var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segmentId);
					if (segmentLight == null)
						continue;

					segmentLight.LightMain = calcLightState(prevLightState.LightMain, segLightState.LightMain, nextLightState.LightMain, atStartTransition, atEndTransition);
					segmentLight.LightLeft = calcLightState(prevLightState.LightLeft, segLightState.LightLeft, nextLightState.LightLeft, atStartTransition, atEndTransition);
					segmentLight.LightRight = calcLightState(prevLightState.LightRight, segLightState.LightRight, nextLightState.LightRight, atStartTransition, atEndTransition);
					segmentLight.LightPedestrian = calcLightState(prevLightState.LightPedestrian, segLightState.LightPedestrian, nextLightState.LightPedestrian, atStartTransition, atEndTransition);

					segmentLight.UpdateVisuals();
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
		internal void addSegment(ushort segmentId) {
			segmentLightStates.Add(segmentId, (ManualSegmentLight)TrafficLightsManual.GetOrLiveSegmentLight(nodeId, segmentId).Clone());
			segmentLightStates[segmentId].makeRedOrGreen();
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
			foreach (KeyValuePair<ushort, ManualSegmentLight> e in segmentLightStates) {
				var segmentId = e.Key;
				var segLightState = e.Value;
				
				//if (segment == 0) continue;
				var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segmentId);
				if (segmentLight == null)
					continue;

				segLightState.LightMain = segmentLight.LightMain;
				segLightState.LightLeft = segmentLight.LightLeft;
				segLightState.LightRight = segmentLight.LightRight;
				segLightState.LightPedestrian = segmentLight.LightPedestrian;
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

		public bool StepDone() {
			if (stepDone)
				return true;

			if (startFrame + maxTime <= getCurrentFrame()) {
				// maximum time reached. switch!
#if DEBUG
				//Log.Message("step finished @ " + nodeId);
#endif
				stepDone = true;
				endTransitionStart = (int)getCurrentFrame();
				return stepDone;
			}

			if (startFrame + minTime <= getCurrentFrame()) {
				if (masterNodeId != null && TrafficLightsTimed.IsTimedLight((ushort)masterNodeId)) {
					TrafficLightsTimed masterTimedNode = TrafficLightsTimed.GetTimedLight((ushort)masterNodeId);
					bool done = masterTimedNode.Steps[masterTimedNode.CurrentStep].StepDone();
#if DEBUG
					//Log.Message("step finished (1) @ " + nodeId);
#endif
					stepDone = done;
					if (stepDone)
						endTransitionStart = (int)getCurrentFrame();
					return stepDone;
				} else {
					int numFlows = 0;
					int numWaits = 0;
					float curMeanFlow = 0;
					float curMeanWait = 0;

					// we are the master node. calculate traffic data
					foreach (ushort timedNodeId in groupNodeIds) {
						if (!TrafficLightsTimed.IsTimedLight(timedNodeId))
							continue;
						TrafficLightsTimed slaveTimedNode = TrafficLightsTimed.GetTimedLight(timedNodeId);
						TimedTrafficStep slaveStep = slaveTimedNode.Steps[timedNode.CurrentStep];

						//List<int> segmentIdsToDelete = new List<int>();

						// minimum time reached. check traffic!
						foreach (KeyValuePair<ushort, ManualSegmentLight> e in slaveStep.segmentLightStates) {
							var fromSegmentId = e.Key;
							var segLightState = e.Value;

							// one of the traffic lights at this segment is green: count minimum traffic flowing through
							PrioritySegment fromSeg = TrafficPriority.GetPrioritySegment(timedNodeId, fromSegmentId);
							if (fromSeg == null) {
								//Log.Warning("stepDone(): prioSeg is null");
								//segmentIdsToDelete.Add(fromSegmentId);
								continue; // skip invalid segment
							}

							bool startPhase = getCurrentFrame() <= startFrame + minTime + 2; // during start phase all vehicles on "green" segments are counted as flowing
							float?[] minSpeeds = startPhase ? new float?[] { null } : speedsToLookup;
							foreach (float? minSpeed in minSpeeds) {
								foreach (KeyValuePair<ushort, float> f in fromSeg.getNumCarsGoingToSegment(minSpeed)) {
									var toSegmentId = f.Key;
									var totalNormCarLength = f.Value;

									TrafficPriority.Direction dir = TrafficPriority.GetDirection(fromSegmentId, toSegmentId, timedNodeId);
									bool addToFlow = false;
									switch (dir) {
										case TrafficPriority.Direction.Left:
											if (segLightState.isLeftGreen())
												addToFlow = true;
											break;
										case TrafficPriority.Direction.Right:
											if (segLightState.isRightGreen())
												addToFlow = true;
											break;
										case TrafficPriority.Direction.Forward:
										default:
											if (segLightState.isForwardGreen())
												addToFlow = true;
											break;
									}

									if (addToFlow) {
										if (minSpeed != null || startPhase) {
											++numFlows;
											curMeanFlow += (float)totalNormCarLength/* * segmentWeight*/;
										}
									} else {
										if (minSpeed == null) {
											++numWaits;
											curMeanWait += (float)totalNormCarLength/* * segmentWeight*/;
										}
									}
								}
							}
						}

						// delete invalid segments from step
						/*foreach (int segmentId in segmentIdsToDelete) {
							slaveStep.segmentLightStates.Remove(segmentId);
						}*/

						if (slaveStep.segmentLightStates.Count <= 0) {
							invalid = true;
							return true;
						}
					}

					if (numFlows > 0)
						curMeanFlow /= (float)numFlows;
					if (numWaits > 0)
						curMeanWait /= (float)numWaits;

					/*float decisionValue = 0.75f; // a value smaller than 1 rewards steady traffic currents
					curMeanFlow /= decisionValue;*/

					if (Single.IsNaN(minFlow))
						minFlow = curMeanFlow;
					else
						minFlow = 0.5f * minFlow + 0.5f * curMeanFlow; // some smoothing

					if (Single.IsNaN(maxWait))
						maxWait = 0;
					else
						maxWait = 0.5f * maxWait + 0.5f * curMeanWait; // some smoothing

					// if more cars are waiting than flowing, we change the step
					bool done = maxWait > 0 && minFlow < maxWait;
#if DEBUG
					//Log.Message("step finished (2) @ " + nodeId);
#endif
					stepDone = done;
					if (stepDone)
						endTransitionStart = (int)getCurrentFrame();
					return stepDone;
				}
			}
			return false;
		}

		/// <summary>
		/// Calculates the peak (minimum or maximum) flow for a previously known flow (old number of cars)
		/// and current flow (current number of cars)
		/// </summary>
		/// <param name="oldFlow"></param>
		/// <param name="currentFlow"></param>
		/// <param name="minimum"></param>
		/// <returns></returns>
		private int calcPeakFlow(int oldFlow, int currentFlow, bool minimum) {
			if (oldFlow < 0)
				return currentFlow; // initialization of minimum/maximum

			if (minimum)
				return Math.Min(oldFlow, currentFlow);
			else
				return Math.Max(oldFlow, currentFlow);
		}

		public object Clone() {
			return MemberwiseClone();
		}
	}
}
