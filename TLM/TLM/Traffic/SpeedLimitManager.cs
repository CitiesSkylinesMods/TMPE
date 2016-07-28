using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Traffic {
	public class SpeedLimitManager {
		private static SpeedLimitManager instance = null;
		private static readonly float MAX_SPEED = 6f; // 300 km/h

		public static SpeedLimitManager Instance() {
			if (instance == null)
				instance = new SpeedLimitManager();
			return instance;
		}

		static SpeedLimitManager() {
			Instance();
		}

		public readonly List<ushort> AvailableSpeedLimits;

		private SpeedLimitManager() {
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
		/// <param name="finalDir"></param>
		/// <returns></returns>
		public ushort GetCustomSpeedLimit(ushort segmentId, NetInfo.Direction finalDir) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("SpeedLimitManager.GetCustomSpeedLimit1");
#endif
			// calculate the currently set mean speed limit
			if (segmentId == 0 || (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetCustomSpeedLimit1");
#endif
				return 0;
			}

			var segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			float meanSpeedLimit = 0f;
			uint validLanes = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Direction d = segmentInfo.m_lanes[laneIndex].m_finalDirection;
				if ((segmentInfo.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None || d != finalDir)
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
			ushort ret = LaneToCustomSpeedLimit(meanSpeedLimit);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetCustomSpeedLimit1");
#endif
			return ret;
		}

		/// <summary>
		/// Determines the average default speed limit for a given NetInfo object in terms of discrete speed limit levels.
		/// An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into a discrete speed limit value of 100 (km/h).
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <param name="finalDir"></param>
		/// <returns></returns>
		public ushort GetAverageDefaultCustomSpeedLimit(NetInfo segmentInfo, NetInfo.Direction? finalDir=null) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("SpeedLimitManager.GetAverageDefaultCustomSpeedLimit");
#endif

			// calculate the currently set mean speed limit
			float meanSpeedLimit = 0f;
			uint validLanes = 0;
			for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
				NetInfo.Direction d = segmentInfo.m_lanes[i].m_finalDirection;
				if ((segmentInfo.m_lanes[i].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None || (finalDir != null && d != finalDir))
					continue;

				meanSpeedLimit += segmentInfo.m_lanes[i].m_speedLimit;
				++validLanes;
			}

			if (validLanes > 0)
				meanSpeedLimit /= (float)validLanes;
			ushort ret = LaneToCustomSpeedLimit(meanSpeedLimit);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetAverageDefaultCustomSpeedLimit");
#endif
			return ret;
		}

		/// <summary>
		/// Determines the currently set speed limit for the given lane in terms of discrete speed limit levels.
		/// An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into a discrete speed limit value of 100 (km/h).
		/// </summary>
		/// <param name="laneId"></param>
		/// <returns></returns>
		public ushort GetCustomSpeedLimit(uint laneId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("SpeedLimitManager.GetCustomSpeedLimit2");
#endif
			// check custom speed limit
			ushort? setSpeedLimit = Flags.getLaneSpeedLimit(laneId);
			if (setSpeedLimit != null) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetCustomSpeedLimit2");
#endif
				return (ushort)setSpeedLimit;
			}

			// check default speed limit
			ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;

			if (segmentId == 0 || (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetCustomSpeedLimit2");
#endif
				return 0;
			}

			var segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				if (curLaneId == laneId) {
					ushort ret = LaneToCustomSpeedLimit(segmentInfo.m_lanes[laneIndex].m_speedLimit);
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetCustomSpeedLimit2");
#endif
					return ret;
				}

				laneIndex++;
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
			}

			Log.Warning($"Speed limit for lane {laneId} could not be determined.");
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetCustomSpeedLimit2");
#endif
			return 0; // no speed limit found
		}

		/// <summary>
		/// Determines the currently set speed limit for the given lane in terms of game (floating point) speed limit levels
		/// </summary>
		/// <param name="laneId"></param>
		/// <returns></returns>
		public float GetGameSpeedLimit(uint laneId) {
			return ToGameSpeedLimit(GetCustomSpeedLimit(laneId));
		}

		internal float GetLockFreeGameSpeedLimit(ushort segmentId, uint laneIndex, uint laneId, NetInfo.Lane laneInfo) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("SpeedLimitManager.GetLockFreeGameSpeedLimit");
#endif
			if (Flags.IsInitDone()) {
				if (Flags.laneSpeedLimitArray.Length <= segmentId) {
					Log.Error($"laneSpeedLimitArray.Length = {Flags.laneSpeedLimitArray.Length}, segmentId={segmentId}. Out of range!");
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetLockFreeGameSpeedLimit");
#endif
					return laneInfo.m_speedLimit;
				}

				float speedLimit = 0;
				ushort?[] fastArray = Flags.laneSpeedLimitArray[segmentId];
				if (fastArray != null && fastArray.Length > laneIndex && fastArray[laneIndex] != null) {
					speedLimit = ToGameSpeedLimit((ushort)fastArray[laneIndex]);
				} else {
					speedLimit = laneInfo.m_speedLimit;
				}
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetLockFreeGameSpeedLimit");
#endif
				return speedLimit;
			} else {
				float ret = GetGameSpeedLimit(laneId);
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.GetLockFreeGameSpeedLimit");
#endif
				return ret;
			}
		}

		/// <summary>
		/// Converts a custom speed limit to a game speed limit.
		/// </summary>
		/// <param name="customSpeedLimit"></param>
		/// <returns></returns>
		public float ToGameSpeedLimit(ushort customSpeedLimit) {
			if (customSpeedLimit == 0)
				return MAX_SPEED;
			return (float)customSpeedLimit / 50f;
		}

		/// <summary>
		/// Converts a lane speed limit to a custom speed limit.
		/// </summary>
		/// <param name="laneSpeedLimit"></param>
		/// <returns></returns>
		public ushort LaneToCustomSpeedLimit(float laneSpeedLimit, bool roundToSignLimits=true) {
			laneSpeedLimit /= 2f; // 1 == 100 km/h

			if (! roundToSignLimits) {
				return (ushort)Mathf.Round(laneSpeedLimit * 100f);
			}

			// translate the floating point speed limit into our discrete version
			ushort speedLimit = 0;
			if (laneSpeedLimit < 0.15f)
				speedLimit = 10;
			else if (laneSpeedLimit < 1.15f)
				speedLimit = (ushort)((ushort)Math.Round(laneSpeedLimit * 10f) * 10u);
			else if (laneSpeedLimit < 1.25f)
				speedLimit = 120;
			else if (laneSpeedLimit < 1.35f)
				speedLimit = 130;

			return speedLimit;
		}

		/// <summary>
		/// Converts a vehicle's velocity to a custom speed.
		/// </summary>
		/// <param name="vehicleSpeed"></param>
		/// <returns></returns>
		public ushort VehicleToCustomSpeed(float vehicleSpeed) {
			return LaneToCustomSpeedLimit(vehicleSpeed / 8f, false);
		}

		/// <summary>
		/// Sets the speed limit of a given segment and lane direction.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="finalDir"></param>
		/// <param name="speedLimit"></param>
		/// <returns></returns>
		public bool SetSpeedLimit(ushort segmentId, NetInfo.Direction finalDir, ushort speedLimit) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("SpeedLimitManager.SetSpeedLimit");
#endif
			if (segmentId == 0 || !AvailableSpeedLimits.Contains(speedLimit) || (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.SetSpeedLimit");
#endif
				return false;
			}

			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			while (laneIndex < Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Direction d = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection;
				if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == NetInfo.LaneType.None || d != finalDir)
					goto nextIter;

#if DEBUG
				Log._Debug($"SpeedLimitManager: Setting speed limit of lane {curLaneId} to {speedLimit}");
#endif
				Flags.setLaneSpeedLimit(curLaneId, speedLimit);

				nextIter:
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("SpeedLimitManager.SetSpeedLimit");
#endif
			return true;
		}

#if DEBUG
		public Dictionary<NetInfo, ushort> GetDefaultSpeedLimits() {
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
#endif
	}
}
