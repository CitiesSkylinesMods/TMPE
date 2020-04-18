namespace CitiesGameBridge.Service {
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using System;

    [Obsolete("Not used; it should be removed.")]
    public class PathService : IPathService {
        public static readonly IPathService Instance = new PathService();

        private PathService() { }

        public bool CheckUnitFlags(uint unitId, byte flagMask, byte? expectedResult = null) {

            int result =
                Singleton<PathManager>.instance.m_pathUnits.m_buffer[unitId].m_pathFindFlags
                & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }

        public void ProcessUnit(uint unitId, PathUnitHandler handler) {
            ProcessUnit(
                unitId,
                ref Singleton<PathManager>.instance.m_pathUnits.m_buffer[unitId],
                handler);
        }

        public void ProcessUnit(uint unitId, ref PathUnit unit, PathUnitHandler handler) {
            handler(unitId, ref unit);
        }
    }
}