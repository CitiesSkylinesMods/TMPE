using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager.Impl {
	public class TrafficMeasurementManager : AbstractCustomManager, ITrafficMeasurementManager {
		public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;
		public const NetInfo.LaneType LANE_TYPES = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

		public static readonly TrafficMeasurementManager Instance = new TrafficMeasurementManager();

		public const ushort REF_REL_SPEED_PERCENT_DENOMINATOR = 100;
		public const ushort REF_REL_SPEED = 10000;
		public const ushort MAX_REL_SPEED = 15000;

		public struct LaneTrafficData {
			/// <summary>
			/// Number of seen vehicles since last speed measurement
			/// </summary>
			public ushort trafficBuffer;

			/// <summary>
			/// Accumulated speeds since last traffic measurement
			/// </summary>
			public uint accumulatedSpeeds;
#if PFTRAFFICSTATS
			/// <summary>
			/// Number of routed vehicles * (100 - mean speed in %) (current value)
			/// </summary>
			public uint pathFindTrafficBuffer;
			/// <summary>
			/// Number of routed vehicles * (100 - mean speed in %) (stable value)
			/// </summary>
			public uint lastPathFindTrafficBuffer;
#endif
#if MEASUREDENSITY
			/// <summary>
			/// Accumulated densities since last traffic measurement
			/// </summary>
			public uint accumulatedDensities;
			/// <summary>
			/// Current relative density
			/// </summary>
			public ushort relDensity;
#endif
			/// <summary>
			/// Current lane mean speed, per ten thousands
			/// </summary>
			public ushort meanSpeed;

			public override string ToString() {
				return $"[LaneTrafficData\n" +
					"\t" + $"trafficBuffer = {trafficBuffer}\n" +
					"\t" + $"accumulatedSpeeds = {accumulatedSpeeds}\n" +
#if MEASUREDENSITY
					"\t" + $"accumulatedDensities = {accumulatedDensities}\n" +
					"\t" + $"relDensity = {relDensity}\n" +
#endif
					"\t" + $"meanSpeed = {meanSpeed}\n" +
#if PFTRAFFICSTATS
					"\t" + $"pathFindTrafficBuffer = {pathFindTrafficBuffer}\n" +
					"\t" + $"lastPathFindTrafficBuffer = {lastPathFindTrafficBuffer}\n" +
#endif
					"LaneTrafficData]";
			}
		}

		public struct SegmentDirTrafficData {
#if MEASUREDENSITY
			/// <summary>
			/// Current accumulated densities
			/// </summary>
			public uint accumulatedDensities;
#endif
#if MEASURECONGESTION
			/// <summary>
			/// Current minimum lane speed
			/// </summary>
			public ushort minSpeed;
#endif
			public ushort meanSpeed;

#if MEASURECONGESTION
			/// <summary>
			/// Number of times the segment had been marked as congested
			/// </summary>
			public byte numCongested;

			/// <summary>
			/// Total number of times congestion was measured
			/// </summary>
			public byte numCongestionMeasurements;

			/// <summary>
			/// Relative number of times congestion was detected
			/// </summary>
			public ushort congestionRatioInPercent;
#endif

#if PFTRAFFICSTATS
			/// <summary>
			/// Total number of routed vehicles * (100 - mean speed in %) (stable value)
			/// </summary>
			public uint totalPathFindTrafficBuffer;
#endif

			/*public SegmentDirTrafficData() {
				minSpeed = MAX_SPEED;
			}*/

			public override string ToString() {
				return $"[SegmentDirTrafficData\n" +
#if MEASUREDENSITY
					"\t" + $"accumulatedDensities = {accumulatedDensities}\n" +
#endif
#if MEASURECONGESTION
					"\t" + $"minSpeed = {minSpeed}\n" +
#endif
					"\t" + $"meanSpeed = {meanSpeed}\n" +
#if PFTRAFFICSTATS
					"\t" + $"totalPathFindTrafficBuffer = {totalPathFindTrafficBuffer}\n" +
#endif
#if MEASURECONGESTION
					"\t" + $"numCongested = {numCongested}\n" +
					"\t" + $"numCongestionMeasurements = {numCongestionMeasurements}\n" +
					"\t" + $"congestionRatioInPercent = {congestionRatioInPercent}\n" +
#endif
					"SegmentDirTrafficData]";
			}
		}

		/// <summary>
		/// Traffic data per segment and lane
		/// </summary>
		private LaneTrafficData[][] laneTrafficData;

		/// <summary>
		/// Traffic data per segment and traffic direction
		/// </summary>
		internal SegmentDirTrafficData[] segmentDirTrafficData;

		//private SegmentDirTrafficData defaultSegmentDirTrafficData;

#if MEASURECONGESTION
		private ushort[] minRelSpeeds = { REF_REL_SPEED, REF_REL_SPEED };
#endif
#if MEASUREDENSITY
		private uint[] densities = { 0, 0 };
#endif
		//private byte[] numDirLanes = { 0, 0 };
#if EXTSTATS
		private uint[] totalPfBuf = { 0, 0 };
#endif

		private uint[] meanSpeeds = { 0, 0 };
		private int[] meanSpeedLanes = { 0, 0 };

		private TrafficMeasurementManager() {
			laneTrafficData = new LaneTrafficData[NetManager.MAX_SEGMENT_COUNT][];
			segmentDirTrafficData = new SegmentDirTrafficData[NetManager.MAX_SEGMENT_COUNT * 2];
			//defaultSegmentDirTrafficData = new SegmentDirTrafficData();
			//defaultSegmentDirTrafficData.minSpeed = MAX_SPEED;
			//defaultSegmentDirTrafficData.meanSpeed = MAX_SPEED;
			for (int i = 0; i < segmentDirTrafficData.Length; ++i) {
#if MEASURECONGESTION
				segmentDirTrafficData[i].minSpeed = REF_REL_SPEED;
#endif
				segmentDirTrafficData[i].meanSpeed = MAX_REL_SPEED;
			}
			ResetTrafficStats();
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Lane traffic data:");
			if (laneTrafficData == null) {
				Log._Debug($"\t<null>");
			} else {
				for (int i = 0; i < laneTrafficData.Length; ++i) {
					if (laneTrafficData[i] == null) {
						continue;
					}
					Log._Debug($"\tSegment {i}:");
					for (int k = 0; k < laneTrafficData[i].Length; ++k) {
						Log._Debug($"\t\tLane {k}: {laneTrafficData[i][k]}");
					}
				}
			}

			Log._Debug($"Segment direction traffic data:");
			if (segmentDirTrafficData == null) {
				Log._Debug($"\t<null>");
			} else {
				for (int i = 0; i < segmentDirTrafficData.Length; ++i) {
					Log._Debug($"\tIndex {i}: {segmentDirTrafficData[i]}");
				}
			}
		}

		public ushort CalcLaneRelativeMeanSpeed(ushort segmentId, byte laneIndex, uint laneId, NetInfo.Lane laneInfo) {
			if (laneTrafficData[segmentId] == null || laneIndex >= laneTrafficData[segmentId].Length) {
				return MAX_REL_SPEED;
			}

			ushort currentBuf = laneTrafficData[segmentId][laneIndex].trafficBuffer;
			ushort curRelSpeed = MAX_REL_SPEED;

			// we use integer division here because it's faster
			if (currentBuf > 0) {
				uint laneVehicleSpeedLimit = (uint)((Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(segmentId, laneIndex, laneId, laneInfo) : laneInfo.m_speedLimit) * 8f);
				curRelSpeed = (ushort)Math.Min((uint)MAX_REL_SPEED, ((laneTrafficData[segmentId][laneIndex].accumulatedSpeeds * (uint)REF_REL_SPEED) / currentBuf) / laneVehicleSpeedLimit); // 0 .. 10000, m_speedLimit of highway is 2, actual max. vehicle speed on highway is 16, that's why we use x*8 == x<<3 (don't ask why CO uses different units for velocity)
			}
			return curRelSpeed;
		}

		public void SimulationStep(ushort segmentId, ref NetSegment segment) {
			GlobalConfig conf = GlobalConfig.Instance;

			// calculate traffic density
			NetInfo segmentInfo = segment.Info;
			uint curLaneId = segment.m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;

			if (laneTrafficData[segmentId] == null || laneTrafficData[segmentId].Length < numLanes) {
				laneTrafficData[segmentId] = new LaneTrafficData[numLanes];
				for (int i = 0; i < numLanes; ++i) {
					//laneTrafficData[segmentId][i] = new LaneTrafficData();
					laneTrafficData[segmentId][i].meanSpeed = MAX_REL_SPEED;
				}
			}

			// calculate max./min. lane speed
			for (int i = 0; i < 2; ++i) {
#if MEASURECONGESTION
				minRelSpeeds[i] = REF_REL_SPEED;
#endif
#if MEASUREDENSITY
				densities[i] = 0;
#endif

				meanSpeeds[i] = 0;
				meanSpeedLanes[i] = 0;

				//numDirLanes[i] = 0;
#if EXTSTATS
				totalPfBuf[i] = 0;
#endif
			}

			//ushort maxBuffer = 0;

			/*for (uint li = 0; li < numLanes; ++li) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[li];
				if ((laneInfo.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None)
					continue;
				if ((laneInfo.m_laneType & LANE_TYPES) == NetInfo.LaneType.None)
					continue;

				int dirIndex = GetDirIndex(laneInfo.m_finalDirection);

				++numDirLanes[dirIndex];
#if PFTRAFFICSTATS
				uint pfBuf = laneTrafficData[segmentId][li].pathFindTrafficBuffer;
				totalPfBuf[dirIndex] += pfBuf;
				laneTrafficData[segmentId][li].lastPathFindTrafficBuffer = pfBuf;
#endif
#if MEASURECONGESTION
				ushort curSpeed = laneTrafficData[segmentId][li].meanSpeed;
				if (curSpeed < minRelSpeeds[dirIndex]) {
					minRelSpeeds[dirIndex] = curSpeed;
				}
#endif
				//ushort buf = laneTrafficData[segmentId][li].trafficBuffer;
				//if (buf > maxBuffer) {
				//	maxBuffer = buf;
				//}
			}*/

			curLaneId = segment.m_lanes;

			byte laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

				if ((laneInfo.m_laneType & LANE_TYPES) != NetInfo.LaneType.None && (laneInfo.m_vehicleType & VEHICLE_TYPES) != VehicleInfo.VehicleType.None) {
					int dirIndex = GetDirIndex(laneInfo.m_finalDirection);

					// calculate reported mean speed
					ushort newRelSpeed = CalcLaneRelativeMeanSpeed(segmentId, laneIndex, curLaneId, segment.Info.m_lanes[laneIndex]);
#if MEASURECONGESTION
					if (newRelSpeed < minRelSpeeds[dirIndex]) {
						minRelSpeeds[dirIndex] = newRelSpeed;
					}
#endif

					meanSpeeds[dirIndex] += newRelSpeed;
					++meanSpeedLanes[dirIndex];

					laneTrafficData[segmentId][laneIndex].meanSpeed = newRelSpeed;

					// reset buffers
#if MEASUREDENSITY
					laneTrafficData[segmentId][laneIndex].accumulatedDensities /= 2;
#endif
					if (laneTrafficData[segmentId][laneIndex].trafficBuffer >= conf.AdvancedVehicleAI.MaxTrafficBuffer) {
						laneTrafficData[segmentId][laneIndex].accumulatedSpeeds /= laneTrafficData[segmentId][laneIndex].trafficBuffer;
						laneTrafficData[segmentId][laneIndex].trafficBuffer = 1;
					} else if (laneTrafficData[segmentId][laneIndex].trafficBuffer == 1) {
						laneTrafficData[segmentId][laneIndex].accumulatedSpeeds = 0;
						laneTrafficData[segmentId][laneIndex].trafficBuffer = 0;
					}
					/*laneTrafficData[segmentId][laneIndex].accumulatedSpeeds = 0;
					laneTrafficData[segmentId][laneIndex].trafficBuffer = 0;*/

#if PFTRAFFICSTATS
					if (laneTrafficData[segmentId][laneIndex].pathFindTrafficBuffer > conf.MaxPathFindTrafficBuffer) {
						laneTrafficData[segmentId][laneIndex].pathFindTrafficBuffer >>= 1;
					}
#endif

#if MEASUREDENSITY
					densities[dirIndex] += laneTrafficData[segmentId][laneIndex].accumulatedDensities;
#endif
				}

				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}

			for (int i = 0; i < 2; ++i) {
				int segDirIndex = i == 0 ? GetDirIndex(segmentId, NetInfo.Direction.Forward) : GetDirIndex(segmentId, NetInfo.Direction.Backward);

#if MEASURECONGESTION
				if (segmentDirTrafficData[segDirIndex].numCongestionMeasurements > conf.MaxNumCongestionMeasurements) {
					segmentDirTrafficData[segDirIndex].numCongestionMeasurements >>= 1;
					segmentDirTrafficData[segDirIndex].numCongested >>= 1;
				}

				segmentDirTrafficData[segDirIndex].minSpeed = Math.Min(REF_REL_SPEED, minRelSpeeds[i]);

				++segmentDirTrafficData[segDirIndex].numCongestionMeasurements;
				if (segmentDirTrafficData[segDirIndex].minSpeed / 100u < conf.CongestionSpeedThreshold) {
					++segmentDirTrafficData[segDirIndex].numCongested;
				}
				segmentDirTrafficData[segDirIndex].congestionRatioInPercent = (ushort)(segmentDirTrafficData[segDirIndex].numCongestionMeasurements > 0 ? ((uint)segmentDirTrafficData[segDirIndex].numCongested * 100u) / (uint)segmentDirTrafficData[segDirIndex].numCongestionMeasurements : 0); // now in %
#endif
#if MEASUREDENSITY
				segmentDirTrafficData[segmentId][i].accumulatedDensities = densities[i];
#endif
#if PFTRAFFICSTATS
				segmentDirTrafficData[segDirIndex].totalPathFindTrafficBuffer = totalPfBuf[i];
#endif

				if (meanSpeedLanes[i] > 0) {
					segmentDirTrafficData[segDirIndex].meanSpeed = (ushort)Math.Min(REF_REL_SPEED, (meanSpeeds[i] / meanSpeedLanes[i]));
				} else {
					segmentDirTrafficData[segDirIndex].meanSpeed = REF_REL_SPEED;
				}
			}
		}

		public bool GetLaneTrafficData(ushort segmentId, byte laneIndex, out LaneTrafficData trafficData) {
			if (laneTrafficData[segmentId] == null || laneIndex >= laneTrafficData[segmentId].Length) {
				trafficData = default(LaneTrafficData);
				return false;
			} else {
				trafficData = laneTrafficData[segmentId][laneIndex];
				return true;
			}
		}

		public void DestroySegmentStats(ushort segmentId) {
			laneTrafficData[segmentId] = null;

			int fwdIndex = GetDirIndex(segmentId, NetInfo.Direction.Forward);
			int backIndex = GetDirIndex(segmentId, NetInfo.Direction.Backward);

			segmentDirTrafficData[fwdIndex] = default(SegmentDirTrafficData);
#if MEASURECONGESTION
			segmentDirTrafficData[fwdIndex].minSpeed = REF_REL_SPEED;
#endif
			segmentDirTrafficData[fwdIndex].meanSpeed = MAX_REL_SPEED;

			segmentDirTrafficData[backIndex] = default(SegmentDirTrafficData);
#if MEASURECONGESTION
			segmentDirTrafficData[backIndex].minSpeed = REF_REL_SPEED;
#endif
			segmentDirTrafficData[backIndex].meanSpeed = MAX_REL_SPEED;
		}

		public void ResetTrafficStats() {
			for (int i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
				DestroySegmentStats((ushort)i);
			}
		}

		public void AddTraffic(ushort segmentId, byte laneIndex
#if MEASUREDENSITY
			, ushort vehicleLength
#endif
			, ushort speed) {
			if (laneTrafficData[segmentId] == null || laneIndex >= laneTrafficData[segmentId].Length)
				return;

			laneTrafficData[segmentId][laneIndex].trafficBuffer = (ushort)Math.Min(65535u, (uint)laneTrafficData[segmentId][laneIndex].trafficBuffer + 1u);
			laneTrafficData[segmentId][laneIndex].accumulatedSpeeds += (uint)speed;
#if MEASUREDENSITY
			laneTrafficData[segmentId][laneIndex].accumulatedDensities += vehicleLength;
#endif
		}

#if PFTRAFFICSTATS
		public void AddPathFindTraffic(ushort segmentId, byte laneIndex) {
			LaneTrafficData[] lanesData = laneTrafficData[segmentId];
			if (lanesData == null || laneIndex >= lanesData.Length)
				return;

			lanesData[laneIndex].pathFindTrafficBuffer += (uint)((REF_REL_SPEED / REF_REL_SPEED_PERCENT_DENOMINATOR) - Math.Min(REF_REL_SPEED, lanesData[laneIndex].meanSpeed) / (REF_REL_SPEED / REF_REL_SPEED_PERCENT_DENOMINATOR));
		}
#endif

		internal int GetDirIndex(ushort segmentId, NetInfo.Direction dir) {
			return (int)segmentId + (dir == NetInfo.Direction.Backward ? NetManager.MAX_SEGMENT_COUNT : 0);
		}

		internal int GetDirIndex(NetInfo.Direction dir) {
			return dir == NetInfo.Direction.Backward ? 1 : 0;
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			ResetTrafficStats();
		}
	}
}
