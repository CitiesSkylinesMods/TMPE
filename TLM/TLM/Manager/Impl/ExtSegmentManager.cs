using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager.Impl {
	using State.ConfigData;

	public class ExtSegmentManager : AbstractCustomManager, IExtSegmentManager {
		public static ExtSegmentManager Instance { get; private set; } = null;

		static ExtSegmentManager() {
			Instance = new ExtSegmentManager();
		}

		/// <summary>
		/// All additional data for buildings
		/// </summary>
		public ExtSegment[] ExtSegments { get; private set; } = null;

		private ExtSegmentManager() {
			ExtSegments = new ExtSegment[NetManager.MAX_SEGMENT_COUNT];
			for (uint i = 0; i < ExtSegments.Length; ++i) {
				ExtSegments[i] = new ExtSegment((ushort)i);
			}
		}

		public bool IsValid(ushort segmentId) {
			return Constants.ServiceFactory.NetService.IsSegmentValid(segmentId);
		}

		protected void Reset(ref ExtSegment extSegment) {
			extSegment.Reset();
		}

		public void Recalculate(ushort segmentId) {
			Recalculate(ref ExtSegments[segmentId]);
		}

		protected void Recalculate(ref ExtSegment extSegment) {
			IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
			ushort segmentId = extSegment.segmentId;

#if DEBUG
			bool output = DebugSwitch.GeometryDebug.Get();

			if (output)
				Log._Debug($">>> ExtSegmentManager.Recalculate({segmentId}) called.");
#endif

			if (! IsValid(segmentId)) {
				if (extSegment.valid) {
					Reset(ref extSegment);
					extSegment.valid = false;

					extSegEndMan.Recalculate(segmentId);
					Constants.ManagerFactory.GeometryManager.OnUpdateSegment(ref extSegment);
				}
				return;
			}

#if DEBUG
			if (output)
				Log.Info($"Recalculating geometries of segment {segmentId} STARTED");
#endif

			Reset(ref extSegment);
			extSegment.valid = true;

			extSegment.oneWay = CalculateIsOneWay(segmentId);
			extSegment.highway = CalculateIsHighway(segmentId);
			extSegment.buslane = CalculateHasBusLane(segmentId);

			extSegEndMan.Recalculate(segmentId);

#if DEBUG
			if (output) {
				Log.Info($"Recalculated ext. segment {segmentId} (flags={Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags}): {extSegment}");
			}
#endif

			Constants.ManagerFactory.GeometryManager.OnUpdateSegment(ref extSegment);
		}

		public bool CalculateIsOneWay(ushort segmentId) {
			if (!IsValid(segmentId))
				return false;

			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			var hasForward = false;
			var hasBackward = false;

			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				bool validLane = (info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
				// TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check

				if (validLane) {
					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
						hasForward = true;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						hasBackward = true;
					}

					if (hasForward && hasBackward) {
						return false;
					}
				}

				laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
				laneIndex++;
			}

			return true;
		}

		public bool CalculateHasBusLane(ushort segmentId) {
			if (!IsValid(segmentId))
				return false;

			bool ret = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				ret = CalculateHasBusLane(segment.Info);
				return true;
			});
			return ret;
		}

		/// <summary>
		/// Calculates if the given segment info describes a segment having a bus lane
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <returns></returns>
		protected bool CalculateHasBusLane(NetInfo segmentInfo) {
			for (int laneIndex = 0; laneIndex < segmentInfo.m_lanes.Length; ++laneIndex) {
				if (segmentInfo.m_lanes[laneIndex].m_laneType == NetInfo.LaneType.TransportVehicle &&
					(segmentInfo.m_lanes[laneIndex].m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
					return true;
				}
			}

			return false;
		}

		public bool CalculateIsHighway(ushort segmentId) {
			if (!IsValid(segmentId))
				return false;

			bool ret = false;
			Constants.ServiceFactory.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				ret = CalculateIsHighway(segment.Info);
				return true;
			});
			return ret;
		}

		/// <summary>
		/// Calculates if the given segment info describes a highway segment
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <returns></returns>
		protected bool CalculateIsHighway(NetInfo segmentInfo) {
			if (segmentInfo.m_netAI is RoadBaseAI)
				return ((RoadBaseAI)segmentInfo.m_netAI).m_highwayRules;
			return false;
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Extended segment data:");
			for (int i = 0; i < ExtSegments.Length; ++i) {
				if (! IsValid((ushort)i)) {
					continue;
				}
				Log._Debug($"Segment {i}: {ExtSegments[i]}");
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < ExtSegments.Length; ++i) {
				ExtSegments[i].valid = false;
				Reset(ref ExtSegments[i]);
			}
		}

		public override void OnBeforeLoadData() {
			base.OnBeforeLoadData();
			Log._Debug($"ExtSegmentManager.OnBeforeLoadData: Calculating {ExtSegments.Length} extended segments...");
			for (int i = 0; i < ExtSegments.Length; ++i) {
				Recalculate(ref ExtSegments[i]);
			}
			Log._Debug($"ExtSegmentManager.OnBeforeLoadData: Calculation finished.");
		}
	}
}
