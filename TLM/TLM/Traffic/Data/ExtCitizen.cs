using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Traffic.Data {
	public struct ExtCitizen {
		[Flags]
		public enum ExtTransportMode {
			/// <summary>
			/// No information about which mode of transport is used
			/// </summary>
			None = 0,
			/// <summary>
			/// Travelling by car
			/// </summary>
			Car = 1,
			/// <summary>
			/// Travelling by means of public transport
			/// </summary>
			PublicTransport = 2
		}

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

		internal ExtCitizen(uint citizenId) {
			this.citizenId = citizenId;
			transportMode = ExtTransportMode.None;
			lastTransportMode = ExtTransportMode.None;
			lastLocation = Citizen.Location.Moving;
			ResetLastLocation();
		}

		internal bool IsValid() {
			return Constants.ServiceFactory.CitizenService.IsCitizenValid(citizenId);
		}

		internal void Reset() {
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[4]) {
				Log.Warning($"ExtCitizen.Reset({citizenId}): Resetting ext. citizen {citizenId}");
			}
#endif
			transportMode = ExtTransportMode.None;
			lastTransportMode = ExtTransportMode.None;
			ResetLastLocation();
		}

		private void ResetLastLocation() {
			Citizen.Location loc = Citizen.Location.Moving;
			Constants.ServiceFactory.CitizenService.ProcessCitizen(citizenId, delegate (uint citId, ref Citizen citizen) {
				loc = citizen.CurrentLocation;
				return true;
			});
			lastLocation = loc;
		}
	}
}
