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

namespace TrafficManager.Manager {
	public class TrafficMeasurementManager : AbstractCustomManager {
		public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;
		public const NetInfo.LaneType LANE_TYPES = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

		public static readonly TrafficMeasurementManager Instance = new TrafficMeasurementManager();

		public const ushort MAX_SPEED = 10000;

		public struct LaneTrafficData {
			/// <summary>
			/// Number of seen vehicles since last speed measurement
			/// </summary>
			public ushort trafficBuffer;
			/// <summary>
			/// Accumulated speeds since last traffic measurement
			/// </summary>
			public uint accumulatedSpeeds;
			/// <summary>
			/// Number of routed vehicles * (100 - mean speed in %) (current value)
			/// </summary>
			public uint pathFindTrafficBuffer;
			/// <summary>
			/// Number of routed vehicles * (100 - mean speed in %) (stable value)
			/// </summary>
			public uint lastPathFindTrafficBuffer;
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
					"\t" + $"pathFindTrafficBuffer = {pathFindTrafficBuffer}\n" +
					"\t" + $"lastPathFindTrafficBuffer = {lastPathFindTrafficBuffer}\n" +
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
			/// <summary>
			/// Current minimum lane speed
			/// </summary>
			public ushort minSpeed;
			public ushort meanSpeed;

			/// <summary>
			/// Number of times the segment had been marked as congested
			/// </summary>
			public byte numCongested;
			/// <summary>
			/// Total number of times congestion was measured
			/// </summary>
			public byte numCongestionMeasurements;

			/// <summary>
			/// Total number of routed vehicles * (100 - mean speed in %) (stable value)
			/// </summary>
			public uint totalPathFindTrafficBuffer;

			/*public SegmentDirTrafficData() {
				minSpeed = MAX_SPEED;
			}*/

