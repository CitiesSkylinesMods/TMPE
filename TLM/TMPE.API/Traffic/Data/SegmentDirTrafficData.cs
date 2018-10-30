using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Data {
	public struct SegmentDirTrafficData {
		public ushort meanSpeed;

		public override string ToString() {
			return $"[SegmentDirTrafficData\n" +
				"\t" + $"meanSpeed = {meanSpeed}\n" +
				"SegmentDirTrafficData]";
		}
	}
}
