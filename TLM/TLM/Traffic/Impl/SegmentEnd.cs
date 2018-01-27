#define DEBUGFRONTVEHx
#define DEBUGREGx
#define DEBUGMETRIC
#define DEBUGMETRIC2x

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.AI;
using TrafficManager.Util;
using System.Threading;
using TrafficManager.State;
using TrafficManager.UI;
using TrafficManager.Manager;
using System.Linq;
using CSUtil.Commons;
using TrafficManager.Geometry.Impl;
using TrafficManager.Manager.Impl;
using TrafficManager.Traffic.Data;

/// <summary>
/// A segment end describes a directional traffic segment connected to a controlled node
/// (having custom traffic lights or priority signs).
/// </summary>
namespace TrafficManager.Traffic.Impl {
	public class SegmentEnd : SegmentEndId, ISegmentEnd {
		// TODO convert to struct

		[Obsolete]
		public ushort NodeId {
			get {
				return Constants.ServiceFactory.NetService.GetSegmentNodeId(SegmentId, StartNode);
			}
		}

		private int numLanes = 0;

		/// <summary>
		/// Vehicles that are traversing or will traverse this segment
		/// </summary>
		public ushort FirstRegisteredVehicleId { get; set; } = 0; // TODO private set

		private bool cleanupRequested = false;

		/// <summary>
		/// Vehicles that are traversing or will traverse this segment
		/// </summary>
		//private ushort[] frontVehicleIds;

		/// <summary>
		/// Number of vehicles / vehicle length going to a certain segment.
		/// First key: source lane index, second key: target segment id, value: total normalized vehicle length
		/// </summary>
		private IDictionary<ushort, uint>[] numVehiclesMovingToSegmentId; // minimum speed required
		private IDictionary<ushort, uint>[] numVehiclesGoingToSegmentId; // no minimum speed required

		public override string ToString() {
			return $"[SegmentEnd {base.ToString()}\n" +
				"\t" + $"NodeId = {NodeId}\n" +
				"\t" + $"numLanes = {numLanes}\n" +
				"\t" + $"FirstRegisteredVehicleId = {FirstRegisteredVehicleId}\n" +
				"\t" + $"cleanupRequested = {cleanupRequested}\n" +
				"\t" + $"numVehiclesMovingToSegmentId = " + (numVehiclesMovingToSegmentId == null ? "<null>" : numVehiclesMovingToSegmentId.ArrayToString()) + "\n" +
				"\t" + $"numVehiclesGoingToSegmentId = " + (numVehiclesGoingToSegmentId == null ? "<null>" : numVehiclesGoingToSegmentId.ArrayToString()) + "\n" +
				"SegmentEnd]";
		}

		public SegmentEnd(ushort segmentId, bool startNode) : base(segmentId, startNode) {
			FirstRegisteredVehicleId = 0;
			Update();
		}
		
		~SegmentEnd() {
			Destroy();
		}

		/// <summary>
		/// Calculates for each segment the number of cars going to this segment.
		/// We use integer arithmetic for better performance.
		/// </summary>
		public IDictionary<ushort, uint>[] MeasureOutgoingVehicles(bool includeStopped=true, bool debug = false) {
			//VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			//NetManager netManager = Singleton<NetManager>.instance;
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

			// TODO pre-calculate this
			uint avgSegLen = 0;
			Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segmentId, ref NetSegment segment) {
				avgSegLen = (uint)segment.m_averageLength;
				return true;
			});

			IDictionary<ushort, uint>[] ret = includeStopped ? numVehiclesGoingToSegmentId : numVehiclesMovingToSegmentId;

