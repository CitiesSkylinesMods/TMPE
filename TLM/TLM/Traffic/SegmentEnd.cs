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

/// <summary>
/// A segment end describes a directional traffic segment connected to a controlled node
/// (having custom traffic lights or priority signs).
/// </summary>
namespace TrafficManager.Traffic {
	public class SegmentEnd : SegmentEndId {
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
		internal ushort FirstRegisteredVehicleId = 0;

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

		internal void RequestCleanup() {
			cleanupRequested = true;
		}

		internal void SimulationStep() {
			if (cleanupRequested) {
				VehicleManager vehManager = Singleton<VehicleManager>.instance;
				VehicleStateManager vehStateManager = VehicleStateManager.Instance;

#if DEBUG
				//Log._Debug($"Cleanup of SegmentEnd {SegmentId} @ {NodeId} requested. Performing cleanup now.");
#endif
				ushort vehicleId = FirstRegisteredVehicleId;
				while (vehicleId != 0) {
					VehicleState state = vehStateManager._GetVehicleState(vehicleId);

					bool removeVehicle = false;
					if (!state.Valid) {
						removeVehicle = true;
					}
					
					ushort nextVehicleId = state.NextVehicleIdOnSegment;
					if (removeVehicle) {
						state.Unlink();
					}
					vehicleId = nextVehicleId;
				}

				cleanupRequested = false;
			}

			//Housekeeping();
		}

		/// <summary>
		/// Calculates for each segment the number of cars going to this segment.
		/// We use integer arithmetic for better performance.
		/// </summary>
		public IDictionary<ushort, uint>[] MeasureOutgoingVehicles(bool includeStopped=true, bool debug = false) {
			//VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			//NetManager netManager = Singleton<NetManager>.instance;
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

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
				Log._Debug($"GetVehicleMetricGoingToSegment: Segment {SegmentId}, Node {NodeId}.");
#endif

			ushort vehicleId = FirstRegisteredVehicleId;
			int numProcessed = 0;
			while (vehicleId != 0) {
				VehicleState state = vehStateManager._GetVehicleState(vehicleId);

				bool breakLoop = false;

				state.ProcessCurrentAndNextPathPosition(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], delegate (ref Vehicle vehState, ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
					if (!state.CheckValidity(ref vehState)) {
						RequestCleanup();
						return;
					}

#if DEBUGMETRIC
				if (debug)
					Log._Debug($" GetVehicleMetricGoingToSegment: (Segment {SegmentId}, Node {NodeId}) Checking vehicle {vehicleId}. Coming from seg. {curPos.m_segment}, lane {curPos.m_lane}, going to seg. {nextPos.m_segment}, lane {nextPos.m_lane}");
#endif

					if (curPos.m_segment != SegmentId) {
#if DEBUGMETRIC
						if (debug)
							Log._Debug($" GetVehicleMetricGoingToSegment: (Segment {SegmentId}, Node {NodeId}) Vehicle {vehicleId} error: Segment mismatch! Vehicle is on seg. {curPos.m_segment} but should be on {SegmentId}");
#endif
						RequestCleanup();
						return;
					}

					ushort targetSegmentId = nextPos.m_segment;
					if (targetSegmentId == 0) {
#if DEBUGMETRIC
						if (debug)
							Log._Debug($" GetVehicleMetricGoingToSegment: (Segment {SegmentId}, Node {NodeId}) Vehicle {vehicleId} is going from {curPos.m_segment} to {targetSegmentId}: Changing targetSegmentId to {curPos.m_segment}");
#endif
						targetSegmentId = curPos.m_segment;
					}

					if (!includeStopped && vehState.GetLastFrameVelocity().sqrMagnitude < TrafficPriorityManager.MAX_SQR_STOP_VELOCITY) {
#if DEBUGMETRIC
						if (debug)
							Log._Debug($"  GetVehicleMetricGoingToSegment: (Segment {SegmentId}, Node {NodeId}) Vehicle {vehicleId}: too slow");
#endif
						++numProcessed;
						return;
					}

					try {
						if (Options.simAccuracy <= 2) {
							uint normLength = 100u;
							if (avgSegLen > 0) {
								normLength = Math.Min(100u, (uint)(state.TotalLength * 100u) / avgSegLen) + 1; // TODO +1 because the vehicle length calculation for trains/monorail in the method VehicleState.OnVehicleSpawned returns 0 (or a very small number maybe?)
							}

#if DEBUGMETRIC
							if (debug)
								Log._Debug($"  GetVehicleMetricGoingToSegment: (Segment {SegmentId}, Node {NodeId}) NormLength of vehicle {vehicleId}: {avgSegLen} -> {normLength} (TotalLength={state.TotalLength})");
#endif

							ret[curPos.m_lane][targetSegmentId] += normLength;
						} else {
							ret[curPos.m_lane][targetSegmentId] += 10;
						}
						++numProcessed;
					} catch (Exception
#if DEBUGMETRIC
					e
#endif
					) {
#if DEBUGMETRIC
						Log._Debug($"  GetVehicleMetricGoingToSegment: (Segment {SegmentId}, Node {NodeId}) Could not add length of vehicle {vehicleId} (lane {curPos.m_lane}, target seg. {targetSegmentId}) to going/moving stats: {e}");
#endif
					}

#if DEBUGMETRIC
					if (debug)
						Log._Debug($"  GetVehicleMetricGoingToSegment: (Segment {SegmentId}, Node {NodeId}) Vehicle {vehicleId}: *added*! Coming from segment {SegmentId}, lane {curPos.m_lane}. Going to segment {nextPos.m_segment}, lane {nextPos.m_lane}");
#endif

					if ((Options.simAccuracy >= 3 && numProcessed >= 3) || (Options.simAccuracy == 2 && numProcessed >= 5) || (Options.simAccuracy == 1 && numProcessed >= 10)) {
						breakLoop = true;
						return;
					}
				});

				if (breakLoop)
					break;

				vehicleId = state.NextVehicleIdOnSegment;
			}

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"GetVehicleMetricGoingToSegment: Calculation completed. {string.Join(", ", ret.Select(e => "[" + string.Join(", ", e.Select(x => x.Key.ToString() + "=" + x.Value.ToString()).ToArray()) + "]").ToArray())}");
#endif
			return ret;
		}

		internal int GetRegisteredVehicleCount() {
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

			ushort vehicleId = FirstRegisteredVehicleId;
			int ret = 0;
			while (vehicleId != 0) {
				++ret;
				vehicleId = vehStateManager._GetVehicleState(vehicleId).NextVehicleIdOnSegment;
			}
			return ret;
		}

		internal void Destroy() {
			UnregisterAllVehicles();
		}

		private void UnregisterAllVehicles() {
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;
			while (FirstRegisteredVehicleId != 0) {
				vehStateManager._GetVehicleState(FirstRegisteredVehicleId).Unlink();
			}
		}

		internal void Update() {
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
