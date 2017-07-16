using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using UnityEngine;

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

		internal ExtBuilding(ushort buildingId) {
			this.buildingId = buildingId;
			parkingSpaceDemand = 0;
			incomingPublicTransportDemand = 0;
			outgoingPublicTransportDemand = 0;
		}

		public bool IsValid() {
			return Constants.ServiceFactory.BuildingService.IsBuildingValid(buildingId);
		}

		internal void Reset() {
			parkingSpaceDemand = 0;
            incomingPublicTransportDemand = 0;
            outgoingPublicTransportDemand = 0;
		}

		internal void AddParkingSpaceDemand(uint delta) {
			parkingSpaceDemand = (byte)Math.Min(100, (int)parkingSpaceDemand + delta);
			RequestColorUpdate();
		}

		internal void RemoveParkingSpaceDemand(uint delta) {
			parkingSpaceDemand = (byte)Math.Max(0, (int)parkingSpaceDemand - delta);
			RequestColorUpdate();
		}

		internal void ModifyParkingSpaceDemand(Vector3 parkPos, int minDelta=-10, int maxDelta=10) {
			Vector3 buildingPos = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingId].m_position;
			float distance = Mathf.Clamp((parkPos - buildingPos).magnitude, 0f, GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding);

			float delta = (float)(maxDelta - minDelta) * (distance / GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding) + (float)minDelta;
			parkingSpaceDemand = (byte)Mathf.Clamp((int)parkingSpaceDemand + (int)Mathf.Round(delta), 0, 100);
			RequestColorUpdate();
		}

		internal void AddPublicTransportDemand(uint delta, bool outgoing) {
            byte oldDemand = outgoing ? outgoingPublicTransportDemand : incomingPublicTransportDemand;
			byte newDemand = (byte)Math.Min(100, (int)oldDemand + delta);
            if (outgoing)
                outgoingPublicTransportDemand = newDemand;
            else
                incomingPublicTransportDemand = newDemand;

            RequestColorUpdate();
		}

		internal void RemovePublicTransportDemand(uint delta, bool outgoing) {
            byte oldDemand = outgoing ? outgoingPublicTransportDemand : incomingPublicTransportDemand;
            byte newDemand = (byte)Math.Max(0, (int)oldDemand - delta);
            if (outgoing)
                outgoingPublicTransportDemand = newDemand;
            else
                incomingPublicTransportDemand = newDemand;

			RequestColorUpdate();
		}

		private void RequestColorUpdate() {
			Singleton<BuildingManager>.instance.UpdateBuildingColors(buildingId);
		}
	}
}
