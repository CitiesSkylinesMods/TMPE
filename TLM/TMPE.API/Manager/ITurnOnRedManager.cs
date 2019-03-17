using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Data;

namespace TrafficManager.Manager {
	public interface ITurnOnRedManager {
		TurnOnRedSegments[] TurnOnRedSegments { get; }

		/// <summary>
		/// Retrieves the array index for the given segment end id.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">start node</param>
		/// <returns>array index for the segment end id</returns>
		int GetIndex(ushort segmentId, bool startNode);
	}
}
