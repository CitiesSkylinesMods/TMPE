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
using TrafficManager.Traffic.Enums;

/// <summary>
/// A segment end describes a directional traffic segment connected to a controlled node
/// (having custom traffic lights or priority signs).
/// </summary>
namespace TrafficManager.Traffic.Impl {
	using API.Traffic.Data;
	using API.Traffic.Enums;

	[Obsolete("should be removed when implementing issue #240")]
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
		//public ushort FirstRegisteredVehicleId { get; set; } = 0; // TODO private set

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
				"\t" + $"cleanupRequested = {cleanupRequested}\n" +
				"\t" + $"numVehiclesMovingToSegmentId = " + (numVehiclesMovingToSegmentId == null ? "<null>" : numVehiclesMovingToSegmentId.ArrayToString()) + "\n" +
				"\t" + $"numVehiclesGoingToSegmentId = " + (numVehiclesGoingToSegmentId == null ? "<null>" : numVehiclesGoingToSegmentId.ArrayToString()) + "\n" +
				"SegmentEnd]";
		}

		public SegmentEnd(ushort segmentId, bool startNode) : base(segmentId, startNode) {
			Update();
		}

		~SegmentEnd() {
			//Destroy();
		}

		/// <summary>
		/// Calculates for each segment the number of cars going to this segment.
		/// We use integer arithmetic for better performance.
		/// </summary>
		public IDictionary<ushort, uint>[] MeasureOutgoingVehicles(bool includeStopped=true, bool debug = false) {
			//VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			//NetManager netManager = Singleton<NetManager>.instance;
			ExtVehicleManager vehStateManager = ExtVehicleManager.Instance;
			IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

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

			int endIndex = segEndMan.GetIndex(SegmentId, StartNode);
			ushort vehicleId = segEndMan.ExtSegmentEnds[endIndex].firstVehicleId;
			int numProcessed = 0;
			int numIter = 0;
			while (vehicleId != 0) {
				Constants.ServiceFactory.VehicleService.ProcessVehicle(vehicleId, delegate (ushort vId, ref Vehicle veh) {
					MeasureOutgoingVehicle(debug, ret, includeStopped, avgSegLen, vehicleId, ref veh, ref vehStateManager.ExtVehicles[vehicleId], ref numProcessed);
					return true;
				});
				vehicleId = vehStateManager.ExtVehicles[vehicleId].nextVehicleIdOnSegment;

				if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"GetVehicleMetricGoingToSegment: Calculation completed. {string.Join(", ", ret.Select(e => "[" + string.Join(", ", e.Select(x => x.Key.ToString() + "=" + x.Value.ToString()).ToArray()) + "]").ToArray())}");
#endif
			return ret;
		}

		protected void MeasureOutgoingVehicle(bool debug, IDictionary<ushort, uint>[] ret, bool includeStopped, uint avgSegmentLength, ushort vehicleId, ref Vehicle vehicle, ref ExtVehicle state, ref int numProcessed) {
#if DEBUGMETRIC
			if (debug)
				Log._Debug($" MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId} (start={StartNode})) Checking vehicle {vehicleId}. Coming from seg. {state.currentSegmentId}, start {state.currentStartNode}, lane {state.currentLaneIndex} going to seg. {state.nextSegmentId}, lane {state.nextLaneIndex}");
#endif

			if ((state.flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
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

			if (!includeStopped && vehicle.GetLastFrameVelocity().sqrMagnitude < GlobalConfig.Instance.PriorityRules.MaxStopVelocity * GlobalConfig.Instance.PriorityRules.MaxStopVelocity) {
#if DEBUGMETRIC
				if (debug)
					Log._Debug($"  MeasureOutgoingVehicle: (Segment {SegmentId}, Node {NodeId}) Vehicle {vehicleId}: too slow ({vehicle.GetLastFrameVelocity().sqrMagnitude})");
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

		public uint GetRegisteredVehicleCount() {
			IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
			return segEndMan.GetRegisteredVehicleCount(ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(SegmentId, StartNode)]);
		}

		/*public void Destroy() {
			UnregisterAllVehicles();
		}

		private void UnregisterAllVehicles() {
			ExtVehicleManager extVehicleMan = ExtVehicleManager.Instance;
			IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

			int endIndex = segEndMan.GetIndex(SegmentId, StartNode);
			int numIter = 0;
			while (segEndMan.ExtSegmentEnds[endIndex].firstVehicleId != 0) {
				extVehicleMan.Unlink(ref extVehicleMan.ExtVehicles[segEndMan.ExtSegmentEnds[endIndex].firstVehicleId]);
				if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
		}*/

		public void Update() {
			Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segmentId, ref NetSegment segment) {
				StartNode = segment.m_startNode == NodeId;
				numLanes = segment.Info.m_lanes.Length;
				return true;
			});

			if (!Constants.ServiceFactory.NetService.IsSegmentValid(SegmentId)) {
				Log.Error($"SegmentEnd.Update: Segment {SegmentId} is invalid.");
				return;
			}

			Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
				RebuildVehicleNumDicts(ref node);
				return true;
			});
		}

		private void RebuildVehicleNumDicts(ref NetNode node) {
			numVehiclesMovingToSegmentId = new TinyDictionary<ushort, uint>[numLanes];
			numVehiclesGoingToSegmentId = new TinyDictionary<ushort, uint>[numLanes];

			Constants.ServiceFactory.NetService.IterateSegmentLanes(SegmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segmentId, ref NetSegment segment, byte laneIndex) {
				IDictionary<ushort, uint> numVehicleMoving = new TinyDictionary<ushort, uint>();
				IDictionary<ushort, uint> numVehicleGoing = new TinyDictionary<ushort, uint>();

				numVehiclesMovingToSegmentId[laneIndex] = numVehicleMoving;
				numVehiclesGoingToSegmentId[laneIndex] = numVehicleGoing;

				return true;
			});

			IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

			for (int i = 0; i < 8; ++i) {
				ushort segId = node.GetSegment(i);
				if (segId == 0) {
					continue;
				}

				if (!segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segId, (bool)Constants.ServiceFactory.NetService.IsStartNode(segId, NodeId))].outgoing) {
					continue;
				}

				foreach (TinyDictionary<ushort, uint> numVehiclesMovingToSegId in numVehiclesMovingToSegmentId) {
					numVehiclesMovingToSegId[segId] = 0;
				}

				foreach (TinyDictionary<ushort, uint> numVehiclesGoingToSegId in numVehiclesGoingToSegmentId) {
					numVehiclesGoingToSegId[segId] = 0;
				}
			}
		}
	}
}
