using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager {
	public class SpeedLimitManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<Configuration.LaneSpeedLimit>>, ICustomDataManager<Dictionary<string, float>> {
		public static SpeedLimitManager Instance { get; private set; } = null;

		private static readonly float MAX_SPEED = 6f; // 300 km/h
		private Dictionary<string, float[]> vanillaLaneSpeedLimitsByNetInfoName; // For each NetInfo (by name) and lane index: game default speed limit
		private Dictionary<string, List<string>> childNetInfoNamesByCustomizableNetInfoName; // For each NetInfo (by name): Parent NetInfo (name)
		private List<NetInfo> customizableNetInfos;

		internal Dictionary<string, int> CustomLaneSpeedLimitIndexByNetInfoName; // For each NetInfo (by name) and lane index: custom speed limit index
		internal Dictionary<string, NetInfo> NetInfoByName; // For each name: NetInfo

		private Dictionary<ushort, IDisposable> segGeometryUnsubscribers = new Dictionary<ushort, IDisposable>();

		static SpeedLimitManager() {
			Instance = new SpeedLimitManager();
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
			AvailableSpeedLimits.Add(110);
			AvailableSpeedLimits.Add(120);
			AvailableSpeedLimits.Add(130);
			AvailableSpeedLimits.Add(0);

			vanillaLaneSpeedLimitsByNetInfoName = new Dictionary<string, float[]>();
			CustomLaneSpeedLimitIndexByNetInfoName = new Dictionary<string, int>();
			customizableNetInfos = new List<NetInfo>();
			childNetInfoNamesByCustomizableNetInfoName = new Dictionary<string, List<string>>();
			NetInfoByName = new Dictionary<string, NetInfo>();
		}

		/// <summary>
		/// Determines if custom speed limits may be assigned to the given segment.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public bool MayHaveCustomSpeedLimits(ushort segmentId, ref NetSegment data) {
			if ((data.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
				return false;
			ItemClass connectionClass = data.Info.GetConnectionClass();
			return (connectionClass.m_service == ItemClass.Service.Road ||
				(connectionClass.m_service == ItemClass.Service.PublicTransport && (connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain || connectionClass.m_subService == ItemClass.SubService.PublicTransportMetro)));
		}

		/// <summary>
		/// Determines if custom speed limits may be assigned to the given lane info
		/// </summary>
		/// <param name="laneInfo"></param>
		/// <returns></returns>
		public bool MayHaveCustomSpeedLimits(NetInfo.Lane laneInfo) {
			return (laneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram)) != VehicleInfo.VehicleType.None;
		}

		/// <summary>
		/// Determines the currently set speed limit for the given segment and lane direction in terms of discrete speed limit levels.
		/// An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into a discrete speed limit value of 100 (km/h).
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="finalDir"></param>
		/// <returns></returns>
		public ushort GetCustomSpeedLimit(ushort segmentId, NetInfo.Direction finalDir) {
			// calculate the currently set mean speed limit
			if (segmentId == 0 || (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
				return 0;
			}

			var segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			float meanSpeedLimit = 0f;
			uint validLanes = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				NetInfo.Direction d = laneInfo.m_finalDirection;
				if (d != finalDir)
					goto nextIter;
				if (!MayHaveCustomSpeedLimits(laneInfo))
					goto nextIter;

				ushort? setSpeedLimit = Flags.getLaneSpeedLimit(curLaneId);
				if (setSpeedLimit != null)
					meanSpeedLimit += ToGameSpeedLimit((ushort)setSpeedLimit); // custom speed limit
				else
					meanSpeedLimit += laneInfo.m_speedLimit; // game default
				++validLanes;

				nextIter:
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}

			if (validLanes > 0)
				meanSpeedLimit /= (float)validLanes;
			ushort ret = LaneToCustomSpeedLimit(meanSpeedLimit);
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
			float meanSpeedLimit = 0f;
			uint validLanes = 0;
			for (int i = 0; i < segmentInfo.m_lanes.Length; ++i) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[i];
				NetInfo.Direction d = laneInfo.m_finalDirection;
				if (finalDir != null && d != finalDir)
					continue;
				if (!MayHaveCustomSpeedLimits(laneInfo))
					continue;
				
				meanSpeedLimit += laneInfo.m_speedLimit;
				++validLanes;
			}

			if (validLanes > 0)
				meanSpeedLimit /= (float)validLanes;
			ushort ret = LaneToCustomSpeedLimit(meanSpeedLimit);
			return ret;
		}

        /// <summary>
		/// Determines the average custom speed limit for a given NetInfo object in terms of discrete speed limit levels.
		/// An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into a discrete speed limit value of 100 (km/h).
		/// </summary>
		/// <param name="segmentInfo"></param>
		/// <param name="finalDir"></param>
		/// <returns></returns>
		public ushort GetAverageCustomSpeedLimit(ushort segmentId, ref NetSegment segment, NetInfo segmentInfo, NetInfo.Direction? finalDir = null) {
            // calculate the currently set mean speed limit
            float meanSpeedLimit = 0f;
            uint validLanes = 0;
            uint curLaneId = segment.m_lanes;
            for (uint laneIndex = 0; laneIndex < segmentInfo.m_lanes.Length; ++laneIndex) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				NetInfo.Direction d = laneInfo.m_finalDirection;
				if (finalDir != null && d != finalDir)
					continue;
				if (!MayHaveCustomSpeedLimits(laneInfo))
					continue;

                meanSpeedLimit += GetLockFreeGameSpeedLimit(segmentId, laneIndex, curLaneId, laneInfo);
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++validLanes;
            }

            if (validLanes > 0)
                meanSpeedLimit /= (float)validLanes;
            return (ushort)Mathf.Round(meanSpeedLimit);
        }

        /// <summary>
        /// Determines the currently set speed limit for the given lane in terms of discrete speed limit levels.
        /// An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="laneId"></param>
        /// <returns></returns>
        public ushort GetCustomSpeedLimit(uint laneId) {
			// check custom speed limit
			ushort? setSpeedLimit = Flags.getLaneSpeedLimit(laneId);
			if (setSpeedLimit != null) {
				return (ushort)setSpeedLimit;
			}

			// check default speed limit
			ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
			if (!MayHaveCustomSpeedLimits(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId])) {
				return 0;
			}
			
			var segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				if (curLaneId == laneId) {
					NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
					if (!MayHaveCustomSpeedLimits(laneInfo))
						return 0;

					ushort ret = LaneToCustomSpeedLimit(laneInfo.m_speedLimit);
					return ret;
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
		public float GetGameSpeedLimit(uint laneId) {
			return ToGameSpeedLimit(GetCustomSpeedLimit(laneId));
		}

		internal float GetLockFreeGameSpeedLimit(ushort segmentId, uint laneIndex, uint laneId, NetInfo.Lane laneInfo) {
			float speedLimit = 0;
			ushort?[] fastArray = Flags.laneSpeedLimitArray[segmentId];
			if (fastArray != null && fastArray.Length > laneIndex && fastArray[laneIndex] != null) {
				speedLimit = ToGameSpeedLimit((ushort)fastArray[laneIndex]);
			} else {
				speedLimit = laneInfo.m_speedLimit;
			}
			return speedLimit;
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
			else if (laneSpeedLimit < 1.35f)
				speedLimit = (ushort)((ushort)Mathf.Round(laneSpeedLimit * 10f) * 10u);

			return speedLimit;
		}

		/// <summary>
		/// Explicitly stores currently set speed limits for all segments of the specified NetInfo
		/// </summary>
		/// <param name="info"></param>
		public void FixCurrentSpeedLimits(NetInfo info) {
			if (info == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.FixCurrentSpeedLimits: info is null!");
#endif
				return;
			}

			if (info.name == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.FixCurrentSpeedLimits: info.name is null!");
#endif
				return;
			}

			if (!customizableNetInfos.Contains(info))
				return;

			for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
				if (!NetUtil.IsLaneValid(laneId))
					continue;

				ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
				NetInfo laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
				if (laneInfo.name != info.name && (!childNetInfoNamesByCustomizableNetInfoName.ContainsKey(info.name) || !childNetInfoNamesByCustomizableNetInfoName[info.name].Contains(laneInfo.name)))
					continue;

				Flags.setLaneSpeedLimit(laneId, GetCustomSpeedLimit(laneId));
				SubscribeToSegmentGeometry(segmentId);
			}
		}

		/// <summary>
		/// Explicitly clear currently set speed limits for all segments of the specified NetInfo
		/// </summary>
		/// <param name="info"></param>
		public void ClearCurrentSpeedLimits(NetInfo info) {
			if (info == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.ClearCurrentSpeedLimits: info is null!");
#endif
				return;
			}

			if (info.name == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.ClearCurrentSpeedLimits: info.name is null!");
#endif
				return;
			}

			if (!customizableNetInfos.Contains(info))
				return;

			for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
				if (!NetUtil.IsLaneValid(laneId))
					continue;

				NetInfo laneInfo = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment].Info;
				if (laneInfo.name != info.name && (!childNetInfoNamesByCustomizableNetInfoName.ContainsKey(info.name) || !childNetInfoNamesByCustomizableNetInfoName[info.name].Contains(laneInfo.name)))
					continue;

				Flags.removeLaneSpeedLimit(laneId);
			}
		}

		/// <summary>
		/// Determines the game default speed limit of the given NetInfo.
		/// </summary>
		/// <param name="info">the NetInfo of which the game default speed limit should be determined</param>
		/// <param name="roundToSignLimits">if true, custom speed limit are rounded to speed limits available as speed limit sign</param>
		/// <returns></returns>
		public ushort GetVanillaNetInfoSpeedLimit(NetInfo info, bool roundToSignLimits = true) {
			if (info == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info is null!");
#endif
				return 0;
			}

			if (info.m_netAI == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info.m_netAI is null!");
#endif
				return 0;
			}

			if (info.name == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info.name is null!");
#endif
				return 0;
			}

			/*if (! (info.m_netAI is RoadBaseAI))
				return 0;*/

			//string infoName = ((RoadBaseAI)info.m_netAI).m_info.name;
			string infoName = info.name;
			if (! vanillaLaneSpeedLimitsByNetInfoName.ContainsKey(infoName))
				return 0;

			float[] vanillaSpeedLimits = vanillaLaneSpeedLimitsByNetInfoName[infoName];
			float? maxSpeedLimit = null;
			foreach (float speedLimit in vanillaSpeedLimits) {
				if (maxSpeedLimit == null || speedLimit > maxSpeedLimit) {
					maxSpeedLimit = speedLimit;
				}
			}

			if (maxSpeedLimit == null)
				return 0;

			return LaneToCustomSpeedLimit((float)maxSpeedLimit, roundToSignLimits);
		}

		/// <summary>
		/// Determines the custom speed limit of the given NetInfo.
		/// </summary>
		/// <param name="info">the NetInfo of which the custom speed limit should be determined</param>
		/// <returns></returns>
		public int GetCustomNetInfoSpeedLimitIndex(NetInfo info) {
			if (info == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.SetCustomNetInfoSpeedLimitIndex: info is null!");
#endif
				return -1;
			}

			if (info.name == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.SetCustomNetInfoSpeedLimitIndex: info.name is null!");
#endif
				return -1;
			}

			/*if (!(info.m_netAI is RoadBaseAI))
				return -1;*/

			//string infoName = ((RoadBaseAI)info.m_netAI).m_info.name;
			string infoName = info.name;
			if (!CustomLaneSpeedLimitIndexByNetInfoName.ContainsKey(infoName))
				return AvailableSpeedLimits.IndexOf(GetVanillaNetInfoSpeedLimit(info, true));

			return CustomLaneSpeedLimitIndexByNetInfoName[infoName];
		}

		/// <summary>
		/// Sets the custom speed limit of the given NetInfo.
		/// </summary>
		/// <param name="info">the NetInfo for which the custom speed limit should be set</param>
		/// <returns></returns>
		public void SetCustomNetInfoSpeedLimitIndex(NetInfo info, int customSpeedLimitIndex) {
			if (info == null) {
#if DEBUG
				Log.Warning($"SetCustomNetInfoSpeedLimitIndex: info is null!");
#endif
				return;
			}

			if (info.name == null) {
#if DEBUG
				Log.Warning($"SetCustomNetInfoSpeedLimitIndex: info.name is null!");
#endif
				return;
			}

			/*if (!(info.m_netAI is RoadBaseAI))
				return;*/

			/*RoadBaseAI baseAI = (RoadBaseAI)info.m_netAI;
			string infoName = baseAI.m_info.name;*/
			string infoName = info.name;
			CustomLaneSpeedLimitIndexByNetInfoName[infoName] = customSpeedLimitIndex;

			float gameSpeedLimit = ToGameSpeedLimit(AvailableSpeedLimits[customSpeedLimitIndex]);

			// save speed limit in all NetInfos
			Log._Debug($"Updating parent NetInfo {infoName}: Setting speed limit to {gameSpeedLimit}");
			UpdateNetInfoGameSpeedLimit(info, gameSpeedLimit);

			if (childNetInfoNamesByCustomizableNetInfoName.ContainsKey(infoName)) {
				foreach (string childNetInfoName in childNetInfoNamesByCustomizableNetInfoName[infoName]) {
					if (NetInfoByName.ContainsKey(childNetInfoName)) {
						Log._Debug($"Updating child NetInfo {childNetInfoName}: Setting speed limit to {gameSpeedLimit}");
						CustomLaneSpeedLimitIndexByNetInfoName[childNetInfoName] = customSpeedLimitIndex;
						UpdateNetInfoGameSpeedLimit(NetInfoByName[childNetInfoName], gameSpeedLimit);
					}
				}
			}
		}

		private void UpdateNetInfoGameSpeedLimit(NetInfo info, float gameSpeedLimit) {
			if (info == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.UpdateNetInfoGameSpeedLimit: info is null!");
#endif
				return;
			}

			if (info.name == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.UpdateNetInfoGameSpeedLimit: info.name is null!");
#endif
				return;
			}

			if (info.m_lanes == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.UpdateNetInfoGameSpeedLimit: info.name is null!");
#endif
				return;
			}

			Log._Debug($"Updating speed limit of NetInfo {info.name} to {gameSpeedLimit}");

			foreach (NetInfo.Lane lane in info.m_lanes) {
				// TODO refactor check
				if ((lane.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram)) != VehicleInfo.VehicleType.None) {
					lane.m_speedLimit = gameSpeedLimit;
				}
			}
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
			if (!MayHaveCustomSpeedLimits(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId])) {
				return false;
			}
			if (!AvailableSpeedLimits.Contains(speedLimit)) {
				return false;
			}

			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

			if (segmentInfo == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.SetSpeedLimit: info is null!");
#endif
				return false;
			}

			if (segmentInfo.m_lanes == null) {
#if DEBUG
				Log.Warning($"SpeedLimitManager.SetSpeedLimit: info.name is null!");
#endif
				return false;
			}

			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
			int laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				NetInfo.Direction d = laneInfo.m_finalDirection;
				if (d != finalDir)
					goto nextIter;
				if (!MayHaveCustomSpeedLimits(laneInfo))
					goto nextIter;
				
