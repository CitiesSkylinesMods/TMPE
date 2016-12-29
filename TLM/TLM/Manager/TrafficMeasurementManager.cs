using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager {
	public class TrafficMeasurementManager : AbstractCustomManager {
		public static TrafficMeasurementManager Instance { get; private set; } = null;

		public const ushort MAX_SPEED = 10000;

		public class LaneTrafficData {
			/// <summary>
			/// Number of seen vehicles since last speed measurement
			/// </summary>
			public ushort trafficBuffer;
			/// <summary>
			/// Accumulated speeds since last traffic measurement
			/// </summary>
			public uint accumulatedSpeeds;
			/// <summary>
			/// Accumulated densities since last traffic measurement
			/// </summary>
			public uint accumulatedDensities;
			/// <summary>
			/// Current relative density
			/// </summary>
			public ushort relDensity;
			/// <summary>
			/// Current lane mean speed, per ten thousands
			/// </summary>
			public ushort meanSpeed;
		}

		public class SegmentDirTrafficData {
			/// <summary>
			/// Current accumulated densities
			/// </summary>
			public uint accumulatedDensities;
			/// <summary>
			/// Current minimum lane speed
			/// </summary>
			public ushort minSpeed;
#if DEBUG
			public ushort meanSpeed;
#endif

			public SegmentDirTrafficData() {
				minSpeed = MAX_SPEED;
			}
		}

		/// <summary>
		/// Traffic data per segment and lane
		/// </summary>
		private LaneTrafficData[][] laneTrafficData;

		/// <summary>
		/// Traffic data per segment and traffic direction
		/// </summary>
		private SegmentDirTrafficData[][] segmentDirTrafficData;

		private SegmentDirTrafficData defaultSegmentDirTrafficData;

		private ushort[] minSpeeds = { MAX_SPEED, MAX_SPEED };
		private uint[] densities = { 0, 0 };
#if DEBUG
		private uint[] meanSpeeds = { 0, 0 };
		private int[] meanSpeedLanes = { 0, 0 };
#endif

		static TrafficMeasurementManager() {
			Instance = new TrafficMeasurementManager();
		}

		private TrafficMeasurementManager() {
			laneTrafficData = new LaneTrafficData[NetManager.MAX_SEGMENT_COUNT][];
			segmentDirTrafficData = new SegmentDirTrafficData[NetManager.MAX_SEGMENT_COUNT][];
			defaultSegmentDirTrafficData = new SegmentDirTrafficData();
			ResetTrafficStats();
		}

		public void SimulationStep(ushort segmentId, ref NetSegment segmentData) {
			GlobalConfig conf = GlobalConfig.Instance;

			// calculate traffic density
			NetInfo segmentInfo = segmentData.Info;
			uint curLaneId = segmentData.m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;

			// ensure valid array sizes
			if (segmentDirTrafficData[segmentId] == null) {
				segmentDirTrafficData[segmentId] = new SegmentDirTrafficData[2];
				segmentDirTrafficData[segmentId][0] = new SegmentDirTrafficData();
				segmentDirTrafficData[segmentId][1] = new SegmentDirTrafficData();
			}

			if (laneTrafficData[segmentId] == null || laneTrafficData[segmentId].Length < numLanes) {
				laneTrafficData[segmentId] = new LaneTrafficData[numLanes];
				for (int i = 0; i < numLanes; ++i) {
					laneTrafficData[segmentId][i] = new LaneTrafficData();
					laneTrafficData[segmentId][i].meanSpeed = MAX_SPEED;
				}
			}

			// calculate max./min. lane speed
			for (int i = 0; i < 2; ++i) {
				minSpeeds[i] = MAX_SPEED;
				densities[i] = 0;
#if DEBUG
				meanSpeeds[i] = 0;
				meanSpeedLanes[i] = 0;
#endif
			}

			for (uint li = 0; li < numLanes; ++li) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[li];
				if ((laneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None)
					continue;

				int dirIndex = GetDirIndex(laneInfo.m_finalDirection);

				ushort curSpeed = laneTrafficData[segmentId][li].meanSpeed;
				if (curSpeed < minSpeeds[dirIndex])
					minSpeeds[dirIndex] = curSpeed;
			}

			curLaneId = segmentData.m_lanes;

			uint laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

				if ((laneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None) {
					int dirIndex = GetDirIndex(laneInfo.m_finalDirection);

					ushort currentBuf = laneTrafficData[segmentId][laneIndex].trafficBuffer;
					ushort curSpeed = MAX_SPEED;

					// we use integer division here because it's faster
					if (currentBuf > 0) {
						curSpeed = (ushort)Math.Min((uint)MAX_SPEED, ((laneTrafficData[segmentId][laneIndex].accumulatedSpeeds * (uint)MAX_SPEED) / currentBuf) / ((uint)(Math.Max(Math.Min(2f, SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(segmentId, laneIndex, curLaneId, segmentData.Info.m_lanes[laneIndex])) * 8f, 1f)))); // 0 .. 10000, m_speedLimit of highway is 2, actual max. vehicle speed on highway is 16, that's why we use x*8 == x<<3 (don't ask why CO uses different units for velocity)
					}

					// calculate reported mean speed
					uint minSpeed = minSpeeds[dirIndex];
					ushort prevSpeed = laneTrafficData[segmentId][laneIndex].meanSpeed;
					float maxSpeedDiff = Mathf.Abs((short)curSpeed - (short)minSpeed);

					float updateFactor = Mathf.Clamp(1f - (float)maxSpeedDiff / (float)conf.MaxSpeedDifference, conf.MinSpeedUpdateFactor, conf.MaxSpeedUpdateFactor);
					ushort newSpeed = (ushort)Mathf.Clamp((float)prevSpeed + ((float)curSpeed - (float)prevSpeed) * updateFactor, 0, MAX_SPEED);

					if (newSpeed < minSpeed) {
						minSpeeds[dirIndex] = newSpeed;
					} else {
						int maxTolerableSpeed = (int)minSpeed + (int)conf.MaxSpeedDifference;
						if (newSpeed > maxTolerableSpeed)
							newSpeed = (ushort)maxTolerableSpeed;
					}

#if DEBUG
					meanSpeeds[dirIndex] += newSpeed;
					meanSpeedLanes[dirIndex]++;
#endif
					laneTrafficData[segmentId][laneIndex].meanSpeed = newSpeed;
					densities[dirIndex] += laneTrafficData[segmentId][laneIndex].accumulatedDensities;

					// reset buffers
					laneTrafficData[segmentId][laneIndex].accumulatedDensities /= 2;
					laneTrafficData[segmentId][laneIndex].accumulatedSpeeds = 0;
					laneTrafficData[segmentId][laneIndex].trafficBuffer = 0;
				}

				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}

			for (int i = 0; i < 2; ++i) {
				segmentDirTrafficData[segmentId][i].minSpeed = minSpeeds[i];
				segmentDirTrafficData[segmentId][i].accumulatedDensities = densities[i];
#if DEBUG
				if (meanSpeedLanes[i] > 0) {
					segmentDirTrafficData[segmentId][i].meanSpeed = (ushort)(meanSpeeds[i] / meanSpeedLanes[i]);
				} else {
					segmentDirTrafficData[segmentId][i].meanSpeed = MAX_SPEED;
				}
#endif
			}
		}

		public bool GetTrafficData(ushort segmentId, NetInfo segmentInfo, out LaneTrafficData[] trafficData) {
			if (laneTrafficData[segmentId] == null || laneTrafficData[segmentId].Length != segmentInfo.m_lanes.Length) {
				trafficData = null;
				return false;
			} else {
				trafficData = laneTrafficData[segmentId];
				return true;
			}
		}

		public bool GetTrafficData(ushort segmentId, NetInfo.Direction dir, out SegmentDirTrafficData trafficData) {
			if (segmentDirTrafficData[segmentId] == null) {
				trafficData = defaultSegmentDirTrafficData;
				return false;
			} else {
				trafficData = segmentDirTrafficData[segmentId][GetDirIndex(dir)];
				return true;
			}
		}

		public void DestroySegmentStats(ushort segmentId) {
			laneTrafficData[segmentId] = null;
			segmentDirTrafficData[segmentId] = null;
		}

		public void ResetTrafficStats() {
			for (int i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
				DestroySegmentStats((ushort)i);
			}
		}

		public void AddTraffic(ushort segmentId, byte laneIndex, ushort vehicleLength, ushort? speed) {
			if (laneTrafficData[segmentId] == null || laneIndex >= laneTrafficData[segmentId].Length)
				return;

			if (speed != null) {
				laneTrafficData[segmentId][laneIndex].trafficBuffer = (ushort)Math.Min(65535u, (uint)laneTrafficData[segmentId][laneIndex].trafficBuffer + 1u);
				laneTrafficData[segmentId][laneIndex].accumulatedSpeeds += (uint)speed;
			}
			laneTrafficData[segmentId][laneIndex].accumulatedDensities += vehicleLength;
		}

		protected int GetDirIndex(NetInfo.Direction dir) {
			return dir == NetInfo.Direction.Backward ? 1 : 0;
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			ResetTrafficStats();
		}
	}
}
