using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using UnityEngine;
using static TrafficManager.Custom.PathFinding.CustomPathManager;

namespace TrafficManager.Custom.AI {
	public class CustomPostVanAI : CarAI {
		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
			if (vehicleData.m_transferType == (byte)TransferManager.TransferReason.Mail) {
				return base.StartPathFind(vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, undergroundTarget);
			}

			if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) != 0) {
				return base.StartPathFind(vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, undergroundTarget);
			}

			bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != (Vehicle.Flags)0;
			PathUnit.Position startPosA = default(PathUnit.Position);
			PathUnit.Position startPosB = default(PathUnit.Position);
			float startDistSqrA = default(float);
			float startDistSqrB = default(float);

			// try to find road start position
			bool startPosFound = CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, allowUnderground, false, 32f, out startPosA, out startPosB, out startDistSqrA, out startDistSqrB);

			// try to find other start position (plane, train, ship)
			PathUnit.Position altStartPosA = default(PathUnit.Position);
			PathUnit.Position altStartPosB = default(PathUnit.Position);
			float altStartDistSqrA = default(float);
			float altStartDistSqrB = default(float);
			if (PathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship | VehicleInfo.VehicleType.Plane, allowUnderground, false, 32f, out altStartPosA, out altStartPosB, out altStartDistSqrA, out altStartDistSqrB)) {
				if (!startPosFound || (altStartDistSqrA < startDistSqrA && (Mathf.Abs(startPos.x) > 4800f || Mathf.Abs(startPos.z) > 4800f))) {
					startPosA = altStartPosA;
					startPosB = altStartPosB;
					startDistSqrA = altStartDistSqrA;
					startDistSqrB = altStartDistSqrB;
				}
				startPosFound = true;
			}

			PathUnit.Position endPosA = default(PathUnit.Position);
			PathUnit.Position endPosB = default(PathUnit.Position);
			float endDistSqrA = default(float);
			float endDistSqrB = default(float);

			// try to find road end position
			bool endPosFound = PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, undergroundTarget, false, 32f, out endPosA, out endPosB, out endDistSqrA, out endDistSqrB);

			// try to find other end position (plane, train, ship)
			PathUnit.Position altEndPosA = default(PathUnit.Position);
			PathUnit.Position altEndPosB = default(PathUnit.Position);
			float altEndDistSqrA = default(float);
			float altEndDistSqrB = default(float);
			if (PathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship | VehicleInfo.VehicleType.Plane, undergroundTarget, false, 32f, out altEndPosA, out altEndPosB, out altEndDistSqrA, out altEndDistSqrB)) {
				if (!endPosFound || (altEndDistSqrA < endDistSqrA && (Mathf.Abs(endPos.x) > 4800f || Mathf.Abs(endPos.z) > 4800f))) {
					endPosA = altEndPosA;
					endPosB = altEndPosB;
					endDistSqrA = altEndDistSqrA;
					endDistSqrB = altEndDistSqrB;
				}
				endPosFound = true;
			}

			if (startPosFound && endPosFound) {
				CustomPathManager pathManager = CustomPathManager._instance;
				if (!startBothWays || startDistSqrA < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || endDistSqrA < 10f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;

				PathCreationArgs args;
				args.extPathType = ExtCitizenInstance.ExtPathType.None;
				args.extVehicleType = ExtVehicleType.Service;
				args.vehicleId = vehicleID;
				args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
				args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
				args.startPosA = startPosA;
				args.startPosB = startPosB;
				args.endPosA = endPosA;
				args.endPosB = endPosB;
				args.vehiclePosition = default(PathUnit.Position);
				args.laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle;
				args.vehicleTypes = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship | VehicleInfo.VehicleType.Plane;
				args.maxLength = 20000f;
				args.isHeavyVehicle = this.IsHeavyVehicle();
				args.hasCombustionEngine = this.CombustionEngine();
				args.ignoreBlocked = this.IgnoreBlocked(vehicleID, ref vehicleData);
				args.ignoreFlooded = false;
				args.ignoreCosts = false;
				args.randomParking = false;
				args.stablePath = false;
				args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

				if (pathManager.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args)) {
					if (vehicleData.m_path != 0) {
						pathManager.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}
	}
}
