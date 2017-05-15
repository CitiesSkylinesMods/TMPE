using ColossalFramework;
using CSUtil.Commons;
using GenericGameBridge.Service;

namespace CitiesGameBridge.Service {
	public class BuildingService : IBuildingService {
		public static readonly IBuildingService Instance = new BuildingService();

		private BuildingService() {

		}

		public bool IsBuildingValid(ushort buildingId) {
			return CheckBuildingFlags(buildingId, Building.Flags.Created | Building.Flags.Deleted, Building.Flags.Created);
		}

		public bool CheckBuildingFlags(ushort buildingId, Building.Flags flagMask, Building.Flags? expectedResult = default(Building.Flags?)) {
			bool ret = false;
			ProcessBuilding(buildingId, delegate (ushort bId, ref Building building) {
				ret = LogicUtil.CheckFlags((uint)building.m_flags, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
		}

		public void ProcessBuilding(ushort buildingId, BuildingHandler handler) {
			ProcessBuilding(buildingId, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingId], handler);
		}

		public void ProcessBuilding(ushort buildingId, ref Building building, BuildingHandler handler) {
			handler(buildingId, ref building);
		}
	}
}
