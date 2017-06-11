using ColossalFramework;
using CSUtil.Commons;
using GenericGameBridge.Service;
using System;
using System.Collections.Generic;

namespace CitiesGameBridge.Service {
	public class PathService : IPathService {
		public static readonly IPathService Instance = new PathService();

		private PathService() {
			
		}
		
		public bool CheckUnitFlags(uint unitId, byte flagMask, byte? expectedResult=null) {
			bool ret = false;
			ProcessUnit(unitId, delegate (uint uId, ref PathUnit unit) {
				ret = LogicUtil.CheckFlags((uint)unit.m_pathFindFlags, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
		}

		public void ProcessUnit(uint unitId, PathUnitHandler handler) {
			ProcessUnit(unitId, ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[unitId], handler);
		}

		public void ProcessUnit(uint unitId, ref PathUnit unit, PathUnitHandler handler) {
			handler(unitId, ref unit);
		}
	}
}
