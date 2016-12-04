using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Traffic {
	public class ExtBuilding {
		private ushort BuildingId;

		public byte ParkingSpaceDemand { get; private set; }

		public byte IncomingPublicTransportDemand { get; private set; }

        public byte OutgoingPublicTransportDemand { get; private set; }

        public ExtBuilding(ushort buildingId) {
			this.BuildingId = buildingId;
			Reset();
		}

		internal void Reset() {
			ParkingSpaceDemand = 0;
            IncomingPublicTransportDemand = 0;
            OutgoingPublicTransportDemand = 0;
		}

		internal void AddParkingSpaceDemand(uint delta) {
			ParkingSpaceDemand = (byte)Math.Min(100, (int)ParkingSpaceDemand + delta);
			RequestColorUpdate();
		}

		internal void RemoveParkingSpaceDemand(uint delta) {
			ParkingSpaceDemand = (byte)Math.Max(0, (int)ParkingSpaceDemand - delta);
			RequestColorUpdate();
		}

		internal void ModifyParkingSpaceDemand(Vector3 parkPos, int minDelta=-10, int maxDelta=10) {
			Vector3 buildingPos = Singleton<BuildingManager>.instance.m_buildings.m_buffer[BuildingId].m_position;
			float distance = Mathf.Clamp((parkPos - buildingPos).magnitude, 0f, GlobalConfig.Instance.VicinityParkingSpaceSearchRadius);

			float delta = (float)(maxDelta - minDelta) * (distance / GlobalConfig.Instance.VicinityParkingSpaceSearchRadius) + (float)minDelta;
			ParkingSpaceDemand = (byte)Mathf.Clamp((int)ParkingSpaceDemand + (int)Mathf.Round(delta), 0, 100);
			RequestColorUpdate();
		}

		internal void AddPublicTransportDemand(uint delta, bool outgoing) {
            byte oldDemand = outgoing ? OutgoingPublicTransportDemand : IncomingPublicTransportDemand;
			byte newDemand = (byte)Math.Min(100, (int)oldDemand + delta);
            if (outgoing)
                OutgoingPublicTransportDemand = newDemand;
            else
                IncomingPublicTransportDemand = newDemand;

            RequestColorUpdate();
		}

		internal void RemovePublicTransportDemand(uint delta, bool outgoing) {
            byte oldDemand = outgoing ? OutgoingPublicTransportDemand : IncomingPublicTransportDemand;
            byte newDemand = (byte)Math.Max(0, (int)oldDemand - delta);
            if (outgoing)
                OutgoingPublicTransportDemand = newDemand;
            else
                IncomingPublicTransportDemand = newDemand;

			RequestColorUpdate();
		}

		private void RequestColorUpdate() {
			Singleton<BuildingManager>.instance.UpdateBuildingColors(BuildingId);
		}
	}
}
