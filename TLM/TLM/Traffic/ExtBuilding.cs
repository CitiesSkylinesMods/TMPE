using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic {
	public class ExtBuilding {
		private ushort BuildingId;

		public byte ParkingSpaceDemand { get; private set; }

		public ExtBuilding(ushort buildingId) {
			this.BuildingId = buildingId;
			Reset();
		}

		internal void Reset() {
			ParkingSpaceDemand = 0;
		}

		internal void AddParkingSpaceDemand(int delta=5) {
			ParkingSpaceDemand = (byte)Math.Min(100, (int)ParkingSpaceDemand + delta);
			RequestColorUpdate();
		}

		internal void RemoveParkingSpaceDemand(int delta=5) {
			ParkingSpaceDemand = (byte)Math.Max(0, (int)ParkingSpaceDemand - delta);
			RequestColorUpdate();
		}

		private void RequestColorUpdate() {
			Singleton<BuildingManager>.instance.UpdateBuildingColors(BuildingId);
		}
	}
}
