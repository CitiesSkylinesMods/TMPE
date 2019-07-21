using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.Manager.Impl {
    using API.Manager;

    public class ExtPathManager : AbstractCustomManager, IExtPathManager {
		public static readonly ExtPathManager Instance = new ExtPathManager();

		private ExtPathManager() {
			
		}

		public bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPos) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, otherLaneType, otherVehicleType, allowUnderground, requireConnect, maxDistance, out pathPos);
		}

		public bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPos) {
			PathUnit.Position position2;
			float distanceSqrA;
			float distanceSqrB;
			return FindPathPositionWithSpiralLoop(position, secondaryPosition, service, laneType, vehicleType, otherLaneType, otherVehicleType, VehicleInfo.VehicleType.None, allowUnderground, requireConnect, maxDistance, out pathPos, out position2, out distanceSqrA, out distanceSqrB);
		}

		public bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, otherLaneType, otherVehicleType, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, secondaryPosition, service, laneType, vehicleType, otherLaneType, otherVehicleType, VehicleInfo.VehicleType.None, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public bool FindPathPositionWithSpiralLoop(Vector3 position, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, VehicleInfo.VehicleType stopType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			return FindPathPositionWithSpiralLoop(position, null, service, laneType, vehicleType, otherLaneType, otherVehicleType, stopType, allowUnderground, requireConnect, maxDistance, out pathPosA, out pathPosB, out distanceSqrA, out distanceSqrB);
		}

		public bool FindPathPositionWithSpiralLoop(Vector3 position, Vector3? secondaryPosition, ItemClass.Service service, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType otherLaneType, VehicleInfo.VehicleType otherVehicleType, VehicleInfo.VehicleType stopType, bool allowUnderground, bool requireConnect, float maxDistance, out PathUnit.Position pathPosA, out PathUnit.Position pathPosB, out float distanceSqrA, out float distanceSqrB) {
			int iMin = Mathf.Max((int)((position.z - (float)NetManager.NODEGRID_CELL_SIZE) / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f), 0);
			int iMax = Mathf.Min((int)((position.z + (float)NetManager.NODEGRID_CELL_SIZE) / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f), NetManager.NODEGRID_RESOLUTION - 1);

			int jMin = Mathf.Max((int)((position.x - (float)NetManager.NODEGRID_CELL_SIZE) / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f), 0);
			int jMax = Mathf.Min((int)((position.x + (float)NetManager.NODEGRID_CELL_SIZE) / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f), NetManager.NODEGRID_RESOLUTION - 1);

			int width = iMax - iMin + 1;
			int height = jMax - jMin + 1;

			int centerI = (int)(position.z / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f);
			int centerJ = (int)(position.x / (float)NetManager.NODEGRID_CELL_SIZE + (float)NetManager.NODEGRID_RESOLUTION / 2f);

			NetManager netManager = Singleton<NetManager>.instance;
			/*pathPosA.m_segment = 0;
			pathPosA.m_lane = 0;
			pathPosA.m_offset = 0;*/
			distanceSqrA = 1E+10f;
			/*pathPosB.m_segment = 0;
			pathPosB.m_lane = 0;
			pathPosB.m_offset = 0;*/
			distanceSqrB = 1E+10f;
			float minDist = float.MaxValue;

			PathUnit.Position myPathPosA = default(PathUnit.Position);
			float myDistanceSqrA = float.MaxValue;
			PathUnit.Position myPathPosB = default(PathUnit.Position);
			float myDistanceSqrB = float.MaxValue;

			int lastSpiralDist = 0;
			bool found = false;

			LoopUtil.SpiralLoop(centerI, centerJ, width, height, delegate (int i, int j) {
				if (i < 0 || i >= NetManager.NODEGRID_RESOLUTION || j < 0 || j >= NetManager.NODEGRID_RESOLUTION)
					return true;

				int spiralDist = Math.Max(Math.Abs(i - centerI), Math.Abs(j - centerJ)); // maximum norm

				if (found && spiralDist > lastSpiralDist) {
					// last iteration
					return false;
				}

				ushort segmentId = netManager.m_segmentGrid[i * NetManager.NODEGRID_RESOLUTION + j];
				int iterations = 0;
				while (segmentId != 0) {
					NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
					if (segmentInfo != null &&
						segmentInfo.m_class.m_service == service &&
						(netManager.m_segments.m_buffer[segmentId].m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) == NetSegment.Flags.None &&
						(allowUnderground || !segmentInfo.m_netAI.IsUnderground())) {

						bool otherPassed = true;
						if (otherLaneType != NetInfo.LaneType.None || otherVehicleType != VehicleInfo.VehicleType.None) {
							// check if any lane is present that matches the given conditions
							otherPassed = false;
							Constants.ServiceFactory.NetService.IterateSegmentLanes(segmentId, delegate (uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segtId, ref NetSegment segment, byte laneIndex) {
								if (
									(otherLaneType == NetInfo.LaneType.None || (laneInfo.m_laneType & otherLaneType) != NetInfo.LaneType.None) &&
									(otherVehicleType == VehicleInfo.VehicleType.None || (laneInfo.m_vehicleType & otherVehicleType) != VehicleInfo.VehicleType.None)) {
									otherPassed = true;
									return false;
								} else {
									return true;
								}
							});
						}

						if (otherPassed) {
							ushort startNodeId = netManager.m_segments.m_buffer[segmentId].m_startNode;
							ushort endNodeId = netManager.m_segments.m_buffer[segmentId].m_endNode;
							Vector3 startNodePos = netManager.m_nodes.m_buffer[startNodeId].m_position;
							Vector3 endNodePos = netManager.m_nodes.m_buffer[endNodeId].m_position;

							Vector3 posA; int laneIndexA; float laneOffsetA;
							Vector3 posB; int laneIndexB; float laneOffsetB;

							if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(position, laneType, vehicleType, stopType, requireConnect, out posA, out laneIndexA, out laneOffsetA, out posB, out laneIndexB, out laneOffsetB)) {
								float dist = Vector3.SqrMagnitude(position - posA);
								if (secondaryPosition != null)
									dist += Vector3.SqrMagnitude((Vector3)secondaryPosition - posA);

								if (dist < minDist) {
									found = true;

									minDist = dist;
									myPathPosA.m_segment = segmentId;
									myPathPosA.m_lane = (byte)laneIndexA;
									myPathPosA.m_offset = (byte)Mathf.Clamp(Mathf.RoundToInt(laneOffsetA * 255f), 0, 255);
									myDistanceSqrA = dist;

									dist = Vector3.SqrMagnitude(position - posB);
									if (secondaryPosition != null)
										dist += Vector3.SqrMagnitude((Vector3)secondaryPosition - posB);

									if (laneIndexB < 0) {
										myPathPosB.m_segment = 0;
										myPathPosB.m_lane = 0;
										myPathPosB.m_offset = 0;
										myDistanceSqrB = float.MaxValue;
									} else {
										myPathPosB.m_segment = segmentId;
										myPathPosB.m_lane = (byte)laneIndexB;
										myPathPosB.m_offset = (byte)Mathf.Clamp(Mathf.RoundToInt(laneOffsetB * 255f), 0, 255);
										myDistanceSqrB = dist;
									}
								}
							}
						}
					}

					segmentId = netManager.m_segments.m_buffer[segmentId].m_nextGridSegment;
					if (++iterations >= NetManager.MAX_SEGMENT_COUNT) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}

				lastSpiralDist = spiralDist;
				return true;
			});

			pathPosA = myPathPosA;
			distanceSqrA = myDistanceSqrA;
			pathPosB = myPathPosB;
			distanceSqrB = myDistanceSqrB;

			return pathPosA.m_segment != 0;
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
		}
		
	}
}
