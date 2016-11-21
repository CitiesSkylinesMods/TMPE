using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Geometry;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomTransportLineAI : TransportLineAI { // TODO inherit from NetAI (in order to keep the correct references to `base`)
		public static bool CustomStartPathFind(ushort segmentID, ref NetSegment data, ItemClass.Service netService, VehicleInfo.VehicleType vehicleType, bool skipQueue) {
			if (data.m_path != 0u) {
				Singleton<PathManager>.instance.ReleasePath(data.m_path);
				data.m_path = 0u;
			}
			NetManager instance = Singleton<NetManager>.instance;
			if ((instance.m_nodes.m_buffer[(int)data.m_startNode].m_flags & NetNode.Flags.Ambiguous) != NetNode.Flags.None) {
				for (int i = 0; i < 8; i++) {
					ushort segment = instance.m_nodes.m_buffer[(int)data.m_startNode].GetSegment(i);
					if (segment != 0 && segment != segmentID && instance.m_segments.m_buffer[(int)segment].m_path != 0u) {
						return true;
					}
				}
			}
			if ((instance.m_nodes.m_buffer[(int)data.m_endNode].m_flags & NetNode.Flags.Ambiguous) != NetNode.Flags.None) {
				for (int j = 0; j < 8; j++) {
					ushort segment2 = instance.m_nodes.m_buffer[(int)data.m_endNode].GetSegment(j);
					if (segment2 != 0 && segment2 != segmentID && instance.m_segments.m_buffer[(int)segment2].m_path != 0u) {
						return true;
					}
				}
			}
			Vector3 position = instance.m_nodes.m_buffer[(int)data.m_startNode].m_position;
			Vector3 position2 = instance.m_nodes.m_buffer[(int)data.m_endNode].m_position;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float startSqrDistA;
			float startSqrDistB;
			if (!CustomPathManager.FindPathPosition(position, netService, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, vehicleType, true, false, 32f, out startPosA, out startPosB, out startSqrDistA, out startSqrDistB)) {
				CustomTransportLineAI.CheckSegmentProblems(segmentID, ref data);
				return true;
			}
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float endSqrDistA;
			float endSqrDistB;
			if (!CustomPathManager.FindPathPosition(position2, netService, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, vehicleType, true, false, 32f, out endPosA, out endPosB, out endSqrDistA, out endSqrDistB)) {
				CustomTransportLineAI.CheckSegmentProblems(segmentID, ref data);
				return true;
			}
			if ((instance.m_nodes.m_buffer[(int)data.m_startNode].m_flags & NetNode.Flags.Fixed) != NetNode.Flags.None) {
				startPosB = default(PathUnit.Position);
			}
			if ((instance.m_nodes.m_buffer[(int)data.m_endNode].m_flags & NetNode.Flags.Fixed) != NetNode.Flags.None) {
				endPosB = default(PathUnit.Position);
			}
			startPosA.m_offset = 128;
			startPosB.m_offset = 128;
			endPosA.m_offset = 128;
			endPosB.m_offset = 128;
			bool stopLane = CustomTransportLineAI.GetStopLane(ref startPosA, vehicleType);
			bool stopLane2 = CustomTransportLineAI.GetStopLane(ref startPosB, vehicleType);
			bool stopLane3 = CustomTransportLineAI.GetStopLane(ref endPosA, vehicleType);
			bool stopLane4 = CustomTransportLineAI.GetStopLane(ref endPosB, vehicleType);
			if ((!stopLane && !stopLane2) || (!stopLane3 && !stopLane4)) {
				CustomTransportLineAI.CheckSegmentProblems(segmentID, ref data);
				return true;
			}
			uint path;
			ExtVehicleType extVehicleType = ExtVehicleType.Bus;
			if ((vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro)) != VehicleInfo.VehicleType.None)
				extVehicleType = ExtVehicleType.PassengerTrain;
			if ((vehicleType & VehicleInfo.VehicleType.Tram) != VehicleInfo.VehicleType.None)
				extVehicleType = ExtVehicleType.Tram;
			if ((vehicleType & VehicleInfo.VehicleType.Ship) != VehicleInfo.VehicleType.None)
				extVehicleType = ExtVehicleType.PassengerShip;
			if ((vehicleType & VehicleInfo.VehicleType.Plane) != VehicleInfo.VehicleType.None)
				extVehicleType = ExtVehicleType.PassengerPlane;
			//Log._Debug($"Transport line. extVehicleType={extVehicleType}");

			if (CustomPathManager._instance.CreatePath(extVehicleType, 0, ExtCitizenInstance.ExtPathType.None, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, vehicleType, 20000f, false, true, true, skipQueue)) {
				if (startPosA.m_segment != 0 && startPosB.m_segment != 0) {
					NetNode[] expr_2F5_cp_0 = instance.m_nodes.m_buffer;
					ushort expr_2F5_cp_1 = data.m_startNode;
					expr_2F5_cp_0[(int)expr_2F5_cp_1].m_flags = (expr_2F5_cp_0[(int)expr_2F5_cp_1].m_flags | NetNode.Flags.Ambiguous);
				} else {
					NetNode[] expr_321_cp_0 = instance.m_nodes.m_buffer;
					ushort expr_321_cp_1 = data.m_startNode;
					expr_321_cp_0[(int)expr_321_cp_1].m_flags = (expr_321_cp_0[(int)expr_321_cp_1].m_flags & ~NetNode.Flags.Ambiguous);
				}
				if (endPosA.m_segment != 0 && endPosB.m_segment != 0) {
					NetNode[] expr_360_cp_0 = instance.m_nodes.m_buffer;
					ushort expr_360_cp_1 = data.m_endNode;
					expr_360_cp_0[(int)expr_360_cp_1].m_flags = (expr_360_cp_0[(int)expr_360_cp_1].m_flags | NetNode.Flags.Ambiguous);
				} else {
					NetNode[] expr_38C_cp_0 = instance.m_nodes.m_buffer;
					ushort expr_38C_cp_1 = data.m_endNode;
					expr_38C_cp_0[(int)expr_38C_cp_1].m_flags = (expr_38C_cp_0[(int)expr_38C_cp_1].m_flags & ~NetNode.Flags.Ambiguous);
				}
				data.m_path = path;
				data.m_flags |= NetSegment.Flags.WaitingPath;
				return false;
			}
			CustomTransportLineAI.CheckSegmentProblems(segmentID, ref data);
			return true;
		}

		public static bool GetStopLane(ref PathUnit.Position pos, VehicleInfo.VehicleType vehicleType) {
			if (pos.m_segment != 0) {
				NetManager instance = Singleton<NetManager>.instance;
				int num;
				uint num2;
				if (instance.m_segments.m_buffer[(int)pos.m_segment].GetClosestLane((int)pos.m_lane, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, vehicleType, out num, out num2)) {
					pos.m_lane = (byte)num;
					return true;
				}
			}
			pos = default(PathUnit.Position);
			return false;
		}

		internal static void CheckSegmentProblems(ushort segmentID, ref NetSegment data) {
			NetManager instance = Singleton<NetManager>.instance;
			Notification.Problem problems = instance.m_nodes.m_buffer[(int)data.m_startNode].m_problems;
			CheckNodeProblems(data.m_startNode, ref instance.m_nodes.m_buffer[(int)data.m_startNode]);
			Notification.Problem problems2 = instance.m_nodes.m_buffer[(int)data.m_startNode].m_problems;
			if (problems != problems2) {
				instance.UpdateNodeNotifications(data.m_startNode, problems, problems2);
			}
			Notification.Problem problems3 = instance.m_nodes.m_buffer[(int)data.m_endNode].m_problems;
			CheckNodeProblems(data.m_endNode, ref instance.m_nodes.m_buffer[(int)data.m_endNode]);
			Notification.Problem problems4 = instance.m_nodes.m_buffer[(int)data.m_endNode].m_problems;
			if (problems3 != problems4) {
				instance.UpdateNodeNotifications(data.m_endNode, problems3, problems4);
			}
		}

		internal static void CheckNodeProblems(ushort nodeID, ref NetNode data) {
			if (data.m_transportLine != 0) {
				bool flag = false;
				if ((data.m_flags & NetNode.Flags.Temporary) == NetNode.Flags.None) {
					NetManager instance = Singleton<NetManager>.instance;
					int num = 0;
					for (int i = 0; i < 8; i++) {
						ushort segment = data.GetSegment(i);
						if (segment != 0) {
							num++;
							if (instance.m_segments.m_buffer[(int)segment].m_path == 0u || (instance.m_segments.m_buffer[(int)segment].m_flags & NetSegment.Flags.PathFailed) != NetSegment.Flags.None) {
								flag = true;
							}
						}
					}
					if (num <= 1) {
						flag = true;
					}
				}
				if (flag) {
					data.m_problems = Notification.AddProblems(data.m_problems, Notification.Problem.LineNotConnected);
				} else {
					data.m_problems = Notification.RemoveProblems(data.m_problems, Notification.Problem.LineNotConnected);
				}
			}
		}
	}
}
