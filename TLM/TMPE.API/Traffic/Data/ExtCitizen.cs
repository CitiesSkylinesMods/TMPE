using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.Traffic.Data {
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

		public override string ToString() {
			return $"[ExtCitizen\n" +
				"\t" + $"citizenId = {citizenId}\n" +
				"\t" + $"transportMode = {transportMode}\n" +
				"\t" + $"lastTransportMode = {transportMode}\n" +
				"\t" + $"lastLocation = {lastLocation}\n" +
				"ExtCitizen]";
		}

		public ExtCitizen(uint citizenId) {
			this.citizenId = citizenId;
			transportMode = ExtTransportMode.None;
			lastTransportMode = ExtTransportMode.None;
			lastLocation = Citizen.Location.Moving;
		}
	}
}
