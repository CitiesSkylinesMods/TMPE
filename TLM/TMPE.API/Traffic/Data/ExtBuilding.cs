namespace TrafficManager.API.Traffic.Data {
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Auto)]
    public struct ExtBuilding {
        /// <summary>
        /// Building id
        /// </summary>
        public ushort buildingId;

        /// <summary>
        /// Current parking space demand (0-100)
        /// </summary>
        public byte parkingSpaceDemand;

        /// <summary>
        /// Current incoming public transport demand (0-100)
        /// </summary>
        public byte incomingPublicTransportDemand;

        /// <summary>
        /// Current outgoing public transport demand (0-100)
        /// </summary>
        public byte outgoingPublicTransportDemand;

        public ExtBuilding(ushort buildingId) {
            this.buildingId = buildingId;
            parkingSpaceDemand = 0;
            incomingPublicTransportDemand = 0;
            outgoingPublicTransportDemand = 0;
        }

        public override string ToString() {
            return string.Format(
                "[ExtBuilding {0}\n\tbuildingId = {1}\n\tparkingSpaceDemand = {2}\n" +
                "\tincomingPublicTransportDemand = {3}\n\toutgoingPublicTransportDemand = {4}" +
                "\nExtBuilding]",
                base.ToString(),
                buildingId,
                parkingSpaceDemand,
                incomingPublicTransportDemand,
                outgoingPublicTransportDemand);
        }
    }
}