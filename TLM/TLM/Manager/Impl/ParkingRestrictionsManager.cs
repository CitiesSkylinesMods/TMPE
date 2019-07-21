using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.Traffic.Data;

namespace TrafficManager.Manager.Impl {
    using API.Manager;

    public class ParkingRestrictionsManager : AbstractGeometryObservingManager, ICustomDataManager<List<Configuration.ParkingRestriction>>, IParkingRestrictionsManager {
		public const NetInfo.LaneType LANE_TYPES = NetInfo.LaneType.Parking;
		public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

		public static readonly ParkingRestrictionsManager Instance = new ParkingRestrictionsManager();

		private bool[][] parkingAllowed;

		private ParkingRestrictionsManager() {
			
		}

		public bool MayHaveParkingRestriction(ushort segmentId) {
			bool ret = false;
			Services.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
				if ((segment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
					return true;
				}
				ItemClass connectionClass = segment.Info.GetConnectionClass();
				ret = connectionClass.m_service == ItemClass.Service.Road && segment.Info.m_hasParkingSpaces;
				return true;
			});
			return ret;
		}

		public bool IsParkingAllowed(ushort segmentId, NetInfo.Direction finalDir) {
			return parkingAllowed[segmentId][GetDirIndex(finalDir)];
		}

		public bool ToggleParkingAllowed(ushort segmentId, NetInfo.Direction finalDir) {
			return SetParkingAllowed(segmentId, finalDir, !IsParkingAllowed(segmentId, finalDir));
		}

		public bool SetParkingAllowed(ushort segmentId, NetInfo.Direction finalDir, bool flag) {
			if (!MayHaveParkingRestriction(segmentId)) {
				return false;
			}
			int dirIndex = GetDirIndex(finalDir);
			parkingAllowed[segmentId][dirIndex] = flag;
			if (!flag || !parkingAllowed[segmentId][1 - dirIndex]) {
				Services.SimulationService.AddAction(() => {
					// force relocation of illegaly parked vehicles
					Services.NetService.ProcessSegment(segmentId, delegate (ushort segId, ref NetSegment segment) {
						segment.UpdateSegment(segmentId);
						return true;
					});
				});
			}
			return true;
		}

		protected override void HandleInvalidSegment(ref ExtSegment seg) {
			parkingAllowed[seg.segmentId][0] = true;
			parkingAllowed[seg.segmentId][1] = true;
		}

		protected override void HandleValidSegment(ref ExtSegment seg) {
			if (! MayHaveParkingRestriction(seg.segmentId)) {
				parkingAllowed[seg.segmentId][0] = true;
				parkingAllowed[seg.segmentId][1] = true;
			}
		}

		protected int GetDirIndex(NetInfo.Direction dir) {
			return dir == NetInfo.Direction.Backward ? 1 : 0;
		}

		public override void OnBeforeLoadData() {
			base.OnBeforeLoadData();

			parkingAllowed = new bool[NetManager.MAX_SEGMENT_COUNT][];
			for (uint segmentId = 0; segmentId < parkingAllowed.Length; ++segmentId) {
				parkingAllowed[segmentId] = new bool[2];
				for (int i = 0; i < 2; ++i) {
					parkingAllowed[segmentId][i] = true;
				}
			}
		}

		public bool LoadData(List<Configuration.ParkingRestriction> data) {
			bool success = true;
			Log.Info($"Loading parking restrictions data. {data.Count} elements");
			foreach (Configuration.ParkingRestriction restr in data) {
				try {
					Log._Trace($"Setting forwardParkingAllowed={restr.forwardParkingAllowed}, backwardParkingAllowed={restr.backwardParkingAllowed} at segment {restr.segmentId}");
					SetParkingAllowed(restr.segmentId, NetInfo.Direction.Forward, restr.forwardParkingAllowed);
					SetParkingAllowed(restr.segmentId, NetInfo.Direction.Backward, restr.backwardParkingAllowed);
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading data from parking restrictions: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		public List<Configuration.ParkingRestriction> SaveData(ref bool success) {
			List<Configuration.ParkingRestriction> ret = new List<Configuration.ParkingRestriction>();
			for (uint segmentId = 0; segmentId < parkingAllowed.Length; ++segmentId) {
				try {
					if (parkingAllowed[segmentId][0] && parkingAllowed[segmentId][1]) {
						continue;
					}

					Configuration.ParkingRestriction restr = new Configuration.ParkingRestriction((ushort)segmentId);
					restr.forwardParkingAllowed = parkingAllowed[segmentId][0];
					restr.backwardParkingAllowed = parkingAllowed[segmentId][1];
					Log._Trace($"Saving forwardParkingAllowed={restr.forwardParkingAllowed}, backwardParkingAllowed={restr.backwardParkingAllowed} at segment {restr.segmentId}");
					ret.Add(restr);
				} catch (Exception ex) {
					Log.Error($"Exception occurred while saving parking restrictions @ {segmentId}: {ex.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}
