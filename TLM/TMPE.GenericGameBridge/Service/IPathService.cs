namespace GenericGameBridge.Service {
    public delegate bool PathUnitHandler(uint unitId, ref PathUnit unit);

    public interface IPathService {
        bool CheckUnitFlags(uint unitId, byte flagMask, byte? expectedResult = null);

        void ProcessUnit(uint unitId, PathUnitHandler handler);

        void ProcessUnit(uint unitId, ref PathUnit unit, PathUnitHandler handler);
    }
}