#if DEBUG
				Log._Debug($"SpeedLimitManager: Setting speed limit of lane {curLaneId} to {speedLimit}");
#endif
				Flags.setLaneSpeedLimit(curLaneId, speedLimit);
				SubscribeToSegmentGeometry(segmentId);

			nextIter:
				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}

			return true;
		}

		public List<NetInfo> GetCustomizableNetInfos() {
			return customizableNetInfos;
		}

		public override void OnBeforeLoadData() {
			base.OnBeforeLoadData();

			// determine vanilla speed limits and customizable NetInfos
			SteamHelper.DLC_BitMask dlcMask = SteamHelper.GetOwnedDLCMask();

			int numLoaded = PrefabCollection<NetInfo>.LoadedCount();

			vanillaLaneSpeedLimitsByNetInfoName.Clear();
			customizableNetInfos.Clear();
			CustomLaneSpeedLimitIndexByNetInfoName.Clear();
			childNetInfoNamesByCustomizableNetInfoName.Clear();
			NetInfoByName.Clear();

			List<NetInfo> mainNetInfos = new List<NetInfo>();

			Log.Info($"SpeedLimitManager.OnBeforeLoadData: {numLoaded} NetInfos loaded.");
			for (uint i = 0; i < numLoaded; ++i) {
				NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);

				if (info == null || info.m_netAI == null || !(info.m_netAI is RoadBaseAI || info.m_netAI is MetroTrackAI || info.m_netAI is TrainTrackBaseAI) || !(info.m_dlcRequired == 0 || (uint)(info.m_dlcRequired & dlcMask) != 0u)) {
					if (info == null)
						Log.Warning($"SpeedLimitManager.OnBeforeLoadData: NetInfo @ {i} is null!");
					continue;
				}

				string infoName = info.name;
				if (infoName == null) {
					Log.Warning($"SpeedLimitManager.OnBeforeLoadData: NetInfo name @ {i} is null!");
					continue;
				}

				if (!vanillaLaneSpeedLimitsByNetInfoName.ContainsKey(infoName)) {
					if (info.m_lanes == null) {
						Log.Warning($"SpeedLimitManager.OnBeforeLoadData: NetInfo lanes @ {i} is null!");
						continue;
					}

					Log.Info($"Loaded road NetInfo: {infoName}");
					NetInfoByName[infoName] = info;
					mainNetInfos.Add(info);

					float[] vanillaLaneSpeedLimits = new float[info.m_lanes.Length];
					for (int k = 0; k < info.m_lanes.Length; ++k) {
						vanillaLaneSpeedLimits[k] = info.m_lanes[k].m_speedLimit;
					}
					vanillaLaneSpeedLimitsByNetInfoName[infoName] = vanillaLaneSpeedLimits;
				}
			}

			mainNetInfos.Sort(delegate(NetInfo a, NetInfo b) {
				bool aRoad = a.m_netAI is RoadBaseAI;
				bool bRoad = b.m_netAI is RoadBaseAI;

				if (aRoad != bRoad) {
					if (aRoad)
						return -1;
					else
						return 1;
				}

				bool aTrain = a.m_netAI is TrainTrackBaseAI;
				bool bTrain = b.m_netAI is TrainTrackBaseAI;

				if (aTrain != bTrain) {
					if (aTrain)
						return 1;
					else
						return -1;
				}

				bool aMetro = a.m_netAI is MetroTrackAI;
				bool bMetro = b.m_netAI is MetroTrackAI;

				if (aMetro != bMetro) {
					if (aMetro)
						return 1;
					else
						return -1;
				}

				if (aRoad && bRoad) {
					bool aHighway = ((RoadBaseAI)a.m_netAI).m_highwayRules;
					bool bHighway = ((RoadBaseAI)b.m_netAI).m_highwayRules;

					if (aHighway != bHighway) {
						if (aHighway)
							return 1;
						else
							return -1;
					}
				}

				int aNumVehicleLanes = 0;
				foreach (NetInfo.Lane lane in a.m_lanes) {
					if ((lane.m_laneType & NetInfo.LaneType.Vehicle) != NetInfo.LaneType.None)
						++aNumVehicleLanes;
				}

				int bNumVehicleLanes = 0;
				foreach (NetInfo.Lane lane in b.m_lanes) {
					if ((lane.m_laneType & NetInfo.LaneType.Vehicle) != NetInfo.LaneType.None)
						++bNumVehicleLanes;
				}

				int res = aNumVehicleLanes.CompareTo(bNumVehicleLanes);
				if (res == 0) {
					return a.name.CompareTo(b.name);
				} else {
					return res;
				}
			});

			// identify parent NetInfos
			int x = 0;
			while (x < mainNetInfos.Count) {
				NetInfo info = mainNetInfos[x];
				string infoName = info.name;

				// find parent with prefix name

				bool foundParent = false;
				for (int y = 0; y < mainNetInfos.Count; ++y) {
					NetInfo parentInfo = mainNetInfos[y];

					if (info.m_placementStyle == ItemClass.Placement.Procedural && !infoName.Equals(parentInfo.name) && infoName.StartsWith(parentInfo.name)) {
						Log.Info($"Identified child NetInfo {infoName} of parent {parentInfo.name}");
						if (!childNetInfoNamesByCustomizableNetInfoName.ContainsKey(parentInfo.name)) {
							childNetInfoNamesByCustomizableNetInfoName[parentInfo.name] = new List<string>();
						}
						childNetInfoNamesByCustomizableNetInfoName[parentInfo.name].Add(info.name);
						NetInfoByName[infoName] = info;
						foundParent = true;
						break;
					}
				}

				if (foundParent) {
					mainNetInfos.RemoveAt(x);
				} else {
					++x;
				}
			}

			customizableNetInfos = mainNetInfos;
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[geometry.SegmentId].Info;
			uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[geometry.SegmentId].m_lanes;
			int laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				ushort? setSpeedLimit = Flags.getLaneSpeedLimit(curLaneId);

				Flags.setLaneSpeedLimit(curLaneId, null);

				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {

		}

		public bool LoadData(List<Configuration.LaneSpeedLimit> data) {
			bool success = true;
			Log.Info($"Loading lane speed limit data. {data.Count} elements");
			foreach (Configuration.LaneSpeedLimit laneSpeedLimit in data) {
				try {
					if (!NetUtil.IsLaneValid(laneSpeedLimit.laneId)) {
						Log._Debug($"SpeedLimitManager.LoadData: Skipping lane {laneSpeedLimit.laneId}: Lane is invalid");
						continue;
					}

					ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneSpeedLimit.laneId].m_segment;
					NetInfo info = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
					int customSpeedLimitIndex = GetCustomNetInfoSpeedLimitIndex(info);
					Log._Debug($"SpeedLimitManager.LoadData: Handling lane {laneSpeedLimit.laneId}: Custom speed limit index of segment {segmentId} info ({info}, name={info?.name}, lanes={info?.m_lanes} is {customSpeedLimitIndex}");
					if (customSpeedLimitIndex < 0 || AvailableSpeedLimits[customSpeedLimitIndex] != laneSpeedLimit.speedLimit) {
						// lane speed limit differs from default speed limit
						Log._Debug($"SpeedLimitManager.LoadData: Loading lane speed limit: lane {laneSpeedLimit.laneId} = {laneSpeedLimit.speedLimit}");
						Flags.setLaneSpeedLimit(laneSpeedLimit.laneId, laneSpeedLimit.speedLimit);
						SubscribeToSegmentGeometry(segmentId);
					} else {
						Log._Debug($"SpeedLimitManager.LoadData: Skipping lane speed limit of lane {laneSpeedLimit.laneId} ({laneSpeedLimit.speedLimit})");
					}
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("SpeedLimitManager.LoadData: Error loading speed limits: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		List<Configuration.LaneSpeedLimit> ICustomDataManager<List<Configuration.LaneSpeedLimit>>.SaveData(ref bool success) {
			List<Configuration.LaneSpeedLimit> ret = new List<Configuration.LaneSpeedLimit>();
			foreach (KeyValuePair<uint, ushort> e in Flags.getAllLaneSpeedLimits()) {
				try {
					Configuration.LaneSpeedLimit laneSpeedLimit = new Configuration.LaneSpeedLimit(e.Key, e.Value);
					Log._Debug($"Saving speed limit of lane {laneSpeedLimit.laneId}: {laneSpeedLimit.speedLimit}");
					ret.Add(laneSpeedLimit);
				} catch (Exception ex) {
					Log.Error($"Exception occurred while saving lane speed limit @ {e.Key}: {ex.ToString()}");
					success = false;
				}
			}
			return ret;
		}

		public bool LoadData(Dictionary<string, float> data) {
			bool success = true;
			Log.Info($"Loading custom default speed limit data. {data.Count} elements");
			foreach (KeyValuePair<string, float> e in data) {
				if (!NetInfoByName.ContainsKey(e.Key))
					continue;

				ushort customSpeedLimit = LaneToCustomSpeedLimit(e.Value, true);
				int customSpeedLimitIndex = AvailableSpeedLimits.IndexOf(customSpeedLimit);
				if (customSpeedLimitIndex >= 0) {
					NetInfo info = NetInfoByName[e.Key];
					SetCustomNetInfoSpeedLimitIndex(info, customSpeedLimitIndex);
				}
			}
			return success;
		}

		Dictionary<string, float> ICustomDataManager<Dictionary<string, float>>.SaveData(ref bool success) {
			Dictionary<string, float> ret = new Dictionary<string, float>();
			foreach (KeyValuePair<string, int> e in CustomLaneSpeedLimitIndexByNetInfoName) {
				try {
					ushort customSpeedLimit = AvailableSpeedLimits[e.Value];
					float gameSpeedLimit = ToGameSpeedLimit(customSpeedLimit);

					ret.Add(e.Key, gameSpeedLimit);
				} catch (Exception ex) {
					Log.Error($"Exception occurred while saving custom default speed limits @ {e.Key}: {ex.ToString()}");
					success = false;
				}
			}
			return ret;
		}

#if DEBUG
		/*public Dictionary<NetInfo, ushort> GetDefaultSpeedLimits() {
			Dictionary<NetInfo, ushort> ret = new Dictionary<NetInfo, ushort>();
			int numLoaded = PrefabCollection<NetInfo>.LoadedCount();
			for (uint i = 0; i < numLoaded; ++i) {
				NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);
				ushort defaultSpeedLimit = GetAverageDefaultCustomSpeedLimit(info, NetInfo.Direction.Forward);
				ret.Add(info, defaultSpeedLimit);
				Log._Debug($"Loaded NetInfo: {info.name}, placementStyle={info.m_placementStyle}, availableIn={info.m_availableIn}, thumbnail={info.m_Thumbnail} connectionClass.service: {info.GetConnectionClass().m_service.ToString()}, connectionClass.subService: {info.GetConnectionClass().m_subService.ToString()}, avg. default speed limit: {defaultSpeedLimit}");
			}
			return ret;
		}*/
#endif
	}
}
