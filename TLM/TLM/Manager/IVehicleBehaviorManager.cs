using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Data;

namespace TrafficManager.Manager {
	public interface IVehicleBehaviorManager {
		// TODO define me!
		// TODO documentation

		/// <summary>
		/// Checks if space reservation at <paramref name="targetPos"/> is allowed. When a custom traffic light is active at the transit node
		/// space reservation is only allowed if the light is not red.
		/// </summary>
		/// <param name="transitNodeId">transition node id</param>
		/// <param name="sourcePos">source path position</param>
		/// <param name="targetPos">target path position</param>
		/// <returns></returns>
		bool IsSpaceReservationAllowed(ushort transitNodeId, PathUnit.Position sourcePos, PathUnit.Position targetPos);

		/// <summary>
		/// Identifies the best lane on the next segment.
		/// </summary>
		/// <param name="vehicleId">queried vehicle</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="vehicleState">vehicle state</param>
		/// <param name="currentLaneId">current lane id</param>
		/// <param name="currentPathPos">current path position</param>
		/// <param name="currentSegInfo">current segment info</param>
		/// <param name="next1PathPos">1st next path position</param>
		/// <param name="next1SegInfo">1st next segment info</param>
		/// <param name="next2PathPos">2nd next path position</param>
		/// <param name="next3PathPos">3rd next path position</param>
		/// <param name="next4PathPos">4th next path position</param>
		/// <returns>target position lane index</returns>
		int FindBestLane(ushort vehicleId, ref Vehicle vehicleData, ref VehicleState vehicleState, uint currentLaneId, PathUnit.Position currentPathPos, NetInfo currentSegInfo, PathUnit.Position next1PathPos, NetInfo next1SegInfo, PathUnit.Position next2PathPos, NetInfo next2SegInfo, PathUnit.Position next3PathPos, NetInfo next3SegInfo, PathUnit.Position next4PathPos);

		/// <summary>
		/// Determines if the given vehicle is allowed to find an alternative lane.
		/// </summary>
		/// <param name="vehicleId">queried vehicle</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="vehicleState">vehicle state</param>
		/// <returns></returns>
		bool MayFindBestLane(ushort vehicleId, ref Vehicle vehicleData, ref VehicleState vehicleState);
	}
}
