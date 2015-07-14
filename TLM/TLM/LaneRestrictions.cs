namespace TrafficManager
{
    class LaneRestrictions
    {
        public uint laneID;
        public int laneNum;
        public NetInfo.Direction direction;

        //if (vehicleService == ItemClass.Service.Commercial)
        //    vehicleFlag |= 128;
        //else if (vehicleService == ItemClass.Service.FireDepartment)
        //    vehicleFlag |= 130;
        //else if (vehicleService == ItemClass.Service.Garbage)
        //    vehicleFlag |= 132;
        //else if (vehicleService == ItemClass.Service.HealthCare)
        //    vehicleFlag |= 134;
        //else if (vehicleService == ItemClass.Service.Industrial)
        //    vehicleFlag |= 136;
        //else if (vehicleService == ItemClass.Service.PoliceDepartment)
        //    vehicleFlag |= 138;
        //else if (vehicleService == ItemClass.Service.PublicTransport)
        //    vehicleFlag |= 140;
        public bool enableCars = true;
        public bool enableService = true;
        public bool enableTransport = true;
        public bool enableCargo = true;

        public int enabledTypes = 4;
        public readonly int maxAllowedTypes = 4;

        public LaneRestrictions(uint laneid, int laneNum, NetInfo.Direction dir)
        {
            this.laneID = laneid;
            this.laneNum = laneNum;
            this.direction = dir;
        }

        public void toggleCars()
        {
            enableCars = !enableCars;

            enabledTypes = enableCars ? enabledTypes + 1 : enabledTypes - 1;
        }

        public void toggleCargo()
        {
            enableCargo = !enableCargo;

            enabledTypes = enableCargo ? enabledTypes + 1 : enabledTypes - 1;
        }

        public void toggleService()
        {
            enableService = !enableService;

            enabledTypes = enableService ? enabledTypes + 1 : enabledTypes - 1;
        }

        public void toggleTransport()
        {
            enableTransport = !enableTransport;

            enabledTypes = enableTransport ? enabledTypes + 1 : enabledTypes - 1;
        }
    }
}