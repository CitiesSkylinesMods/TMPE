#define DEBUGFRONTVEHx
#define DEBUGREGx
#define DEBUGMETRICx

using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.AI;
using TrafficManager.Util;
using System.Threading;
using TrafficManager.State;
using TrafficManager.UI;

/// <summary>
/// A segment end describes a directional traffic segment connected to a controlled node
/// (having custom traffic lights or priority signs).
/// </summary>
namespace TrafficManager.Traffic {
	public class SegmentEnd : IObserver<SegmentGeometry>, IObserver<NodeGeometry> {
		public enum PriorityType {
			None = 0,
			Main = 1,
			Stop = 2,
			Yield = 3
		}

		public ushort NodeId {
			get; private set;
		}

		public ushort SegmentId {
			get; private set;
		}

		private bool startNode = false;

		private int numLanes = 0;

		public PriorityType Type {
			get { return type; }
			set { type = value; Housekeeping(); }
		}
		private PriorityType type = PriorityType.None;

		private IDisposable segGeometryUnsubscriber;
		private IDisposable nodeGeometryUnsubscriber;

		/// <summary>
		/// Vehicles that are traversing or will traverse this segment
		/// </summary>
		private Dictionary<ushort, VehiclePosition> registeredVehicles;

		private bool cleanupRequested = false;

		/// <summary>
		/// Vehicles that are traversing or will traverse this segment
		/// </summary>
		//private ushort[] frontVehicleIds;

		/// <summary>
		/// Number of vehicles / vehicle length goint to a certain segment
		/// </summary>
		private Dictionary<ushort, uint> numVehiclesGoingToSegmentId;

		public SegmentEnd(ushort nodeId, ushort segmentId, PriorityType type) {
			NodeId = nodeId;
			SegmentId = segmentId;
			Type = type;
			registeredVehicles = new Dictionary<ushort, VehiclePosition>();
			SegmentGeometry segGeometry = SegmentGeometry.Get(segmentId);
			OnUpdate(segGeometry);
			segGeometryUnsubscriber = segGeometry.Subscribe(this);
			NodeGeometry nodeGeometry = NodeGeometry.Get(nodeId);
			OnUpdate(nodeGeometry);
			nodeGeometryUnsubscriber = nodeGeometry.Subscribe(this);
		}

		~SegmentEnd() {
			Destroy();
		}

		internal void RequestCleanup() {
			cleanupRequested = true;
		}

		internal void SimulationStep() {
			if (cleanupRequested) {
#if DEBUG
				//Log._Debug($"Cleanup of SegmentEnd {SegmentId} @ {NodeId} requested. Performing cleanup now.");
#endif
				ushort[] regVehs = registeredVehicles.Keys.ToArray();
				foreach (ushort vehicleId in regVehs) {
					if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_flags & Vehicle.Flags.Created) == 0) {
						UnregisterVehicle(vehicleId);
						continue;
					}

					VehicleState state = VehicleStateManager.GetVehicleState(vehicleId);
					if (state == null) {
						UnregisterVehicle(vehicleId);
						continue;
					}

					VehiclePosition pos = state.GetCurrentPosition();
					if (pos == null) {
						UnregisterVehicle(vehicleId);
						continue;
					}
				}

				cleanupRequested = false;
			}

