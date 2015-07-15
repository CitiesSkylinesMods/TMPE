namespace TrafficManager
{
    class LaneRestrictions
    {
        public uint LaneId;
        public int LaneNum;
        public NetInfo.Direction Direction;

        public bool EnableCars = true;
        public bool EnableService = true;
        public bool EnableTransport = true;
        public bool EnableCargo = true;

        public int EnabledTypes = 4;
        public readonly int MaxAllowedTypes = 4;

        public LaneRestrictions(uint laneid, int laneNum, NetInfo.Direction dir)
        {
            LaneId = laneid;
            LaneNum = laneNum;
            Direction = dir;
        }

        public void ToggleCars()
        {
            EnableCars = !EnableCars;

            EnabledTypes = EnableCars ? EnabledTypes + 1 : EnabledTypes - 1;
        }

        public void ToggleCargo()
        {
            EnableCargo = !EnableCargo;

            EnabledTypes = EnableCargo ? EnabledTypes + 1 : EnabledTypes - 1;
        }

        public void ToggleService()
        {
            EnableService = !EnableService;

            EnabledTypes = EnableService ? EnabledTypes + 1 : EnabledTypes - 1;
        }

        public void ToggleTransport()
        {
            EnableTransport = !EnableTransport;

            EnabledTypes = EnableTransport ? EnabledTypes + 1 : EnabledTypes - 1;
        }
    }
}