			// reset
			for (byte laneIndex = 0; laneIndex < ret.Length; ++laneIndex) {
				IDictionary<ushort, uint> laneMetrics = ret[laneIndex];
				foreach (KeyValuePair<ushort, uint> e in laneMetrics) {
					laneMetrics[e.Key] = 0;
				}
			}

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"GetVehicleMetricGoingToSegment: Segment {SegmentId}, Node {NodeId}, includeStopped={includeStopped}.");
#endif

			ushort vehicleId = FirstRegisteredVehicleId;
			int numProcessed = 0;
			while (vehicleId != 0) {
				MeasureOutgoingVehicle(debug, ret, includeStopped, avgSegLen, vehicleId, ref vehStateManager.VehicleStates[vehicleId], ref numProcessed);

				if ((Options.simAccuracy >= 3 && numProcessed >= 3) || (Options.simAccuracy == 2 && numProcessed >= 5) || (Options.simAccuracy == 1 && numProcessed >= 10)) {
					break;
				}

				vehicleId = vehStateManager.VehicleStates[vehicleId].nextVehicleIdOnSegment;
			}

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"GetVehicleMetricGoingToSegment: Calculation completed. {string.Join(", ", ret.Select(e => "[" + string.Join(", ", e.Select(x => x.Key.ToString() + "=" + x.Value.ToString()).ToArray()) + "]").ToArray())}");
#endif
			return ret;
		}

		protected void MeasureOutgoingVehicle(bool debug, IDictionary<ushort, uint>[] ret, bool includeStopped, uint avgSegmentLength, ushort vehicleId, ref VehicleState state, ref int numProcessed) {
#if DEBUGMETRIC
			if (debug)
				Log._Debug($" MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId} (start={StartNode})) Checking vehicle {vehicleId}. Coming from seg. {state.currentSegmentId}, start {state.currentStartNode}, lane {state.currentLaneIndex} going to seg. {state.nextSegmentId}, lane {state.nextLaneIndex}");
#endif

			if ((state.flags & VehicleState.Flags.Spawned) == VehicleState.Flags.None) {
#if DEBUGMETRIC
				if (debug)
					Log._Debug($" MeasureOutgoingVehicle: Vehicle {vehicleId} is unspawned. Ignoring.");
#endif
				return;
			}

#if DEBUGMETRIC
			if (state.currentSegmentId != SegmentId || state.currentStartNode != StartNode) {
				if (debug)
					Log._Debug($" MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId} (start={StartNode})) Vehicle {vehicleId} error: Segment end mismatch! {state.ToString()}");
				//RequestCleanup();
				return;
			}
#endif

			if (state.nextSegmentId == 0) {
#if DEBUGMETRIC
				if (debug)
					Log._Debug($" MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId} (start={StartNode})) Vehicle {vehicleId}: Ignoring vehicle");
#endif
				return;
			}

			if (state.currentLaneIndex >= ret.Length || !ret[state.currentLaneIndex].ContainsKey(state.nextSegmentId)) {
#if DEBUGMETRIC
				if (debug)
					Log._Debug($" MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId} (start={StartNode})) Vehicle {vehicleId} is on lane {state.currentLaneIndex} and wants to go to segment {state.nextSegmentId} but one or both are invalid: {ret.CollectionToString()}");
#endif
				return;
			}

			if (!includeStopped && state.SqrVelocity < TrafficPriorityManager.MAX_SQR_STOP_VELOCITY) {
#if DEBUGMETRIC
				if (debug)
					Log._Debug($"  MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId}) Vehicle {vehicleId}: too slow ({state.SqrVelocity})");
#endif
				++numProcessed;
				return;
			}

			
			uint normLength = 10u;
			if (avgSegmentLength > 0) {
				normLength = Math.Min(100u, (uint)(Math.Max(1u, state.totalLength) * 100u) / avgSegmentLength) + 1; // TODO +1 because the vehicle length calculation for trains/monorail in the method VehicleState.OnVehicleSpawned returns 0 (or a very small number maybe?)
			}

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"  MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId}) NormLength of vehicle {vehicleId}: {state.totalLength} -> {normLength} (avgSegmentLength={avgSegmentLength})");
#endif

			ret[state.currentLaneIndex][state.nextSegmentId] += normLength;
			++numProcessed;

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"  MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId}) Vehicle {vehicleId}: ***ADDED*** ({state.currentSegmentId}@{state.currentLaneIndex} -> {state.nextSegmentId}@{state.nextLaneIndex})!");
#endif

			return;
		}

		public int GetRegisteredVehicleCount() {
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

			ushort vehicleId = FirstRegisteredVehicleId;
			int ret = 0;
			while (vehicleId != 0) {
				++ret;
				vehicleId = vehStateManager.VehicleStates[vehicleId].nextVehicleIdOnSegment;
			}
			return ret;
		}

		public void Destroy() {
			UnregisterAllVehicles();
		}

		private void UnregisterAllVehicles() {
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;
			while (FirstRegisteredVehicleId != 0) {
				vehStateManager.VehicleStates[FirstRegisteredVehicleId].Unlink();
			}
		}

		public void Update() {
			Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate(ushort segmentId, ref NetSegment segment) {
				StartNode = segment.m_startNode == NodeId;
				numLanes = segment.Info.m_lanes.Length;
				return true;
			});
			SegmentGeometry segGeo = SegmentGeometry.Get(SegmentId);

			if (segGeo == null) {
				Log.Error($"SegmentEnd.Update: No geometry information available for segment {SegmentId}");
				return;
			}

			ushort[] outgoingSegmentIds = SegmentGeometry.Get(SegmentId).GetOutgoingSegments(StartNode);
			numVehiclesMovingToSegmentId = new TinyDictionary<ushort, uint>[numLanes];
			numVehiclesGoingToSegmentId = new TinyDictionary<ushort, uint>[numLanes];

			Constants.ServiceFactory.NetService.IterateSegmentLanes(SegmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segmentId, ref NetSegment segment, byte laneIndex) {
				IDictionary<ushort, uint> numVehicleMoving = new TinyDictionary<ushort, uint>();
				IDictionary<ushort, uint> numVehicleGoing = new TinyDictionary<ushort, uint>();

				numVehiclesMovingToSegmentId[laneIndex] = numVehicleMoving;
				numVehiclesGoingToSegmentId[laneIndex] = numVehicleGoing;

				foreach (ushort otherSegmentId in outgoingSegmentIds) {
					numVehicleMoving[otherSegmentId] = 0;
					numVehicleGoing[otherSegmentId] = 0;
				}
				numVehicleMoving[SegmentId] = 0;
				numVehicleGoing[SegmentId] = 0;

				return true;
			});
		}
	}
}
