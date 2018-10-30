using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.Math;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using UnityEngine;

namespace TrafficManager.Manager {
	public interface IEmergencyBehaviorManager {
		// TODO documentation
		EmergencyBehavior GetEmergencyBehavior(bool force, PathUnit.Position position, uint laneId, ref NetSegment segment, ref ExtSegment extSegment);
		//bool ApplyRescueTranslation(ushort vehicleId, ref Vehicle vehicle, ref ExtVehicle extVehicle, PathUnit.Position position, byte offset, ref NetSegment segment, ref NetLane lane, ref Vector3 pos, Vector3 dir, out EmergencyBehavior behavior);
		int GetRescueLane(EmergencyBehavior behavior, PathUnit.Position position);
		int GetEvasionLane(EmergencyBehavior behavior, PathUnit.Position position);
		bool IsEmergencyActive(ushort segmentId);
		bool CheckOverlap(ushort ignoreVehicleId, ref Bezier3 bezier, float offset, float length);
		void RegisterEmergencyVehicle(ushort segmentId, bool register);
		//bool CheckVehicleStopped(ref ExtVehicle extVehicle, ushort segmentId);
		void AdaptSegmentPosition(ushort vehicleId, ref Vehicle vehicle, ref ExtVehicle extVehicle, PathUnit.Position position, uint laneId, ref byte offset, ref Vector3 pos, Vector3 dir, ref float maxSpeed/*, out EmergencyBehavior behavior*/);
		void UnstopVehicle(ref ExtVehicle extVehicle);
		//void AddToGrid(ref ExtVehicle extVehicle, int gridX, int gridZ);
		//void RemoveFromGrid(ref ExtVehicle extVehicle);
	}
}
