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

		public struct LaneTrafficData {
			/// <summary>
			/// Number of seen vehicles since last speed measurement
			/// </summary>
			public ushort trafficBuffer;

			/// <summary>
			/// Number of seen vehicles before last speed measurement
			/// </summary>
			public ushort lastTrafficBuffer;

			/// <summary>
			/// All-time max. traffic buffer
			/// </summary>
			public ushort maxTrafficBuffer;

			/// <summary>
			/// Accumulated speeds since last traffic measurement
			/// </summary>
			public uint accumulatedSpeeds;

			/// <summary>
			/// Current lane mean speed, per ten thousands
			/// </summary>
			public ushort meanSpeed;

			public override string ToString() {
				return $"[LaneTrafficData\n" +
					"\t" + $"trafficBuffer = {trafficBuffer}\n" +
					"\t" + $"lastTrafficBuffer = {lastTrafficBuffer}\n" +
					"\t" + $"maxTrafficBuffer = {maxTrafficBuffer}\n" +
					"\t" + $"trafficBuffer = {trafficBuffer}\n" +
					"\t" + $"accumulatedSpeeds = {accumulatedSpeeds}\n" +
					"\t" + $"meanSpeed = {meanSpeed}\n" +
					"LaneTrafficData]";
			}
		}

		public struct SegmentDirTrafficData {
			public ushort meanSpeed;

			public override string ToString() {
				return $"[SegmentDirTrafficData\n" +
					"\t" + $"meanSpeed = {meanSpeed}\n" +
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

		private uint[] meanSpeeds = { 0, 0 };
		private int[] meanSpeedLanes = { 0, 0 };

		private TrafficMeasurementManager() {
			laneTrafficData = new LaneTrafficData[NetManager.MAX_SEGMENT_COUNT][];
			segmentDirTrafficData = new SegmentDirTrafficData[NetManager.MAX_SEGMENT_COUNT * 2];

			for (int i = 0; i < segmentDirTrafficData.Length; ++i) {
				segmentDirTrafficData[i].meanSpeed = REF_REL_SPEED;
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
				return REF_REL_SPEED;
			}

			ushort currentBuf = laneTrafficData[segmentId][laneIndex].trafficBuffer;
			ushort curRelSpeed = REF_REL_SPEED;

			// we use integer division here because it's faster
			if (currentBuf > 0) {
				uint laneVehicleSpeedLimit = Math.Min(3u * 8u, (uint)((Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(segmentId, laneIndex, laneId, laneInfo) : laneInfo.m_speedLimit) * 8f));
				if (laneVehicleSpeedLimit <= 0) {
					// fallback: custom lanes may not have valid values set for speed limit
					laneVehicleSpeedLimit = 1;
				}
				curRelSpeed = (ushort)Math.Min((uint)REF_REL_SPEED, ((laneTrafficData[segmentId][laneIndex].accumulatedSpeeds * (uint)REF_REL_SPEED) / currentBuf) / laneVehicleSpeedLimit); // 0 .. 10000, m_speedLimit of highway is 2, actual max. vehicle speed on highway is 16, that's why we use x*8 == x<<3 (don't ask why CO uses different units for velocity)

				if (curRelSpeed >= (uint)GlobalConfig.Instance.DynamicLaneSelection.VolumeMeasurementRelSpeedThreshold * (uint)REF_REL_SPEED_PERCENT_DENOMINATOR) {
					ushort lastBuf = laneTrafficData[segmentId][laneIndex].lastTrafficBuffer;
					ushort maxBuf = laneTrafficData[segmentId][laneIndex].maxTrafficBuffer;

					float factor = Mathf.Clamp01(1f - (float)lastBuf / (float)maxBuf);
					curRelSpeed = (ushort)(curRelSpeed + (uint)(factor * (float)((uint)REF_REL_SPEED - (uint)curRelSpeed)));
				}
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
					laneTrafficData[segmentId][i].meanSpeed = REF_REL_SPEED;
				}
			}

			// calculate max./min. lane speed
			for (int i = 0; i < 2; ++i) {
				meanSpeeds[i] = 0;
				meanSpeedLanes[i] = 0;
			}

			curLaneId = segment.m_lanes;

			byte laneIndex = 0;
			while (laneIndex < numLanes && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

				if ((laneInfo.m_laneType & LANE_TYPES) != NetInfo.LaneType.None && (laneInfo.m_vehicleType & VEHICLE_TYPES) != VehicleInfo.VehicleType.None) {
					int dirIndex = GetDirIndex(laneInfo.m_finalDirection);

					// calculate reported mean speed
					ushort newRelSpeed = CalcLaneRelativeMeanSpeed(segmentId, laneIndex, curLaneId, segment.Info.m_lanes[laneIndex]);

					meanSpeeds[dirIndex] += newRelSpeed;
					++meanSpeedLanes[dirIndex];

					laneTrafficData[segmentId][laneIndex].meanSpeed = newRelSpeed;

					ushort trafficBuffer = laneTrafficData[segmentId][laneIndex].trafficBuffer;

					// remember historic data
					laneTrafficData[segmentId][laneIndex].lastTrafficBuffer = trafficBuffer;

					if (trafficBuffer > laneTrafficData[segmentId][laneIndex].maxTrafficBuffer) {
						laneTrafficData[segmentId][laneIndex].maxTrafficBuffer = trafficBuffer;
					}

					// reset buffers
					if (conf.AdvancedVehicleAI.MaxTrafficBuffer > 0) {
						if (laneTrafficData[segmentId][laneIndex].trafficBuffer > conf.AdvancedVehicleAI.MaxTrafficBuffer) {
							laneTrafficData[segmentId][laneIndex].accumulatedSpeeds /= (laneTrafficData[segmentId][laneIndex].trafficBuffer / conf.AdvancedVehicleAI.MaxTrafficBuffer);
							laneTrafficData[segmentId][laneIndex].trafficBuffer = (ushort)conf.AdvancedVehicleAI.MaxTrafficBuffer;
						} else if (laneTrafficData[segmentId][laneIndex].trafficBuffer == conf.AdvancedVehicleAI.MaxTrafficBuffer) {
							laneTrafficData[segmentId][laneIndex].accumulatedSpeeds = 0;
							laneTrafficData[segmentId][laneIndex].trafficBuffer = 0;
						}
					} else {
						laneTrafficData[segmentId][laneIndex].accumulatedSpeeds = 0;
						laneTrafficData[segmentId][laneIndex].trafficBuffer = 0;
					}
				}

				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}

			for (int i = 0; i < 2; ++i) {
				int segDirIndex = i == 0 ? GetDirIndex(segmentId, NetInfo.Direction.Forward) : GetDirIndex(segmentId, NetInfo.Direction.Backward);

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
			segmentDirTrafficData[fwdIndex].meanSpeed = REF_REL_SPEED;

			segmentDirTrafficData[backIndex] = default(SegmentDirTrafficData);
			segmentDirTrafficData[backIndex].meanSpeed = REF_REL_SPEED;
		}

		public void ResetTrafficStats() {
			for (int i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
				DestroySegmentStats((ushort)i);
			}
		}

		public void AddTraffic(ushort segmentId, byte laneIndex, ushort speed) {
			if (laneTrafficData[segmentId] == null || laneIndex >= laneTrafficData[segmentId].Length)
				return;

			laneTrafficData[segmentId][laneIndex].trafficBuffer = (ushort)Math.Min(65535u, (uint)laneTrafficData[segmentId][laneIndex].trafficBuffer + 1u);
			laneTrafficData[segmentId][laneIndex].accumulatedSpeeds += (uint)speed;
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
