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

		internal void AddParkingSpaceDemand() {
			ParkingSpaceDemand = (byte)Math.Min(100, (int)ParkingSpaceDemand + 10);
		}

		internal void RemoveParkingSpaceDemand() {
			ParkingSpaceDemand = (byte)Math.Max(0, (int)ParkingSpaceDemand - 10);
		}
	}
}
