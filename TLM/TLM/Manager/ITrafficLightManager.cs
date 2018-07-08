using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public enum UnableReason {
		None,
		NoJunction,
		HasTimedLight,
		IsLevelCrossing,
		InsufficientSegments
	}

	public interface ITrafficLightManager {
		// TODO documentation
		bool AddTrafficLight(ushort nodeId, ref NetNode node);
		bool AddTrafficLight(ushort nodeId, ref NetNode node, out UnableReason reason);
		bool HasTrafficLight(ushort nodeId, ref NetNode node);
		bool IsTrafficLightEnablable(ushort nodeId, ref NetNode node, out UnableReason reason);
		bool IsTrafficLightToggleable(ushort nodeId, bool flag, ref NetNode node, out UnableReason reason);
		bool RemoveTrafficLight(ushort nodeId, ref NetNode node);
		bool RemoveTrafficLight(ushort nodeId, ref NetNode node, out UnableReason reason);
		bool SetTrafficLight(ushort nodeId, bool flag, ref NetNode node);
		bool SetTrafficLight(ushort nodeId, bool flag, ref NetNode node, out UnableReason reason);
		bool ToggleTrafficLight(ushort nodeId, ref NetNode node);
		bool ToggleTrafficLight(ushort nodeId, ref NetNode node, out UnableReason reason);
	}
}
