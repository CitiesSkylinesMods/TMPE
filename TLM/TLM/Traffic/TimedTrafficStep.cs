using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;

namespace TrafficManager.Traffic {
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

		public Dictionary<int, ManualSegmentLight> segmentLightStates = new Dictionary<int, ManualSegmentLight>();
		/// <summary>
		/// list of segment ids connected to the node
		/// </summary>
		public List<int> segmentIds = new List<int>();

		/// <summary>
		/// Maximum segment length
		/// </summary>
		float maxSegmentLength = 0f;

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

			for (var s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);
				float segLength = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_averageLength;
				if (segLength > maxSegmentLength)
					maxSegmentLength = segLength;

				if (segmentId != 0) {
					segmentIds.Add(segmentId);
					var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segmentId);
					if (segmentLight != null)
						segmentLightStates[segmentId] = (ManualSegmentLight)segmentLight.Clone();
				}
			}
		}

		public RoadBaseAI.TrafficLightState GetLight(int segment, int lightType) {
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
		/// <param name="startFrame">current time frame >> 6</param>
		public void Start(uint startFrame) {
			this.startFrame = startFrame;
			foreach (int segmentId in segmentIds) {
				minFlow = Single.NaN;
				maxWait = Single.NaN;
			}
		}

		/// <summary>
		/// Updates "real-world" traffic light states according to the timed scripts
		/// </summary>
		public void SetLights() {
			foreach (KeyValuePair<int, ManualSegmentLight> e in segmentLightStates) {
				var segmentId = e.Key;
				var segLightState = e.Value;
				//if (segment == 0) continue;

				var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segmentId);
				if (segmentLight == null)
					continue;

				segmentLight.LightMain = segLightState.LightMain;
				segmentLight.LightLeft = segLightState.LightLeft;
				segmentLight.LightRight = segLightState.LightRight;
				segmentLight.LightPedestrian = segLightState.LightPedestrian;
				segmentLight.UpdateVisuals();
			}
		}

		/// <summary>
		/// Updates timed segment lights according to "real-world" traffic light states
		/// </summary>
		public void UpdateLights() {
			foreach (KeyValuePair<int, ManualSegmentLight> e in segmentLightStates) {
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
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			return Math.Max(0, startFrame + minTime - (currentFrameIndex >> 6));
		}

		/// <summary>
		/// Countdown value for max. time
		/// </summary>
		/// <returns></returns>
		public long MaxTimeRemaining() {
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			return Math.Max(0, startFrame + maxTime - (currentFrameIndex >> 6));
		}

		public bool StepDone(uint frame) {
			if (startFrame + maxTime <= frame) {
				// maximum time reached. switch!
				return true;
			}

			if (startFrame + minTime <= frame) {
				int numFlows = 0;
				int numWaits = 0;
				float curMeanFlow = 0;
				float curMeanWait = 0;

				if (masterNodeId != null && TrafficLightsTimed.IsTimedLight((ushort)masterNodeId)) {
					TrafficLightsTimed masterTimedNode = TrafficLightsTimed.GetTimedLight((ushort)masterNodeId);
					return masterTimedNode.Steps[masterTimedNode.CurrentStep].StepDone(frame);
				} else {
					// we are the master node. calculate traffic data
					foreach (ushort timedNodeId in groupNodeIds) {
						if (!TrafficLightsTimed.IsTimedLight(timedNodeId))
							continue;
						TrafficLightsTimed slaveTimedNode = TrafficLightsTimed.GetTimedLight(timedNodeId);
						TimedTrafficStep slaveStep = slaveTimedNode.Steps[timedNode.CurrentStep];

						// minimum time reached. check traffic!
						foreach (KeyValuePair<int, ManualSegmentLight> e in slaveStep.segmentLightStates) {
							var fromSegmentId = e.Key;
							var segLightState = e.Value;
							float segmentWeight = Singleton<NetManager>.instance.m_segments.m_buffer[fromSegmentId].m_averageLength / maxSegmentLength;

							// one of the traffic lights at this segment is green: count minimum traffic flowing through
							PrioritySegment prioSeg = TrafficPriority.GetPrioritySegment(timedNodeId, fromSegmentId);
							foreach (KeyValuePair<ushort, int> f in prioSeg.numCarsGoingToSegmentId) {
								var toSegmentId = f.Key;
								var numCars = f.Value;

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
									++numFlows;
									curMeanFlow += (float)numCars * segmentWeight;
								} else {
									++numWaits;
									curMeanWait += (float)numCars * segmentWeight;
								}
							}
						}
					}

					if (numFlows > 0)
						curMeanFlow /= (float)numFlows;
					if (numWaits > 0)
						curMeanWait /= (float)numWaits;

					if (Single.IsNaN(minFlow))
						minFlow = curMeanFlow;
					else
						minFlow = Math.Min(curMeanFlow, minFlow);

					if (Single.IsNaN(maxWait))
						maxWait = curMeanWait;
					else
						maxWait = Math.Max(curMeanWait, maxWait);

					// if double as many cars are waiting than flowing, we change the step
					return maxWait > 0 && minFlow * 2f < maxWait;
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