			Housekeeping();
		}

		/// <summary>
		/// Calculates for each segment the number of cars going to this segment.
		/// We use integer arithmetic for better performance.
		/// </summary>
		public Dictionary<ushort, uint> GetVehicleMetricGoingToSegment(float? minSpeed=null, byte? laneIndex=null, bool debug = false) {
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;

			for (var s = 0; s < 8; s++) {
				ushort segmentId = netManager.m_nodes.m_buffer[NodeId].GetSegment(s);

				if (segmentId == 0 || segmentId == SegmentId)
					continue;

				if (!numVehiclesGoingToSegmentId.ContainsKey(segmentId))
					continue;

				numVehiclesGoingToSegmentId[segmentId] = 0;
			}

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"GetVehicleMetricGoingToSegment: Segment {SegmentId}, Node {NodeId}. Target segments: {string.Join(", ", numVehiclesGoingToSegmentId.Keys.Select(x => x.ToString()).ToArray())}, Registered Vehicles: {string.Join(", ", GetRegisteredVehicles().Select(x => x.Key.ToString()).ToArray())}");
#endif

			foreach (KeyValuePair<ushort, VehiclePosition> e in GetRegisteredVehicles()) {
				ushort vehicleId = e.Key;
				VehiclePosition pos = e.Value;

#if DEBUGMETRIC
				if (debug)
					Log._Debug($" GetVehicleMetricGoingToSegment: Checking vehicle {vehicleId}");
#endif

				if (!numVehiclesGoingToSegmentId.ContainsKey(pos.TargetSegmentId)) {
#if DEBUGMETRIC
					if (debug)
						Log._Debug($"  GetVehicleMetricGoingToSegment: numVehiclesGoingToSegmentId does not contain key for target segment {pos.TargetSegmentId}");
#endif
					continue;
				}

				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_flags & Vehicle.Flags.Created) == 0) {
#if DEBUGMETRIC
					if (debug)
						Log._Debug($"  GetVehicleMetricGoingToSegment: Checking vehicle {vehicleId}: vehicle is invalid");
#endif
					RequestCleanup();
					continue;
				}

				VehicleState state = VehicleStateManager.GetVehicleState(vehicleId);
				if (state == null) {
#if DEBUGMETRIC
					if (debug)
						Log._Debug($"  GetVehicleMetricGoingToSegment: Checking vehicle {vehicleId}: state is null");
#endif
					RequestCleanup();
					continue;
				}

				if (minSpeed != null && vehicleManager.m_vehicles.m_buffer[vehicleId].GetLastFrameVelocity().magnitude < minSpeed) {
#if DEBUGMETRIC
					if (debug)
						Log._Debug($"  GetVehicleMetricGoingToSegment: Vehicle {vehicleId}: too slow");
#endif
					continue;
				}

				if (laneIndex != null && pos.SourceLaneIndex != laneIndex) {
#if DEBUGMETRIC
					if (debug)
						Log._Debug($"  GetVehicleMetricGoingToSegment: Vehicle {vehicleId}: Lane index mismatch (expected: {laneIndex}, was: {pos.SourceLaneIndex})");
#endif
					continue;
				}

				uint avgSegmentLength = (uint)netManager.m_segments.m_buffer[SegmentId].m_averageLength;
				uint normLength = Math.Min(100u, (uint)(state.TotalLength * 100u) / avgSegmentLength);

#if DEBUGMETRIC
				if (debug)
					Log._Debug($"  GetVehicleMetricGoingToSegment: NormLength of vehicle {vehicleId}: {avgSegmentLength} -> {normLength}");
#endif

#if DEBUGMETRIC
				if (debug)
					numVehiclesGoingToSegmentId[pos.TargetSegmentId] += 1;
				else
					numVehiclesGoingToSegmentId[pos.TargetSegmentId] += normLength;
#else
				numVehiclesGoingToSegmentId[pos.TargetSegmentId] = Math.Min(100u, numVehiclesGoingToSegmentId[pos.TargetSegmentId] + normLength);
#endif

#if DEBUGMETRIC
				if (debug)
					Log._Debug($"  GetVehicleMetricGoingToSegment: Vehicle {vehicleId}: *added*!");
#endif

					// "else" must not happen (incoming one-way)
			}

#if DEBUGMETRIC
			if (debug)
				Log._Debug($"GetVehicleMetricGoingToSegment: Calculation completed. {string.Join(", ", numVehiclesGoingToSegmentId.Select(x => x.Key.ToString() + "=" + x.Value.ToString()).ToArray())}");
