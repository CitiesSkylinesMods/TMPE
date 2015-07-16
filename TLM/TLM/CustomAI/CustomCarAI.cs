using System;
using ColossalFramework;
using ColossalFramework.Math;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TrafficManager.CustomAI
{
    class CustomCarAI : CarAI
    {
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle data, Vector3 physicsLodRefPos)
        {
            if ((data.m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None)
            {
                if (!CanFindPath(vehicleId, ref data))
                    return;
            }
            else if ((data.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None)
            {
                TrySpawn(vehicleId, ref data);
            }

            var lastFramePosition = data.GetLastFramePosition();
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
            SimulationStep(vehicleId, ref data, vehicleId, ref data, lodPhysics);
            if (data.m_leadingVehicle == 0 && data.m_trailingVehicle != 0)
            {
                var instance2 = Singleton<VehicleManager>.instance;
                var num = data.m_trailingVehicle;
                var num2 = 0;
                while (num != 0)
                {
                    var trailingVehicle = instance2.m_vehicles.m_buffer[num].m_trailingVehicle;
                    var info = instance2.m_vehicles.m_buffer[num].Info;
                    info.m_vehicleAI.SimulationStep(num, ref instance2.m_vehicles.m_buffer[num], vehicleId,
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
            var num3 = (m_info.m_class.m_service > ItemClass.Service.Office) ? 150 : 100;
            if ((data.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) ==
                Vehicle.Flags.None && data.m_cargoParent == 0)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            }
            else if (data.m_blockCounter >= num3 && LoadingExtension.Instance.DespawnEnabled)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            }
        }

        private bool CanFindPath(ushort vehicleId, ref Vehicle data)
        {
            var pathManager = Singleton<PathManager>.instance;
            var pathFindFlags = pathManager.m_pathUnits.m_buffer[(int) ((UIntPtr) data.m_path)].m_pathFindFlags;
            if ((pathFindFlags & 4) != 0)
            {
                data.m_pathPositionIndex = 255;
                data.m_flags &= ~Vehicle.Flags.WaitingPath;
                data.m_flags &= ~Vehicle.Flags.Arriving;
                PathfindSuccess(vehicleId, ref data);
                TrySpawn(vehicleId, ref data);
            }
            else if ((pathFindFlags & 8) != 0)
            {
                data.m_flags &= ~Vehicle.Flags.WaitingPath;
                Singleton<PathManager>.instance.ReleasePath(data.m_path);
                data.m_path = 0u;
                PathfindFailure(vehicleId, ref data);
                return false;
            }
            return true;
        }

        public void TmCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position nextPosition,
            PathUnit.Position position, uint laneId, byte offset, PathUnit.Position prevPos, uint prevLaneId,
            byte prevOffset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
        {
            NetManager instance = Singleton<NetManager>.instance;
            instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].CalculatePositionAndDirection(offset * 0.003921569f, out pos, out dir);
            Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
            Vector3 position2 = lastFrameData.m_position;
            Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)prevLaneId)].CalculatePosition(prevOffset * 0.003921569f);
            float num = 0.5f * lastFrameData.m_velocity.sqrMagnitude / m_info.m_braking + m_info.m_generatedInfo.m_size.z * 0.5f;

            if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car)
            {
                if (!TrafficPriority.VehicleList.ContainsKey(vehicleId))
                {
                    TrafficPriority.VehicleList.Add(vehicleId, new PriorityCar());
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
                    segment.b = pos + dir.normalized*m_info.m_generatedInfo.m_size.z;
                    num2 = instance.m_segments.m_buffer[position.m_segment].m_startNode;
                    num3 = instance.m_segments.m_buffer[position.m_segment].m_endNode;
                }
                else
                {
                    segment.b = pos - dir.normalized*m_info.m_generatedInfo.m_size.z;
                    num2 = instance.m_segments.m_buffer[position.m_segment].m_endNode;
                    num3 = instance.m_segments.m_buffer[position.m_segment].m_startNode;
                }
                ushort num4;
                if (prevOffset == 0)
                {
                    num4 = instance.m_segments.m_buffer[prevPos.m_segment].m_startNode;
                }
                else
                {
                    num4 = instance.m_segments.m_buffer[prevPos.m_segment].m_endNode;
                }

                if (num2 == num4)
                {
                    uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                    uint num5 = (uint)((num4 << 8) / 32768);
                    uint num6 = currentFrameIndex - num5 & 255u;

                    NetNode.Flags flags = instance.m_nodes.m_buffer[num2].m_flags;
                    NetLane.Flags flags2 =
                        (NetLane.Flags) instance.m_lanes.m_buffer[(int) ((UIntPtr) prevLaneId)].m_flags;
                    bool flag = (flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
                    bool flag2 = (flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
                    bool flag3 = (flags2 & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
                    if ((flags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) ==
                        NetNode.Flags.Junction && instance.m_nodes.m_buffer[num2].CountSegments() != 2)
                    {
                        float len = vehicleData.CalculateTotalLength(vehicleId) + 2f;
                        if (!instance.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].CheckSpace(len))
                        {
                            bool flag4 = false;
                            if (nextPosition.m_segment != 0 &&
                                instance.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].m_length < 30f)
                            {
                                NetNode.Flags flags3 = instance.m_nodes.m_buffer[num3].m_flags;
                                if ((flags3 &
                                     (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) !=
                                    NetNode.Flags.Junction || instance.m_nodes.m_buffer[num3].CountSegments() == 2)
                                {
                                    uint laneId2 = PathManager.GetLaneID(nextPosition);
                                    if (laneId2 != 0u)
                                    {
                                        flag4 = instance.m_lanes.m_buffer[(int) ((UIntPtr) laneId2)].CheckSpace(len);
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
                        TrafficPriority.VehicleList.ContainsKey(vehicleId) &&
                        TrafficPriority.IsPrioritySegment(num2, prevPos.m_segment))
                    {
                        uint currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                        uint frame = currentFrameIndex2 >> 4;

                        var prioritySegment = TrafficPriority.GetPrioritySegment(num2, prevPos.m_segment);
                        if (TrafficPriority.VehicleList[vehicleId].toNode != num2 ||
                            TrafficPriority.VehicleList[vehicleId].fromSegment != prevPos.m_segment)
                        {
                            if (TrafficPriority.VehicleList[vehicleId].toNode != 0 &&
                                TrafficPriority.VehicleList[vehicleId].fromSegment != 0)
                            {
                                var oldNode = TrafficPriority.VehicleList[vehicleId].toNode;
                                var oldSegment = TrafficPriority.VehicleList[vehicleId].fromSegment;

                                if (TrafficPriority.IsPrioritySegment(oldNode, oldSegment))
                                {
                                    var oldPrioritySegment = TrafficPriority.GetPrioritySegment(oldNode, oldSegment);
                                    TrafficPriority.VehicleList[vehicleId].waitTime = 0;
                                    TrafficPriority.VehicleList[vehicleId].stopped = false;
                                    oldPrioritySegment.RemoveCar(vehicleId);
                                }
                            }

                            // prevPos - current segment
                            // position - next segment
                            TrafficPriority.VehicleList[vehicleId].toNode = num2;
                            TrafficPriority.VehicleList[vehicleId].fromSegment = prevPos.m_segment;
                            TrafficPriority.VehicleList[vehicleId].toSegment = position.m_segment;
                            TrafficPriority.VehicleList[vehicleId].toLaneID = PathManager.GetLaneID(position);
                            TrafficPriority.VehicleList[vehicleId].fromLaneID = PathManager.GetLaneID(prevPos);
                            TrafficPriority.VehicleList[vehicleId].fromLaneFlags =
                                instance.m_lanes.m_buffer[PathManager.GetLaneID(prevPos)].m_flags;
                            TrafficPriority.VehicleList[vehicleId].yieldSpeedReduce = Random.Range(13f,
                                18f);

                            prioritySegment.AddCar(vehicleId);
                        }

                        TrafficPriority.VehicleList[vehicleId].lastFrame = frame;
                        TrafficPriority.VehicleList[vehicleId].lastSpeed =
                            vehicleData.GetLastFrameData().m_velocity.sqrMagnitude;
                    }

                    if (flag && (!flag3 || flag2))
                    {
                        var nodeSimulation = CustomRoadAI.GetNodeSimulation(num4);

                        NetInfo info = instance.m_nodes.m_buffer[num2].Info;
                        RoadBaseAI.TrafficLightState vehicleLightState;

                        if (nodeSimulation == null || (nodeSimulation.FlagTimedTrafficLights && !nodeSimulation.TimedTrafficLightsActive))
                        {
                            RoadBaseAI.TrafficLightState pedestrianLightState;
                            bool flag5;
                            bool pedestrians;
                            RoadBaseAI.GetTrafficLightState(num4,
                                ref instance.m_segments.m_buffer[prevPos.m_segment],
                                currentFrameIndex - num5, out vehicleLightState, out pedestrianLightState, out flag5,
                                out pedestrians);
                            if (!flag5 && num6 >= 196u)
                            {
                                flag5 = true;
                                RoadBaseAI.SetTrafficLightState(num4,
                                    ref instance.m_segments.m_buffer[prevPos.m_segment], currentFrameIndex - num5,
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

                            if (TrafficPriority.IsLeftSegment(prevPos.m_segment, position.m_segment, num2))
                            {
                                vehicleLightState =
                                    TrafficLightsManual.GetSegmentLight(num4, prevPos.m_segment).GetLightLeft();
                            }
                            else if (TrafficPriority.IsRightSegment(prevPos.m_segment, position.m_segment, num2))
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
                                var hasIncomingCars = TrafficPriority.IncomingVehicles(vehicleId, num2);

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
                            TrafficPriority.VehicleList.ContainsKey(vehicleId) &&
                            TrafficPriority.IsPrioritySegment(num2, prevPos.m_segment))
                        {
                            var currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                            var frame = currentFrameIndex2 >> 4;

                            var prioritySegment = TrafficPriority.GetPrioritySegment(num2, prevPos.m_segment);

                            if (TrafficPriority.VehicleList[vehicleId].carState == PriorityCar.CarState.None)
                            {
                                TrafficPriority.VehicleList[vehicleId].carState = PriorityCar.CarState.Enter;
                            }

                            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None &&
                                TrafficPriority.VehicleList[vehicleId].carState != PriorityCar.CarState.Leave)
                            {
                                if (prioritySegment.Type == PrioritySegment.PriorityType.Stop)
                                {
                                    if (TrafficPriority.VehicleList[vehicleId].waitTime < 75)
                                    {
                                        TrafficPriority.VehicleList[vehicleId].carState = PriorityCar.CarState.Stop;

                                        if (vehicleData.GetLastFrameData().m_velocity.sqrMagnitude < 0.1f ||
                                            TrafficPriority.VehicleList[vehicleId].stopped)
                                        {
                                            TrafficPriority.VehicleList[vehicleId].stopped = true;
                                            TrafficPriority.VehicleList[vehicleId].waitTime++;

                                            if (TrafficPriority.VehicleList[vehicleId].waitTime > 2)
                                            {
                                                var hasIncomingCars = TrafficPriority.IncomingVehicles(vehicleId, num2);

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
                                        TrafficPriority.VehicleList[vehicleId].carState = PriorityCar.CarState.Leave;
                                    }
                                }
                                else if (prioritySegment.Type == PrioritySegment.PriorityType.Yield)
                                {
                                    if (TrafficPriority.VehicleList[vehicleId].waitTime < 75)
                                    {
                                        TrafficPriority.VehicleList[vehicleId].waitTime++;
                                        TrafficPriority.VehicleList[vehicleId].carState = PriorityCar.CarState.Stop;
                                        maxSpeed = 0f;

                                        if (vehicleData.GetLastFrameData().m_velocity.sqrMagnitude <
                                            TrafficPriority.VehicleList[vehicleId].yieldSpeedReduce)
                                        {
                                            var hasIncomingCars = TrafficPriority.IncomingVehicles(vehicleId, num2);

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
                                        TrafficPriority.VehicleList[vehicleId].carState = PriorityCar.CarState.Leave;
                                    }
                                }
                                else if (prioritySegment.Type == PrioritySegment.PriorityType.Main)
                                {
                                    TrafficPriority.VehicleList[vehicleId].waitTime++;
                                    TrafficPriority.VehicleList[vehicleId].carState = PriorityCar.CarState.Stop;
                                    maxSpeed = 0f;

                                    var hasIncomingCars = TrafficPriority.IncomingVehicles(vehicleId, num2);

                                    if (hasIncomingCars)
                                    {
                                        TrafficPriority.VehicleList[vehicleId].stopped = true;
                                        return;
                                    }
                                    TrafficPriority.VehicleList[vehicleId].stopped = false;

                                    NetInfo info3 = instance.m_segments.m_buffer[position.m_segment].Info;
                                    if (info3.m_lanes != null && info3.m_lanes.Length > position.m_lane)
                                    {
                                        maxSpeed =
                                            CalculateTargetSpeed(vehicleId, ref vehicleData,
                                                info3.m_lanes[position.m_lane].m_speedLimit,
                                                instance.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].m_curve)*0.8f;
                                    }
                                    else
                                    {
                                        maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f)*
                                                   0.8f;
                                    }
                                    return;
                                }
                            }
                            else
                            {
                                TrafficPriority.VehicleList[vehicleId].carState = PriorityCar.CarState.Transit;
                            }
                        }
                    }
                }
            }

            NetInfo info2 = instance.m_segments.m_buffer[position.m_segment].Info;
            if (info2.m_lanes != null && info2.m_lanes.Length > position.m_lane)
            {
                var laneSpeedLimit = info2.m_lanes[position.m_lane].m_speedLimit;

                if (TrafficRoadRestrictions.IsSegment(position.m_segment))
                {
                    var restrictionSegment = TrafficRoadRestrictions.GetSegment(position.m_segment);

                    if (restrictionSegment.SpeedLimits[position.m_lane] > 0.1f)
                    {
                        laneSpeedLimit = restrictionSegment.SpeedLimits[position.m_lane];
                    }
                }

                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].m_curve);
            }
            else
            {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }
        }
        public void TmCalculateSegmentPositionPathFinder(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position position, uint laneId, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
        {
            var instance = Singleton<NetManager>.instance;
            instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].CalculatePositionAndDirection(offset * 0.003921569f, out pos, out dir);
            var info = instance.m_segments.m_buffer[position.m_segment].Info;
            if (info.m_lanes != null && info.m_lanes.Length > position.m_lane)
            {
                var laneSpeedLimit = info.m_lanes[position.m_lane].m_speedLimit;

                if (TrafficRoadRestrictions.IsSegment(position.m_segment))
                {
                    var restrictionSegment = TrafficRoadRestrictions.GetSegment(position.m_segment);

                    if (restrictionSegment.SpeedLimits[position.m_lane] > 0.1f)
                    {
                        laneSpeedLimit = restrictionSegment.SpeedLimits[position.m_lane];
                    }
                }

                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].m_curve);
            }
            else
            {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }
        }

        protected override bool StartPathFind(ushort vehicleId, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            VehicleInfo info = m_info;
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != Vehicle.Flags.None;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num3;
            float num4;
            const bool requireConnect = false;
            const float maxDistance = 32f;
            if (PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground, requireConnect, maxDistance, out startPosA, out startPosB, out num, out num2) && PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, false, requireConnect, 32f, out endPosA, out endPosB, out num3, out num4))
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

                if (Singleton<CustomPathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, IsHeavyVehicle(), IgnoreBlocked(vehicleId, ref vehicleData), false, false, vehicleData.Info.m_class.m_service))
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
