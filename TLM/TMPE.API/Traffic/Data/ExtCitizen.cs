namespace TrafficManager.API.Traffic.Data {
    using System.Runtime.InteropServices;
    using TrafficManager.API.Traffic.Enums;

    [StructLayout(LayoutKind.Auto)]
    public struct ExtCitizen {
        public uint citizenId;

        /// <summary>
        /// Mode of transport that is currently used to reach a destination
        /// </summary>
        public ExtTransportMode transportMode;

        /// <summary>
        /// Mode of transport that was previously used to reach a destination
        /// </summary>
        public ExtTransportMode lastTransportMode;

        /// <summary>
        /// Previous building location
        /// </summary>
        public Citizen.Location lastLocation;

        public ExtCitizen(uint citizenId) {
            this.citizenId = citizenId;
            transportMode = ExtTransportMode.None;
            lastTransportMode = ExtTransportMode.None;
            lastLocation = Citizen.Location.Moving;
        }

        public override string ToString() {
            return string.Format(
                "[ExtCitizen\n\tcitizenId = {0}\n\ttransportMode = {1}\n\tlastTransportMode = {2}\n" +
                "\tlastLocation = {3}\nExtCitizen]",
                citizenId,
                transportMode,
                transportMode,
                lastLocation);
        }
    }
}