#endif
			return numVehiclesGoingToSegmentId;
		}

		internal void RegisterVehicle(ushort vehicleId, ref Vehicle vehicleData, VehiclePosition pos) {
			if (pos.TransitNodeId != NodeId || pos.SourceSegmentId != SegmentId) {
				Log.Warning($"Refusing to add vehicle {vehicleId} to SegmentEnd {SegmentId} @ {NodeId} (given: {pos.SourceSegmentId} @ {pos.TransitNodeId}).");
				return;
			}

			registeredVehicles[vehicleId] = pos;

#if DEBUGREG
			Log._Debug($"RegisterVehicle({vehicleId}): Registering vehicle {vehicleId} at segment {SegmentId}, {NodeId}. number of vehicles: {registeredVehicles.Count}. reg. vehicles: {string.Join(", ", registeredVehicles.Select(x => x.ToString()).ToArray())}");
#endif
			/*if (isCurrentSegment)
				DetermineFrontVehicles();*/
		}

		internal void UnregisterVehicle(ushort vehicleId) {
			registeredVehicles.Remove(vehicleId);

#if DEBUGREG
			Log.Warning($"UnregisterVehicle({vehicleId}): Removing vehicle {vehicleId} from segment {SegmentId}, {NodeId}. number of vehicles: {registeredVehicles.Count}. reg. vehicles: {string.Join(", ", registeredVehicles.Select(x => x.ToString()).ToArray())}");
#endif
			//DetermineFrontVehicles();
		}

		/*internal void UpdateApproachingVehicles() {
			DetermineFrontVehicles();
		}*/

		internal Dictionary<ushort, VehiclePosition> GetRegisteredVehicles() {
#if DEBUGREG
			Log._Debug($"GetRegisteredVehicles: Segment {SegmentId}. { string.Join(", ", registeredVehicles.Select(x => x.ToString()).ToArray())}");
#endif
			return registeredVehicles;
		}

		internal int GetRegisteredVehicleCount() {
			return registeredVehicles.Count;
		}

		internal int GetRegisteredVehicleCount(HashSet<byte> laneIndices) {
			int ret = 0;
			foreach (KeyValuePair<ushort, VehiclePosition> e in registeredVehicles) {
				ushort vehicleId = e.Key;
				VehiclePosition pos = e.Value;
#if DEBUGREG
				Log._Debug($"GetRegisteredVehicleCount @ seg. {SegmentId}, node {NodeId}: laneIndices: {string.Join(", ", laneIndices.Select(x => x.ToString()).ToArray())}, vehicle {vehicleId}, pos? {pos != null}, source seg. {pos?.SourceSegmentId}, transit node {pos?.TransitNodeId}, source lane {pos?.SourceLaneIndex}");
#endif
				if (laneIndices.Contains(pos.SourceLaneIndex))
					++ret;
			}
			return ret;
		}

		internal void Destroy() {
			if (segGeometryUnsubscriber != null)
				segGeometryUnsubscriber.Dispose();
			if (nodeGeometryUnsubscriber != null)
				nodeGeometryUnsubscriber.Dispose();
		}

		public void OnUpdate(SegmentGeometry geometry) {
			startNode = Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_startNode == NodeId;
			numLanes = Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].Info.m_lanes.Length;
			numVehiclesGoingToSegmentId = new Dictionary<ushort, uint>(7);
			//frontVehicleIds = new ushort[numLanes];
			ushort[] outgoingSegmentIds = geometry.GetOutgoingSegments(startNode);
			foreach (ushort otherSegmentId in outgoingSegmentIds)
				numVehiclesGoingToSegmentId[otherSegmentId] = 0;
		}

		public void OnUpdate(NodeGeometry geometry) {
			Housekeeping();
		}

		internal void Housekeeping() {
			if (TrafficManagerTool.GetToolMode() != ToolMode.AddPrioritySigns && TrafficLightSimulation.GetNodeSimulation(NodeId) == null && Type == PriorityType.None)
				TrafficPriority.RemovePrioritySegments(NodeId);
		}
	}
}
