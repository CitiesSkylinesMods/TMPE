using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager
{
    class CustomCarAI : CarAI
    {
        protected void SimulationStep(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos)
        {
            if ((data.m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None)
            {
                PathManager instance = Singleton<PathManager>.instance;
                byte pathFindFlags = instance.m_pathUnits.m_buffer[(int) ((UIntPtr) data.m_path)].m_pathFindFlags;
                if ((pathFindFlags & 4) != 0)
                {
                    data.m_pathPositionIndex = 255;
                    data.m_flags &= ~Vehicle.Flags.WaitingPath;
                    data.m_flags &= ~Vehicle.Flags.Arriving;
                    this.PathfindSuccess(vehicleID, ref data);
                    this.TrySpawn(vehicleID, ref data);
                }
                else if ((pathFindFlags & 8) != 0)
                {
                    data.m_flags &= ~Vehicle.Flags.WaitingPath;
                    Singleton<PathManager>.instance.ReleasePath(data.m_path);
                    data.m_path = 0u;
                    this.PathfindFailure(vehicleID, ref data);
                    return;
                }
            }
            else if ((data.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None)
            {
                this.TrySpawn(vehicleID, ref data);
            }
            Vector3 lastFramePosition = data.GetLastFramePosition();
            int lodPhysics;
            if (Vector3.SqrMagnitude(physicsLodRefPos - lastFramePosition) >= 1210000f)
            {
                lodPhysics = 2;
            }
            else if (
                Vector3.SqrMagnitude(Singleton<SimulationManager>.instance.m_simulationView.m_position -
                                     lastFramePosition) >= 250000f)
            {
                lodPhysics = 1;
            }
            else
            {
                lodPhysics = 0;
            }
            this.SimulationStep(vehicleID, ref data, vehicleID, ref data, lodPhysics);
            if (data.m_leadingVehicle == 0 && data.m_trailingVehicle != 0)
            {
                VehicleManager instance2 = Singleton<VehicleManager>.instance;
                ushort num = data.m_trailingVehicle;
                int num2 = 0;
                while (num != 0)
                {
                    ushort trailingVehicle = instance2.m_vehicles.m_buffer[(int) num].m_trailingVehicle;
                    VehicleInfo info = instance2.m_vehicles.m_buffer[(int) num].Info;
                    info.m_vehicleAI.SimulationStep(num, ref instance2.m_vehicles.m_buffer[(int) num], vehicleID,
                        ref data, lodPhysics);
                    num = trailingVehicle;
                    if (++num2 > 16384)
                    {
                        CODebugBase<LogChannel>.Error(LogChannel.Core,
                            "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }
            int num3 = (this.m_info.m_class.m_service > ItemClass.Service.Office) ? 150 : 100;
            if ((data.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) ==
                Vehicle.Flags.None && data.m_cargoParent == 0)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
            }
            else if ((int)data.m_blockCounter >= num3 && LoadingExtension.Instance.despawnEnabled)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
            }
        }

        public void CalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition,
            PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID,
            byte prevOffset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
        {
            NetManager instance = Singleton<NetManager>.instance;
            instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
            Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
            Vector3 position2 = lastFrameData.m_position;
            Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].CalculatePosition((float)prevOffset * 0.003921569f);
            float num = 0.5f * lastFrameData.m_velocity.sqrMagnitude / this.m_info.m_braking + this.m_info.m_generatedInfo.m_size.z * 0.5f;

            if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car)
            {
                if (!TrafficPriority.vehicleList.ContainsKey(vehicleID))
                {
                    TrafficPriority.vehicleList.Add(vehicleID, new PriorityCar());
                }
            }

            if (Vector3.Distance(position2, b) >= num - 1f)
            {
                Segment3 segment;
                segment.a = pos;
                ushort num2;
                ushort num3;
                if (offset < position.m_offset)
                {
                    segment.b = pos + dir.normalized*this.m_info.m_generatedInfo.m_size.z;
                    num2 = instance.m_segments.m_buffer[(int) position.m_segment].m_startNode;
                    num3 = instance.m_segments.m_buffer[(int) position.m_segment].m_endNode;
                }
                else
                {
                    segment.b = pos - dir.normalized*this.m_info.m_generatedInfo.m_size.z;
                    num2 = instance.m_segments.m_buffer[(int) position.m_segment].m_endNode;
                    num3 = instance.m_segments.m_buffer[(int) position.m_segment].m_startNode;
                }
                ushort num4;
                if (prevOffset == 0)
                {
                    num4 = instance.m_segments.m_buffer[(int) prevPos.m_segment].m_startNode;
                }
                else
                {
                    num4 = instance.m_segments.m_buffer[(int) prevPos.m_segment].m_endNode;
                }

                if (num2 == num4)
                {
                    uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                    uint num5 = (uint)(((int)num4 << 8) / 32768);
                    uint num6 = currentFrameIndex - num5 & 255u;

                    NetNode.Flags flags = instance.m_nodes.m_buffer[(int) num2].m_flags;
                    NetLane.Flags flags2 =
                        (NetLane.Flags) instance.m_lanes.m_buffer[(int) ((UIntPtr) prevLaneID)].m_flags;
                    bool flag = (flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
                    bool flag2 = (flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
                    bool flag3 = (flags2 & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
                    if ((flags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) ==
                        NetNode.Flags.Junction && instance.m_nodes.m_buffer[(int) num2].CountSegments() != 2)
                    {
                        float len = vehicleData.CalculateTotalLength(vehicleID) + 2f;
                        if (!instance.m_lanes.m_buffer[(int) ((UIntPtr) laneID)].CheckSpace(len))
                        {
                            bool flag4 = false;
                            if (nextPosition.m_segment != 0 &&
                                instance.m_lanes.m_buffer[(int) ((UIntPtr) laneID)].m_length < 30f)
                            {
                                NetNode.Flags flags3 = instance.m_nodes.m_buffer[(int) num3].m_flags;
                                if ((flags3 &
                                     (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) !=
                                    NetNode.Flags.Junction || instance.m_nodes.m_buffer[(int) num3].CountSegments() == 2)
                                {
                                    uint laneID2 = PathManager.GetLaneID(nextPosition);
                                    if (laneID2 != 0u)
                                    {
                                        flag4 = instance.m_lanes.m_buffer[(int) ((UIntPtr) laneID2)].CheckSpace(len);
                                    }
                                }
                            }
                            if (!flag4)
                            {
                                maxSpeed = 0f;
                                return;
                            }
                        }
                    }

                    if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car &&
                        TrafficPriority.vehicleList.ContainsKey(vehicleID) &&
                        TrafficPriority.isPrioritySegment(num2, prevPos.m_segment))
                    {
                        uint currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                        uint frame = currentFrameIndex2 >> 4;

                        var prioritySegment = TrafficPriority.getPrioritySegment(num2, prevPos.m_segment);
                        if (TrafficPriority.vehicleList[vehicleID].toNode != num2 ||
                            TrafficPriority.vehicleList[vehicleID].fromSegment != prevPos.m_segment)
                        {
                            if (TrafficPriority.vehicleList[vehicleID].toNode != 0 &&
                                TrafficPriority.vehicleList[vehicleID].fromSegment != 0)
                            {
                                var oldNode = TrafficPriority.vehicleList[vehicleID].toNode;
                                var oldSegment = TrafficPriority.vehicleList[vehicleID].fromSegment;

                                if (TrafficPriority.isPrioritySegment(oldNode, oldSegment))
                                {
                                    var oldPrioritySegment = TrafficPriority.getPrioritySegment(oldNode, oldSegment);
                                    TrafficPriority.vehicleList[vehicleID].waitTime = 0;
                                    TrafficPriority.vehicleList[vehicleID].stopped = false;
                                    oldPrioritySegment.RemoveCar(vehicleID);
                                }
                            }

                            // prevPos - current segment
                            // position - next segment
                            TrafficPriority.vehicleList[vehicleID].toNode = num2;
                            TrafficPriority.vehicleList[vehicleID].fromSegment = prevPos.m_segment;
                            TrafficPriority.vehicleList[vehicleID].toSegment = position.m_segment;
                            TrafficPriority.vehicleList[vehicleID].toLaneID = PathManager.GetLaneID(position);
                            TrafficPriority.vehicleList[vehicleID].fromLaneID = PathManager.GetLaneID(prevPos);
                            TrafficPriority.vehicleList[vehicleID].fromLaneFlags =
                                instance.m_lanes.m_buffer[PathManager.GetLaneID(prevPos)].m_flags;
                            TrafficPriority.vehicleList[vehicleID].yieldSpeedReduce = UnityEngine.Random.Range(13f,
                                18f);

                            prioritySegment.AddCar(vehicleID);
                        }

                        TrafficPriority.vehicleList[vehicleID].lastFrame = frame;
                        TrafficPriority.vehicleList[vehicleID].lastSpeed =
                            vehicleData.GetLastFrameData().m_velocity.sqrMagnitude;
                    }

                    if (flag && (!flag3 || flag2))
                    {
                        var nodeSimulation = CustomRoadAI.GetNodeSimulation(num4);

                        NetInfo info = instance.m_nodes.m_buffer[(int) num2].Info;
                        RoadBaseAI.TrafficLightState vehicleLightState;
                        RoadBaseAI.TrafficLightState pedestrianLightState;
                        bool flag5;
                        bool pedestrians;

                        if (nodeSimulation == null || (nodeSimulation.FlagTimedTrafficLights && !nodeSimulation.TimedTrafficLightsActive))
                        {
                            RoadBaseAI.GetTrafficLightState(num4,
                                ref instance.m_segments.m_buffer[(int) prevPos.m_segment],
                                currentFrameIndex - num5, out vehicleLightState, out pedestrianLightState, out flag5,
                                out pedestrians);
                            if (!flag5 && num6 >= 196u)
                            {
                                flag5 = true;
                                RoadBaseAI.SetTrafficLightState(num4,
                                    ref instance.m_segments.m_buffer[(int) prevPos.m_segment], currentFrameIndex - num5,
                                    vehicleLightState, pedestrianLightState, flag5, pedestrians);
                            }

                            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None ||
                                info.m_class.m_service != ItemClass.Service.Road)
                            {
                                switch (vehicleLightState)
                                {
                                    case RoadBaseAI.TrafficLightState.RedToGreen:
                                        if (num6 < 60u)
                                        {
                                            maxSpeed = 0f;
                                            return;
                                        }
                                        break;
                                    case RoadBaseAI.TrafficLightState.Red:
                                        maxSpeed = 0f;
                                        return;
                                    case RoadBaseAI.TrafficLightState.GreenToRed:
                                        if (num6 >= 30u)
                                        {
                                            maxSpeed = 0f;
                                            return;
                                        }
                                        break;
                                }
                            }
                        }
                        else
                        {
                            var stopCar = false;

                            if (TrafficPriority.isLeftSegment(prevPos.m_segment, position.m_segment, num2))
                            {
                                vehicleLightState =
                                    TrafficLightsManual.GetSegmentLight(num4, prevPos.m_segment).GetLightLeft();
                            }
                            else if (TrafficPriority.isRightSegment(prevPos.m_segment, position.m_segment, num2))
                            {
                                vehicleLightState =
                                    TrafficLightsManual.GetSegmentLight(num4, prevPos.m_segment).GetLightRight();
                            }
                            else
                            {
                                vehicleLightState =
                                    TrafficLightsManual.GetSegmentLight(num4, prevPos.m_segment).GetLightMain();
                            }

                            if (vehicleLightState == RoadBaseAI.TrafficLightState.Green)
                            {
                                var hasIncomingCars = TrafficPriority.incomingVehicles(vehicleID, num2);

                                if (hasIncomingCars)
                                {
                                    stopCar = true;
                                }
                            }

                            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None ||
                                info.m_class.m_service != ItemClass.Service.Road)
                            {
                                switch (vehicleLightState)
                                {
                                    case RoadBaseAI.TrafficLightState.RedToGreen:
                                        if (num6 < 60u)
                                        {
                                            stopCar = true;
                                        }
                                        break;
                                    case RoadBaseAI.TrafficLightState.Red:
                                        stopCar = true;
                                        break;
                                    case RoadBaseAI.TrafficLightState.GreenToRed:
                                        if (num6 >= 30u)
                                        {
                                            stopCar = true;
                                        }
                                        break;
                                }
                            }

                            if (stopCar)
                            {
                                maxSpeed = 0f;
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car &&
                            TrafficPriority.vehicleList.ContainsKey(vehicleID) &&
                            TrafficPriority.isPrioritySegment(num2, prevPos.m_segment))
                        {
                            uint currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                            uint frame = currentFrameIndex2 >> 4;

                            var prioritySegment = TrafficPriority.getPrioritySegment(num2, prevPos.m_segment);

                            if (TrafficPriority.vehicleList[vehicleID].carState == PriorityCar.CarState.None)
                            {
                                TrafficPriority.vehicleList[vehicleID].carState = PriorityCar.CarState.Enter;
                            }

                            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None &&
                                TrafficPriority.vehicleList[vehicleID].carState != PriorityCar.CarState.Leave)
                            {
                                if (prioritySegment.type == PrioritySegment.PriorityType.Stop)
                                {
                                    if (TrafficPriority.vehicleList[vehicleID].waitTime < 75)
                                    {
                                        TrafficPriority.vehicleList[vehicleID].carState = PriorityCar.CarState.Stop;

                                        if (vehicleData.GetLastFrameData().m_velocity.sqrMagnitude < 0.1f ||
                                            TrafficPriority.vehicleList[vehicleID].stopped)
                                        {
                                            TrafficPriority.vehicleList[vehicleID].stopped = true;
                                            TrafficPriority.vehicleList[vehicleID].waitTime++;

                                            if (TrafficPriority.vehicleList[vehicleID].waitTime > 2)
                                            {
                                                var hasIncomingCars = TrafficPriority.incomingVehicles(vehicleID, num2);

                                                if (hasIncomingCars)
                                                {
                                                    maxSpeed = 0f;
                                                    return;
                                                }
                                            }
                                            else
                                            {
                                                maxSpeed = 0f;
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            maxSpeed = 0f;
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        TrafficPriority.vehicleList[vehicleID].carState = PriorityCar.CarState.Leave;
                                    }
                                }
                                else if (prioritySegment.type == PrioritySegment.PriorityType.Yield)
                                {
                                    if (TrafficPriority.vehicleList[vehicleID].waitTime < 75)
                                    {
                                        TrafficPriority.vehicleList[vehicleID].waitTime++;
                                        TrafficPriority.vehicleList[vehicleID].carState = PriorityCar.CarState.Stop;
                                        maxSpeed = 0f;

                                        if (vehicleData.GetLastFrameData().m_velocity.sqrMagnitude <
                                            TrafficPriority.vehicleList[vehicleID].yieldSpeedReduce)
                                        {
                                            var hasIncomingCars = TrafficPriority.incomingVehicles(vehicleID, num2);

                                            if (hasIncomingCars)
                                            {
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        TrafficPriority.vehicleList[vehicleID].carState = PriorityCar.CarState.Leave;
                                    }
                                }
                                else if (prioritySegment.type == PrioritySegment.PriorityType.Main)
                                {
                                    TrafficPriority.vehicleList[vehicleID].waitTime++;
                                    TrafficPriority.vehicleList[vehicleID].carState = PriorityCar.CarState.Stop;
                                    maxSpeed = 0f;

                                    var hasIncomingCars = TrafficPriority.incomingVehicles(vehicleID, num2);

                                    if (hasIncomingCars)
                                    {
                                        TrafficPriority.vehicleList[vehicleID].stopped = true;
                                        return;
                                    }
                                    else
                                    {
                                        TrafficPriority.vehicleList[vehicleID].stopped = false;

                                        NetInfo info3 = instance.m_segments.m_buffer[(int) position.m_segment].Info;
                                        if (info3.m_lanes != null && info3.m_lanes.Length > (int) position.m_lane)
                                        {
                                            maxSpeed =
                                                this.CalculateTargetSpeed(vehicleID, ref vehicleData,
                                                    info3.m_lanes[(int) position.m_lane].m_speedLimit,
                                                    instance.m_lanes.m_buffer[(int) ((UIntPtr) laneID)].m_curve)*0.8f;
                                        }
                                        else
                                        {
                                            maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f)*
                                                       0.8f;
                                        }
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                TrafficPriority.vehicleList[vehicleID].carState = PriorityCar.CarState.Transit;
                            }
                        }
                    }
                }
            }

            NetInfo info2 = instance.m_segments.m_buffer[(int)position.m_segment].Info;
            if (info2.m_lanes != null && info2.m_lanes.Length > (int)position.m_lane)
            {
                var laneSpeedLimit = info2.m_lanes[(int) position.m_lane].m_speedLimit;

                if (TrafficRoadRestrictions.isSegment(position.m_segment))
                {
                    var restrictionSegment = TrafficRoadRestrictions.getSegment(position.m_segment);

                    if (restrictionSegment.speedLimits[(int) position.m_lane] > 0.1f)
                    {
                        laneSpeedLimit = restrictionSegment.speedLimits[(int) position.m_lane];
                    }
                }

                maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
            }
            else
            {
                maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
            }
        }
        public void CalculateSegmentPosition2(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
        {
            NetManager instance = Singleton<NetManager>.instance;
            instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
            NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
            if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane)
            {
                var laneSpeedLimit = info.m_lanes[(int)position.m_lane].m_speedLimit;

                if (TrafficRoadRestrictions.isSegment(position.m_segment))
                {
                    var restrictionSegment = TrafficRoadRestrictions.getSegment(position.m_segment);

                    if (restrictionSegment.speedLimits[(int)position.m_lane] > 0.1f)
                    {
                        laneSpeedLimit = restrictionSegment.speedLimits[(int)position.m_lane];
                    }
                }

                maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
            }
            else
            {
                maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
            }
        }

        public bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            VehicleInfo info = this.m_info;
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != Vehicle.Flags.None;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num3;
            float num4;
            if (PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground, 32f, out startPosA, out startPosB, out num, out num2) && PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, false, 32f, out endPosA, out endPosB, out num3, out num4))
            {
                if (!startBothWays || num < 10f)
                {
                    startPosB = default(PathUnit.Position);
                }
                if (!endBothWays || num3 < 10f)
                {
                    endPosB = default(PathUnit.Position);
                }
                uint path;

                if (Singleton<CustomPathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false, vehicleData.Info.m_class.m_service))
                {
                    if (vehicleData.m_path != 0u)
                    {
                        Singleton<CustomPathManager>.instance.ReleasePath(vehicleData.m_path);
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
