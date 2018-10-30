using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface IParkingRestrictionsManager {
		// TODO define me!
		bool MayHaveParkingRestriction(ushort segmentId);
		bool IsParkingAllowed(ushort segmentId, NetInfo.Direction finalDir);
		bool ToggleParkingAllowed(ushort segmentId, NetInfo.Direction finalDir);
		bool SetParkingAllowed(ushort segmentId, NetInfo.Direction finalDir, bool flag);
	}
}
