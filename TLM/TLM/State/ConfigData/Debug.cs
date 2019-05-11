﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.State.ConfigData {
#if DEBUG
	public class Debug {
		public bool[] Switches = {
				false, // 0: path-finding debug log
				false, // 1: routing basic debug log
				false, // 2: parking ai debug log (basic)
				false, // 3: do not actually repair stuck vehicles/cims, just report
				false, // 4: parking ai debug log (extended)
				false, // 5: geometry debug log
				false, // 6: debug parking AI distance issue
				false, // 7: debug TTL
				false, // 8: debug routing
				false, // 9: debug vehicle to segment end linking
				false, // 10: prevent routing recalculation on global configuration reload
				false, // 11: debug junction restrictions
				false, // 12: - unused -
				false, // 13: priority rules debug
				false, // 14: disable GUI overlay of citizens having a valid path
				false, // 15: disable checking of other vehicles for trams
				false, // 16: debug TramBaseAI.SimulationStep (2)
				false, // 17: debug alternative lane selection
				false, // 18: transport line path-find debugging
				false, // 19: enable obligation to drive on the right hand side of the road
				false, // 20: debug realistic public transport
				false, // 21: debug "CalculateSegmentPosition"
				false, // 22: parking ai debug log (vehicles)
				false, // 23: debug lane connections
                false, // 24: debug resource loading
				false // 25: debug turn-on-red
			};

		public int NodeId = 0;
		public int SegmentId = 0;
		public int StartSegmentId = 0;
		public int EndSegmentId = 0;
		public int VehicleId = 0;
		public int CitizenInstanceId = 0;
		public uint CitizenId = 0;
		public uint SourceBuildingId = 0;
		public uint TargetBuildingId = 0;
		public ExtVehicleType ExtVehicleType = ExtVehicleType.None;
		public ExtPathMode ExtPathMode = ExtPathMode.None;
	}
#endif
}