			public override string ToString() {
				return $"[SegmentDirTrafficData\n" +
#if MEASUREDENSITY
					"\t" + $"accumulatedDensities = {accumulatedDensities}\n" +
#endif
					"\t" + $"minSpeed = {minSpeed}\n" +
					"\t" + $"meanSpeed = {meanSpeed}\n" +
					"\t" + $"totalPathFindTrafficBuffer = {totalPathFindTrafficBuffer}\n" +
					"\t" + $"numCongested = {numCongested}\n" +
					"\t" + $"numCongestionMeasurements = {numCongestionMeasurements}\n" +
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

		private SegmentDirTrafficData defaultSegmentDirTrafficData;

		private ushort[] minSpeeds = { MAX_SPEED, MAX_SPEED };
		private uint[] densities = { 0, 0 };
		private byte[] numDirLanes = { 0, 0 };
		private uint[] totalPfBuf = { 0, 0 };
		private uint[] minPfBuf = { 0, 0 };

		private uint[] meanSpeeds = { 0, 0 };
		private int[] meanSpeedLanes = { 0, 0 };

		private int[] segDirIndices = { 0, 0 };

		private TrafficMeasurementManager() {
			laneTrafficData = new LaneTrafficData[NetManager.MAX_SEGMENT_COUNT][];
			segmentDirTrafficData = new SegmentDirTrafficData[NetManager.MAX_SEGMENT_COUNT * 2];
			defaultSegmentDirTrafficData = new SegmentDirTrafficData();
			defaultSegmentDirTrafficData.minSpeed = MAX_SPEED;
			defaultSegmentDirTrafficData.meanSpeed = MAX_SPEED;
			for (int i = 0; i < segmentDirTrafficData.Length; ++i) {
				segmentDirTrafficData[i].minSpeed = MAX_SPEED;
				segmentDirTrafficData[i].meanSpeed = MAX_SPEED;
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

		public void SimulationStep(ushort segmentId, ref NetSegment segmentData) {
			GlobalConfig conf = GlobalConfig.Instance;

			// calculate traffic density
			NetInfo segmentInfo = segmentData.Info;
			uint curLaneId = segmentData.m_lanes;
			int numLanes = segmentInfo.m_lanes.Length;

			if (laneTrafficData[segmentId] == null || laneTrafficData[segmentId].Length < numLanes) {
				laneTrafficData[segmentId] = new LaneTrafficData[numLanes];
				for (int i = 0; i < numLanes; ++i) {
					//laneTrafficData[segmentId][i] = new LaneTrafficData();
					laneTrafficData[segmentId][i].meanSpeed = MAX_SPEED;
				}
			}

			// calculate max./min. lane speed
			for (int i = 0; i < 2; ++i) {
				minSpeeds[i] = MAX_SPEED;
				densities[i] = 0;

				meanSpeeds[i] = 0;
				meanSpeedLanes[i] = 0;
				segDirIndices[i] = 0;

				numDirLanes[i] = 0;
				totalPfBuf[i] = 0;
				minPfBuf[i] = uint.MaxValue;
			}

			ushort maxBuffer = 0;
			
			for (uint li = 0; li < numLanes; ++li) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[li];
				if ((laneInfo.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None)
					continue;
				if ((laneInfo.m_laneType & LANE_TYPES) == NetInfo.LaneType.None)
					continue;

				int dirIndex = GetDirIndex(laneInfo.m_finalDirection);

				++numDirLanes[dirIndex];
				uint pfBuf = laneTrafficData[segmentId][li].pathFindTrafficBuffer;
				totalPfBuf[dirIndex] += pfBuf;
				if (pfBuf < minPfBuf[dirIndex]) {
					minPfBuf[dirIndex] = pfBuf;
				}
				//laneTrafficData[segmentId][li].lastPathFindTrafficBuffer = pfBuf;

				ushort curSpeed = laneTrafficData[segmentId][li].meanSpeed;
				if (curSpeed < minSpeeds[dirIndex]) {
					minSpeeds[dirIndex] = curSpeed;
				}
				ushort buf = laneTrafficData[segmentId][li].trafficBuffer;
				if (buf > maxBuffer) {
					maxBuffer = buf;
				}
			}

			for (int i = 0; i < 2; ++i) {
				totalPfBuf[i] -= numDirLanes[i] * minPfBuf[i];
			}

			curLaneId = segmentData.m_lanes;

			uint laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

				if ((laneInfo.m_laneType & LANE_TYPES) != NetInfo.LaneType.None && (laneInfo.m_vehicleType & VEHICLE_TYPES) != VehicleInfo.VehicleType.None) {
					int dirIndex = GetDirIndex(laneInfo.m_finalDirection);

					laneTrafficData[segmentId][laneIndex].lastPathFindTrafficBuffer = laneTrafficData[segmentId][laneIndex].pathFindTrafficBuffer - minPfBuf[dirIndex];
					uint laneVehicleSpeedLimit = (uint)(Math.Max(Math.Min(2f, SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(segmentId, laneIndex, curLaneId, segmentData.Info.m_lanes[laneIndex])) * 8f, 1f));

					ushort currentBuf = laneTrafficData[segmentId][laneIndex].trafficBuffer;
					ushort curSpeed = MAX_SPEED;

					if (currentBuf < maxBuffer) {
						// if the lane did not have traffic we assume max speeds
						laneTrafficData[segmentId][laneIndex].accumulatedSpeeds += laneVehicleSpeedLimit * (uint)(maxBuffer - currentBuf);
						currentBuf = laneTrafficData[segmentId][laneIndex].trafficBuffer = maxBuffer;
					}

					// we use integer division here because it's faster
					if (currentBuf > 0) {
						curSpeed = (ushort)Math.Min((uint)MAX_SPEED, ((laneTrafficData[segmentId][laneIndex].accumulatedSpeeds * (uint)MAX_SPEED) / currentBuf) / laneVehicleSpeedLimit); // 0 .. 10000, m_speedLimit of highway is 2, actual max. vehicle speed on highway is 16, that's why we use x*8 == x<<3 (don't ask why CO uses different units for velocity)
					}

					// calculate reported mean speed
					ushort newSpeed = (ushort)Math.Min(Math.Max(curSpeed, 0u), MAX_SPEED);

					/*uint minSpeed = minSpeeds[dirIndex];
					ushort prevSpeed = laneTrafficData[segmentId][laneIndex].meanSpeed;
					float dirMinSpeedDiff = Math.Min(conf.MaxSpeedDifference, Mathf.Abs((short)curSpeed - (short)minSpeed)); // in % * 100; [0..MaxSpeedDifference]
					float relDirMinSpeedDiff = (float)dirMinSpeedDiff / (float)conf.MaxSpeedDifference; // [0..1]

					float maxUpdateFactor = conf.MaxSpeedUpdateFactor;
					float minUpdateFactor = conf.MinSpeedUpdateFactor;
					if (curSpeed > prevSpeed) {
						maxUpdateFactor /= 2f;
						minUpdateFactor /= 2f;
					}
					float updateFactor = minUpdateFactor + (1f - relDirMinSpeedDiff) * (maxUpdateFactor - minUpdateFactor);
					ushort newSpeed = (ushort)Mathf.Clamp((float)prevSpeed + ((float)curSpeed - (float)prevSpeed) * updateFactor, 0, MAX_SPEED);*/

					if (newSpeed < minSpeeds[dirIndex]) {
						minSpeeds[dirIndex] = newSpeed;
					} /*else {
						int maxTolerableSpeed = (int)minSpeed + (int)conf.MaxSpeedDifference;
						if (newSpeed > maxTolerableSpeed)
							newSpeed = (ushort)maxTolerableSpeed;
					}*/


					meanSpeeds[dirIndex] += newSpeed;
					meanSpeedLanes[dirIndex]++;

					laneTrafficData[segmentId][laneIndex].meanSpeed = newSpeed;

					// reset buffers
#if MEASUREDENSITY
					laneTrafficData[segmentId][laneIndex].accumulatedDensities /= 2;
#endif
					if (laneTrafficData[segmentId][laneIndex].trafficBuffer > conf.MaxTrafficBuffer) {
						laneTrafficData[segmentId][laneIndex].accumulatedSpeeds >>= 1;
						laneTrafficData[segmentId][laneIndex].trafficBuffer >>= 1;
					}

					if (laneTrafficData[segmentId][laneIndex].pathFindTrafficBuffer > conf.MaxPathFindTrafficBuffer) {
						laneTrafficData[segmentId][laneIndex].pathFindTrafficBuffer >>= 1;
					}

#if MEASUREDENSITY
					densities[dirIndex] += laneTrafficData[segmentId][laneIndex].accumulatedDensities;
#endif
				}

				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}

			segDirIndices[0] = GetDirIndex(segmentId, NetInfo.Direction.Forward);
			segDirIndices[1] = GetDirIndex(segmentId, NetInfo.Direction.Backward);

			for (int i = 0; i < 2; ++i) {
				int segDirIndex = segDirIndices[i];

				if (segmentDirTrafficData[segDirIndex].numCongestionMeasurements > conf.MaxNumCongestionMeasurements) {
					segmentDirTrafficData[segDirIndex].numCongestionMeasurements >>= 1;
					segmentDirTrafficData[segDirIndex].numCongested >>= 1;
				}

				segmentDirTrafficData[segDirIndex].minSpeed = minSpeeds[i];
				++segmentDirTrafficData[segDirIndex].numCongestionMeasurements;
				if (minSpeeds[i] / 100u < conf.CongestionSpeedThreshold) {
					++segmentDirTrafficData[segDirIndex].numCongested;
				}
#if MEASUREDENSITY
				segmentDirTrafficData[segmentId][i].accumulatedDensities = densities[i];
#endif
				segmentDirTrafficData[segDirIndex].totalPathFindTrafficBuffer = totalPfBuf[i];

				if (meanSpeedLanes[i] > 0) {
					segmentDirTrafficData[segDirIndex].meanSpeed = (ushort)(meanSpeeds[i] / meanSpeedLanes[i]);
				} else {
					segmentDirTrafficData[segDirIndex].meanSpeed = MAX_SPEED;
				}
			}
		}

		public bool GetLaneTrafficData(ushort segmentId, NetInfo segmentInfo, out LaneTrafficData[] trafficData) {
			if (laneTrafficData[segmentId] == null || laneTrafficData[segmentId].Length != segmentInfo.m_lanes.Length) {
				trafficData = null;
				return false;
			} else {
				trafficData = laneTrafficData[segmentId];
				return true;
			}
		}

		public void DestroySegmentStats(ushort segmentId) {
			laneTrafficData[segmentId] = null;

			int fwdIndex = GetDirIndex(segmentId, NetInfo.Direction.Forward);
			int backIndex = GetDirIndex(segmentId, NetInfo.Direction.Backward);

			segmentDirTrafficData[fwdIndex] = default(SegmentDirTrafficData);
			segmentDirTrafficData[fwdIndex].minSpeed = MAX_SPEED;
			segmentDirTrafficData[fwdIndex].meanSpeed = MAX_SPEED;

			segmentDirTrafficData[backIndex] = default(SegmentDirTrafficData);
			segmentDirTrafficData[backIndex].minSpeed = MAX_SPEED;
			segmentDirTrafficData[backIndex].meanSpeed = MAX_SPEED;
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
#if MEASUREDENSITY
			laneTrafficData[segmentId][laneIndex].accumulatedDensities += vehicleLength;
#endif
		}

		public void AddPathFindTraffic(ushort segmentId, byte laneIndex) {
			LaneTrafficData[] lanesData = laneTrafficData[segmentId];
			if (lanesData == null || laneIndex >= lanesData.Length)
				return;

			lanesData[laneIndex].pathFindTrafficBuffer += (uint)(100u - lanesData[laneIndex].meanSpeed / 100);
		}

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
