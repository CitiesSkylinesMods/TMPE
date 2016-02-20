using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Traffic {
	class SpeedLimitManager {
		public static readonly List<ushort> AvailableSpeedLimits;

		static SpeedLimitManager() {
			AvailableSpeedLimits = new List<ushort>();
			AvailableSpeedLimits.Add(10);
			AvailableSpeedLimits.Add(20);
			AvailableSpeedLimits.Add(30);
			AvailableSpeedLimits.Add(40);
			AvailableSpeedLimits.Add(50);
			AvailableSpeedLimits.Add(60);
			AvailableSpeedLimits.Add(70);
			AvailableSpeedLimits.Add(80);
			AvailableSpeedLimits.Add(90);
			AvailableSpeedLimits.Add(100);
			AvailableSpeedLimits.Add(120);
			AvailableSpeedLimits.Add(130);
			AvailableSpeedLimits.Add(0);
		}

		/// <summary>
		/// Determines the currently set speed limit for the given segment and lane direction in terms of discrete speed limit levels.
		/// An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into a discrete speed limit value of 100 (km/h).
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="dir"></param>
		/// <returns></returns>
		public static ushort GetCustomSpeedLimit(ushort segmentId, NetInfo.Direction dir) {
			// calculate the currently set mean speed limit
			if (segmentId == 0)
				return 0;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return 0;

			var segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			float meanSpeedLimit = 0f;
			uint validLanes = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Direction d = segmentInfo.m_lanes[laneIndex].m_direction;
				if ((segmentInfo.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None || d != dir)
					goto nextIter;

				ushort? setSpeedLimit = Flags.getLaneSpeedLimit(curLaneId);
				if (setSpeedLimit != null)
					meanSpeedLimit += ToGameSpeedLimit((ushort)setSpeedLimit); // custom speed limit
				else
					meanSpeedLimit += segmentInfo.m_lanes[laneIndex].m_speedLimit; // game default
				++validLanes;

				nextIter:
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}

			if (validLanes > 0)
				meanSpeedLimit /= (float)validLanes;
			return ToCustomSpeedLimit(meanSpeedLimit);
		}

		/// <summary>
		/// Determines the average default speed limit for a given NetInfo object in terms of discrete speed limit levels.
		/// An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into a discrete speed limit value of 100 (km/h).
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <param name="dir"></param>
		/// <returns></returns>
		public static ushort GetAverageDefaultCustomSpeedLimit(NetInfo segmentInfo, NetInfo.Direction? dir=null) {
			// calculate the currently set mean speed limit
			float meanSpeedLimit = 0f;
			uint validLanes = 0;
			for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
				NetInfo.Direction d = segmentInfo.m_lanes[i].m_direction;
				if ((segmentInfo.m_lanes[i].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None || (dir != null && d != dir))
					continue;

				meanSpeedLimit += segmentInfo.m_lanes[i].m_speedLimit;
				++validLanes;
			}

			if (validLanes > 0)
				meanSpeedLimit /= (float)validLanes;
			return ToCustomSpeedLimit(meanSpeedLimit);
		}

		/// <summary>
		/// Determines the currently set speed limit for the given lane in terms of discrete speed limit levels.
		/// An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into a discrete speed limit value of 100 (km/h).
		/// </summary>
		/// <param name="laneId"></param>
		/// <returns></returns>
		public static ushort GetCustomSpeedLimit(uint laneId) {
			// check custom speed limit
			ushort? setSpeedLimit = Flags.getLaneSpeedLimit(laneId);
			if (setSpeedLimit != null)
				return (ushort)setSpeedLimit;

			// check default speed limit
			ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;

			if (segmentId == 0)
				return 0;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return 0;

			var segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				if (curLaneId == laneId) {
					return ToCustomSpeedLimit(segmentInfo.m_lanes[laneIndex].m_speedLimit);
				}

				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}

			Log.Warning($"Speed limit for lane {laneId} could not be determined.");
            return 0; // no speed limit found
		}

		/// <summary>
		/// Determines the currently set speed limit for the given lane in terms of game (floating point) speed limit levels
		/// </summary>
		/// <param name="laneId"></param>
		/// <returns></returns>
		public static float GetGameSpeedLimit(uint laneId) {
			return ToGameSpeedLimit(GetCustomSpeedLimit(laneId));
		}

		internal static float GetLockFreeGameSpeedLimit(ushort segmentId, uint laneIndex, uint laneId, NetInfo.Lane laneInfo) {
			if (Flags.IsInitDone()) {
				float speedLimit = 0;

				ushort?[] fastArray = Flags.laneSpeedLimitArray[segmentId];
				if (fastArray != null && fastArray.Length >= laneIndex && fastArray[laneIndex] != null) {
					speedLimit = ToGameSpeedLimit((ushort)fastArray[laneIndex]);
				} else {
					speedLimit = laneInfo.m_speedLimit;
				}
				return speedLimit;
			} else
				return GetGameSpeedLimit(laneId);
		}

		/// <summary>
		/// Converts a custom speed limit to a game speed limit.
		/// </summary>
		/// <param name="customSpeedLimit"></param>
		/// <returns></returns>
		public static float ToGameSpeedLimit(ushort customSpeedLimit) {
			if (customSpeedLimit == 0)
				return 4f;
			return (float)customSpeedLimit / 50f;
		}

		/// <summary>
		/// Converts a game speed limit to a custom speed limit.
		/// </summary>
		/// <param name="gameSpeedLimit"></param>
		/// <returns></returns>
		public static ushort ToCustomSpeedLimit(float gameSpeedLimit) {
			gameSpeedLimit /= 2f; // 1 == 100 km/h

			// translate the floating point speed limit into our discrete version
			ushort speedLimit = 0;
			if (gameSpeedLimit < 0.15f)
				speedLimit = 10;
			else if (gameSpeedLimit < 1.15f)
				speedLimit = (ushort)((ushort)Math.Round(gameSpeedLimit * 10f) * 10u);
			else if (gameSpeedLimit < 1.25f)
				speedLimit = 120;
			else if (gameSpeedLimit < 1.35f)
				speedLimit = 130;

			return speedLimit;
		}

		/// <summary>
		/// Sets the speed limit of a given segment and lane direction.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="dir"></param>
		/// <param name="speedLimit"></param>
		/// <returns></returns>
		public static bool SetSpeedLimit(ushort segmentId, NetInfo.Direction dir, ushort speedLimit) {
			if (segmentId == 0)
				return false;
			if (!AvailableSpeedLimits.Contains(speedLimit))
				return false;
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return false;

			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			while (laneIndex < Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Direction d = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_direction;
				if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None || d != dir)
					goto nextIter;

				Log._Debug($"SpeedLimitManager: Setting speed limit of lane {curLaneId} to {speedLimit}");
				Flags.setLaneSpeedLimit(curLaneId, speedLimit);

				nextIter:
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}

			return true;
		}

		public static Dictionary<NetInfo, ushort> GetDefaultSpeedLimits() {
			Dictionary<NetInfo, ushort> ret = new Dictionary<NetInfo, ushort>();
			int numLoaded = PrefabCollection<NetInfo>.LoadedCount();
			for (uint i = 0; i < numLoaded; ++i) {
				NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);
				ushort defaultSpeedLimit = GetAverageDefaultCustomSpeedLimit(info, NetInfo.Direction.Forward);
				ret.Add(info, defaultSpeedLimit);
				Log._Debug($"Loaded NetInfo: {info.name}, connectionClass.service: {info.GetConnectionClass().m_service.ToString()}, connectionClass.subService: {info.GetConnectionClass().m_subService.ToString()}, avg. default speed limit: {defaultSpeedLimit}");
			}
			return ret;
		}
	}
}
