namespace TrafficManager.State.ConfigData {
    public class ParkingAI {
        /// <summary>
        /// Target position randomization to allow opposite road-side parking
        /// </summary>
        public uint ParkingSpacePositionRand = 32;

        /// <summary>
        /// parking space search in vicinity is randomized. Cims do not always select the nearest parking space possible.
        /// A value of 1u always selects the nearest parking space.
        /// A value of 2u selects the nearest parking space with 50% chance, the next one with 25%, then 12.5% and so on.
        /// A value of 3u selects the nearest parking space with 66.67% chance, the next one with 22.22%, then 7.4% and so on.
        /// A value of 4u selects the nearest parking space with 75% chance, the next one with 18.75%, then 4.6875% and so on.
        /// A value of N selects the nearest parking space with (N-1)/N chance, the next one with (1-(N-1)/N)*(N-1)/N, then (1-(N-1)/N)^2*(N-1)/N and so on.
        /// </summary>
        public uint VicinityParkingSpaceSelectionRand = 4u;

        /// <summary>
        /// maximum number of parking attempts for passenger cars
        /// </summary>
        public int MaxParkingAttempts = 10;

        /// <summary>
        /// maximum required squared distance between citizen instance and parked vehicle before the parked car is turned into a vehicle
        /// </summary>
        public float MaxParkedCarInstanceSwitchSqrDistance = 6f;

        /// <summary>
        /// maximum distance between building and pedestrian lane
        /// </summary>
        public float MaxBuildingToPedestrianLaneDistance = 96f;

        /// <summary>
        /// Maximum allowed distance between home/source building and parked car when traveling home without forced to use the car
        /// </summary>
        public float MaxParkedCarDistanceToHome = 256f;

        /// <summary>
        /// minimum required distance between target building and parked car for using a car
        /// </summary>
        public float MaxParkedCarDistanceToBuilding = 512f;

        /// <summary>
        /// public transport demand increment on path-find failure
        /// </summary>
        public uint PublicTransportDemandIncrement = 10u;

        /// <summary>
        /// public transport demand increment if waiting time was exceeded
        /// </summary>
        public uint PublicTransportDemandWaitingIncrement = 3u;

        /// <summary>
        /// public transport demand decrement on simulation step
        /// </summary>
        public uint PublicTransportDemandDecrement = 1u;

        /// <summary>
        /// public transport demand decrement on path-find success
        /// </summary>
        public uint PublicTransportDemandUsageDecrement = 7u;

        /// <summary>
        /// parking space demand decrement on simulation step
        /// </summary>
        public uint ParkingSpaceDemandDecrement = 1u;

        /// <summary>
        /// minimum parking space demand delta when a passenger car could be spawned
        /// </summary>
        public int MinSpawnedCarParkingSpaceDemandDelta = -5;

        /// <summary>
        /// maximum parking space demand delta when a passenger car could be spawned
        /// </summary>
        public int MaxSpawnedCarParkingSpaceDemandDelta = 3;

        /// <summary>
        /// minimum parking space demand delta when a parking spot could be found
        /// </summary>
        public int MinFoundParkPosParkingSpaceDemandDelta = -5;

        /// <summary>
        /// maximum parking space demand delta when a parking spot could be found
        /// </summary>
        public int MaxFoundParkPosParkingSpaceDemandDelta = 3;

        /// <summary>
        /// parking space demand increment when no parking spot could be found while trying to park
        /// </summary>
        public uint FailedParkingSpaceDemandIncrement = 5u;

        /// <summary>
        /// parking space demand increment when no parking spot could be found while trying to spawn a parked vehicle
        /// </summary>
        public uint FailedSpawnParkingSpaceDemandIncrement = 10u;
    }
}