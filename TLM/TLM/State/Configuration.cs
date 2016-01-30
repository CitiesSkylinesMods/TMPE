using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Serialization;

// TODO this class should be moved to TrafficManager.State, but the deserialization fails if we just do that now. Anyway, we should get rid of these crazy lists of arrays. So let's move the class when we decide rework the load/save system.
namespace TrafficManager {
	[Serializable]
	public class Configuration {
		[Serializable]
		public class LaneSpeedLimit {
			public uint laneId;
			public ushort speedLimit;

			public LaneSpeedLimit(uint laneId, ushort speedLimit) {
				this.laneId = laneId;
				this.speedLimit = speedLimit;
			}
		}

		public string NodeTrafficLights = ""; // TODO rework
		public string NodeCrosswalk = ""; // TODO rework
		public string LaneFlags = ""; // TODO rework

		/// <summary>
		/// Stored lane speed limits
		/// </summary>
		public List<LaneSpeedLimit> LaneSpeedLimits = new List<LaneSpeedLimit>();

		public List<int[]> PrioritySegments = new List<int[]>(); // TODO rework
		public List<int[]> NodeDictionary = new List<int[]>(); // TODO rework
		public List<int[]> ManualSegments = new List<int[]>(); // TODO rework

		public List<int[]> TimedNodes = new List<int[]>(); // TODO rework
		public List<ushort[]> TimedNodeGroups = new List<ushort[]>(); // TODO rework
		public List<int[]> TimedNodeSteps = new List<int[]>(); // TODO rework
		public List<int[]> TimedNodeStepSegments = new List<int[]>(); // TODO rework
	}
}
