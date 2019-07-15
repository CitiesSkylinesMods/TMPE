namespace TrafficManager.Traffic.Data {
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

		public override string ToString() {
			return $"[ExtBuilding {base.ToString()}\n" +
				"\t" + $"buildingId = {buildingId}\n" +
				"\t" + $"parkingSpaceDemand = {parkingSpaceDemand}\n" +
				"\t" + $"incomingPublicTransportDemand = {incomingPublicTransportDemand}\n" +
				"\t" + $"outgoingPublicTransportDemand = {outgoingPublicTransportDemand}\n" +
				"ExtBuilding]";
		}

		public ExtBuilding(ushort buildingId) {
			this.buildingId = buildingId;
			parkingSpaceDemand = 0;
			incomingPublicTransportDemand = 0;
			outgoingPublicTransportDemand = 0;
		}
	}
}
