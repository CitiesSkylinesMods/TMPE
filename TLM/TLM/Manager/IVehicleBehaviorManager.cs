using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Data;
using UnityEngine;

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
		/// Determines if the given vehicle is driven by a reckless driver.
		/// Note that the result is cached in VehicleState for individual vehicles.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		/// <returns></returns>
		bool IsRecklessDriver(ushort vehicleId, ref Vehicle vehicleData);

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
		/// Identifies the best lane to take on the next segment (for emergency vehicles on duty).
		/// Note that this method does not require Advanced AI and/or DLS to be active.
		/// </summary>
		/// <param name="vehicleId">queried vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="vehicleState">vehicle state</param>
		/// <param name="currentLaneId">current lane id</param>
		/// <param name="currentPathPos">current path position</param>
		/// <param name="currentSegInfo">current segment info</param>
		/// <param name="nextPathPos">next path position</param>
		/// <param name="nextSegInfo">next segment info</param>
		/// <returns>target position lane index</returns>
		int FindBestEmergencyLane(ushort vehicleId, ref Vehicle vehicleData, ref VehicleState vehicleState, uint currentLaneId, PathUnit.Position currentPathPos, NetInfo currentSegInfo, PathUnit.Position nextPathPos, NetInfo nextSegInfo);

		/// <summary>
		/// Determines if the given vehicle is allowed to find an alternative lane.
		/// </summary>
		/// <param name="vehicleId">queried vehicle</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="vehicleState">vehicle state</param>
		/// <returns></returns>
		bool MayFindBestLane(ushort vehicleId, ref Vehicle vehicleData, ref VehicleState vehicleState);

		/// <summary>
		/// Calculates the current randomization value for a vehicle.
		/// The value changes over time.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <returns>a number between 0 and 99</returns>
		uint GetTimedVehicleRand(ushort vehicleId);

		/// <summary>
		/// Calculates the randomization value for a vehicle.
		/// The value is static throughout the vehicle's lifetime.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <returns>a number between 0 and 99</returns>
		uint GetStaticVehicleRand(ushort vehicleId);

		/// <summary>
		/// Applies realistic speed multipliers to the given velocity.
		/// </summary>
		/// <param name="speed">vehicle target velocity</param>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleState">vehicle state</param>
		/// <param name="vehicleInfo">vehicle info</param>
		/// <returns>modified target velocity</returns>
		float ApplyRealisticSpeeds(float speed, ushort vehicleId, ref VehicleState vehicleState, VehicleInfo vehicleInfo);

		/// <summary>
		/// Calculates the target velocity for the given vehicle.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="state">vehicle state</param>
		/// <param name="vehicleInfo">vehicle info</param>
		/// <param name="position">current path position</param>
		/// <param name="segment">segment data</param>
		/// <param name="pos">current world position</param>
		/// <param name="maxSpeed">vehicle target velocity</param>
		/// <returns>modified target velocity</returns>
		float CalcMaxSpeed(ushort vehicleId, ref VehicleState state, VehicleInfo vehicleInfo, PathUnit.Position position, ref NetSegment segment, Vector3 pos, float maxSpeed);
	}
}
