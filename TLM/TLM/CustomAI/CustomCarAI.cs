using System;
using ColossalFramework;
using ColossalFramework.Math;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TrafficManager.CustomAI
{
    internal class CustomCarAI : CarAI
    {
        private const int MaxTrailingVehicles = 16384;
        private const float FarLod = 1210000f;
        private const float CloseLod = 250000f;

        public void TrafficManagerSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos)
        {
            try
            {
                FindPathIfNeeded(vehicleId, ref vehicleData);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            SpawnVehicleIfWaiting(vehicleId, ref vehicleData);

            SimulateVehicleChain(vehicleId, ref vehicleData, physicsLodRefPos);

            DespawnVehicles(vehicleId, vehicleData);
        }

        private void SimulateVehicleChain(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos)
        {
            var lastFramePosition = vehicleData.GetLastFramePosition();

            var lodPhysics = CalculateLod(physicsLodRefPos, lastFramePosition);

            SimulationStep(vehicleId, ref vehicleData, vehicleId, ref vehicleData, lodPhysics);
            
            if (vehicleData.m_leadingVehicle != 0 || vehicleData.m_trailingVehicle == 0)
                return;

            var vehicleManager = Singleton<VehicleManager>.instance;
            var trailingVehicleId = vehicleData.m_trailingVehicle;
            SimulateTrailingVehicles(vehicleId, ref vehicleData, lodPhysics, trailingVehicleId, vehicleManager,
                0);
        }

        private void DespawnVehicles(ushort vehicleId, Vehicle vehicleData)
        {
            DespawnInvalidVehicles(vehicleId, vehicleData);

            DespawnVehicleIfOverBlockMax(vehicleId, vehicleData);
        }

        private void DespawnInvalidVehicles(ushort vehicleId, Vehicle vehicleData)
        {
            if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) ==
                Vehicle.Flags.None && vehicleData.m_cargoParent == 0)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            }
        }

        private void DespawnVehicleIfOverBlockMax(ushort vehicleId, Vehicle vehicleData)
        {
            var maxBlockingVehicles = CalculateMaxBlockingVehicleCount();
            if (vehicleData.m_blockCounter >= maxBlockingVehicles && LoadingExtension.Instance.DespawnEnabled)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            }
        }

        private int CalculateMaxBlockingVehicleCount()
        {
            return (m_info.m_class.m_service > ItemClass.Service.Office) ? 150 : 100;
        }
        
        private void SimulateTrailingVehicles(ushort vehicleId, ref Vehicle vehicleData, int lodPhysics,
            ushort leadingVehicleId, VehicleManager vehicleManager, int numberOfIterations)
        {
            if (leadingVehicleId == 0)
            {
                return;
            }

            var trailingVehicleId = vehicleManager.m_vehicles.m_buffer[leadingVehicleId].m_trailingVehicle;
            var trailingVehicleInfo = vehicleManager.m_vehicles.m_buffer[trailingVehicleId].Info;

            trailingVehicleInfo.m_vehicleAI.SimulationStep(trailingVehicleId,
                ref vehicleManager.m_vehicles.m_buffer[trailingVehicleId], vehicleId,
                ref vehicleData, lodPhysics);

            if (++numberOfIterations > MaxTrailingVehicles)
            {
                CODebugBase<LogChannel>.Error(LogChannel.Core,
                    "Invalid list detected!\n" + Environment.StackTrace);
                return;
            }
            SimulateTrailingVehicles(trailingVehicleId, ref vehicleData, lodPhysics, trailingVehicleId, vehicleManager,
                numberOfIterations);
        }

        private static int CalculateLod(Vector3 physicsLodRefPos, Vector3 lastFramePosition)
        {
            int lodPhysics;
            if (Vector3.SqrMagnitude(physicsLodRefPos - lastFramePosition) >= FarLod)
            {
                lodPhysics = 2;
            }
            else if (Vector3.SqrMagnitude(Singleton<SimulationManager>.
                instance.m_simulationView.m_position - lastFramePosition) >= CloseLod)
            {
                lodPhysics = 1;
            }
            else
            {
                lodPhysics = 0;
            }
            return lodPhysics;
        }

        private void SpawnVehicleIfWaiting(ushort vehicleId, ref Vehicle vehicleData)
        {
            if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None)
            {
                TrySpawn(vehicleId, ref vehicleData);
            }
        }

        private void FindPathIfNeeded(ushort vehicleId, ref Vehicle vehicleData)
        {
            if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) == Vehicle.Flags.None) return;

            if (!CanFindPath(vehicleId, ref vehicleData))
                throw new InvalidOperationException("Path Not Available for Vehicle");
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
            var netManager = Singleton<NetManager>.instance;
            netManager.m_lanes.m_buffer[(int)((UIntPtr) laneId)]
                .CalculatePositionAndDirection(offset*0.003921569f, out pos, out dir);

            var lastFrameData = vehicleData.GetLastFrameData();
            var vehiclePositionLastFrame = lastFrameData.m_position;

            // I think this is supposed to be the lane position?
            var lanePosition = netManager.m_lanes.m_buffer[(int) ((UIntPtr) prevLaneId)]
                .CalculatePosition(prevOffset*0.003921569f);

            var num = 0.5f*lastFrameData.m_velocity.sqrMagnitude/m_info.m_braking + m_info.m_generatedInfo.m_size.z*0.5f;

            if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car)
            {
                if (!TrafficPriority.VehicleList.ContainsKey(vehicleId))
                {
                    TrafficPriority.VehicleList.Add(vehicleId, new PriorityCar());
                }
            }

            if (Vector3.Distance(vehiclePositionLastFrame, lanePosition) >= num - 1f)
            {
                ushort destinationNode;
                ushort nodeId3;
                if (offset < position.m_offset)
                {
                    destinationNode = netManager.m_segments.m_buffer[position.m_segment].m_startNode;
                    nodeId3 = netManager.m_segments.m_buffer[position.m_segment].m_endNode;
                }
                else
                {
                    destinationNode = netManager.m_segments.m_buffer[position.m_segment].m_endNode;
                    nodeId3 = netManager.m_segments.m_buffer[position.m_segment].m_startNode;
                }
                var nodeId4 = prevOffset == 0 ? netManager.m_segments.m_buffer[prevPos.m_segment].m_startNode :
                    netManager.m_segments.m_buffer[prevPos.m_segment].m_endNode;

                if (destinationNode == nodeId4)
                {
                    var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                    var num5 = (uint) ((nodeId4 << 8)/32768);
                    var num6 = currentFrameIndex - num5 & 255u;

                    var flags = netManager.m_nodes.m_buffer[destinationNode].m_flags;
                    var flags2 =
                        (NetLane.Flags) netManager.m_lanes.m_buffer[(int) ((UIntPtr) prevLaneId)].m_flags;
                    var flag = (flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
                    var flag2 = (flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
                    var flag3 = (flags2 & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
                    if ((flags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) ==
                        NetNode.Flags.Junction && netManager.m_nodes.m_buffer[destinationNode].CountSegments() != 2)
                    {
                        var len = vehicleData.CalculateTotalLength(vehicleId) + 2f;
                        if (!netManager.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].CheckSpace(len))
                        {
                            var flag4 = false;
                            if (nextPosition.m_segment != 0 &&
                                netManager.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].m_length < 30f)
                            {
                                var flags3 = netManager.m_nodes.m_buffer[nodeId3].m_flags;
                                if ((flags3 &
                                     (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) !=
                                    NetNode.Flags.Junction || netManager.m_nodes.m_buffer[nodeId3].CountSegments() == 2)
                                {
                                    var laneId2 = PathManager.GetLaneID(nextPosition);
                                    if (laneId2 != 0u)
                                    {
                                        flag4 = netManager.m_lanes.m_buffer[(int) ((UIntPtr) laneId2)].CheckSpace(len);
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
                        TrafficPriority.IsPrioritySegment(destinationNode, prevPos.m_segment))
                    {
                        var currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                        var frame = currentFrameIndex2 >> 4;

                        var prioritySegment = TrafficPriority.GetPrioritySegment(destinationNode, prevPos.m_segment);
                        if (TrafficPriority.VehicleList[vehicleId].ToNode != destinationNode ||
                            TrafficPriority.VehicleList[vehicleId].FromSegment != prevPos.m_segment)
                        {
                            if (TrafficPriority.VehicleList[vehicleId].ToNode != 0 &&
                                TrafficPriority.VehicleList[vehicleId].FromSegment != 0)
                            {
                                var oldNode = TrafficPriority.VehicleList[vehicleId].ToNode;
                                var oldSegment = TrafficPriority.VehicleList[vehicleId].FromSegment;

                                if (TrafficPriority.IsPrioritySegment(oldNode, oldSegment))
                                {
                                    var oldPrioritySegment = TrafficPriority.GetPrioritySegment(oldNode, oldSegment);
                                    TrafficPriority.VehicleList[vehicleId].WaitTime = 0;
                                    TrafficPriority.VehicleList[vehicleId].Stopped = false;
                                    oldPrioritySegment.RemoveCar(vehicleId);
                                }
                            }

                            // prevPos - current segment
                            // position - next segment
                            TrafficPriority.VehicleList[vehicleId].ToNode = destinationNode;
                            TrafficPriority.VehicleList[vehicleId].FromSegment = prevPos.m_segment;
                            TrafficPriority.VehicleList[vehicleId].ToSegment = position.m_segment;
                            TrafficPriority.VehicleList[vehicleId].ToLaneId = PathManager.GetLaneID(position);
                            TrafficPriority.VehicleList[vehicleId].FromLaneId = PathManager.GetLaneID(prevPos);
                            TrafficPriority.VehicleList[vehicleId].FromLaneFlags =
                                netManager.m_lanes.m_buffer[PathManager.GetLaneID(prevPos)].m_flags;
                            TrafficPriority.VehicleList[vehicleId].ReduceSpeedByValueToYield = Random.Range(13f,
                                18f);

                            prioritySegment.AddCar(vehicleId);
                        }

                        TrafficPriority.VehicleList[vehicleId].LastFrame = frame;
                        TrafficPriority.VehicleList[vehicleId].LastSpeed =
                            vehicleData.GetLastFrameData().m_velocity.sqrMagnitude;
                    }

                    if (flag && (!flag3 || flag2))
                    {
                        var nodeSimulation = CustomRoadAI.GetNodeSimulation(nodeId4);

                        var destinationInfo = netManager.m_nodes.m_buffer[destinationNode].Info;
                        RoadBaseAI.TrafficLightState vehicleLightState;

                        if (nodeSimulation == null ||
                            (nodeSimulation.FlagTimedTrafficLights && !nodeSimulation.TimedTrafficLightsActive))
                        {
                            RoadBaseAI.TrafficLightState pedestrianLightState;
                            bool flag5;
                            bool pedestrians;
                            RoadBaseAI.GetTrafficLightState(nodeId4,
                                ref netManager.m_segments.m_buffer[prevPos.m_segment],
                                currentFrameIndex - num5, out vehicleLightState, out pedestrianLightState, out flag5,
                                out pedestrians);
                            if (!flag5 && num6 >= 196u)
                            {
                                flag5 = true;
                                RoadBaseAI.SetTrafficLightState(nodeId4,
                                    ref netManager.m_segments.m_buffer[prevPos.m_segment], currentFrameIndex - num5,
                                    vehicleLightState, pedestrianLightState, flag5, pedestrians);
                            }

                            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None ||
                                destinationInfo.m_class.m_service != ItemClass.Service.Road)
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

                            if (TrafficPriority.IsLeftSegment(prevPos.m_segment, position.m_segment, destinationNode))
                            {
                                vehicleLightState =
                                    TrafficLightsManual.GetSegmentLight(nodeId4, prevPos.m_segment).GetLightLeft();
                            }
                            else if (TrafficPriority.IsRightSegment(prevPos.m_segment, position.m_segment, destinationNode))
                            {
                                vehicleLightState =
                                    TrafficLightsManual.GetSegmentLight(nodeId4, prevPos.m_segment).GetLightRight();
                            }
                            else
                            {
                                vehicleLightState =
                                    TrafficLightsManual.GetSegmentLight(nodeId4, prevPos.m_segment).GetLightMain();
                            }

                            if (vehicleLightState == RoadBaseAI.TrafficLightState.Green)
                            {
                                var hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNode);

                                if (hasIncomingCars)
                                {
                                    stopCar = true;
                                }
                            }

                            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None ||
                                destinationInfo.m_class.m_service != ItemClass.Service.Road)
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
                            TrafficPriority.IsPrioritySegment(destinationNode, prevPos.m_segment))
                        {
                            var currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                            var frame = currentFrameIndex2 >> 4;

                            var prioritySegment = TrafficPriority.GetPrioritySegment(destinationNode, prevPos.m_segment);

                            if (TrafficPriority.VehicleList[vehicleId].CarState == CarState.None)
                            {
                                TrafficPriority.VehicleList[vehicleId].CarState = CarState.Enter;
                            }

                            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None &&
                                TrafficPriority.VehicleList[vehicleId].CarState != CarState.Leave)
                            {
                                bool hasIncomingCars;
                                switch (prioritySegment.Type)
                                {
                                    case PrioritySegment.PriorityType.Stop:
                                        if (TrafficPriority.VehicleList[vehicleId].WaitTime < 75)
                                        {
                                            TrafficPriority.VehicleList[vehicleId].CarState = CarState.Stop;

                                            if (vehicleData.GetLastFrameData().m_velocity.sqrMagnitude < 0.1f ||
                                                TrafficPriority.VehicleList[vehicleId].Stopped)
                                            {
                                                TrafficPriority.VehicleList[vehicleId].Stopped = true;
                                                TrafficPriority.VehicleList[vehicleId].WaitTime++;

                                                if (TrafficPriority.VehicleList[vehicleId].WaitTime > 2)
                                                {
                                                    hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNode);

                                                    if (hasIncomingCars)
                                                    {
                                                        maxSpeed = 0f;
                                                        return;
                                                    }
                                                    TrafficPriority.VehicleList[vehicleId].CarState =
                                                        CarState.Leave;
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
                                            TrafficPriority.VehicleList[vehicleId].CarState = CarState.Leave;
                                        }
                                        break;
                                    case PrioritySegment.PriorityType.Yield:
                                        if (TrafficPriority.VehicleList[vehicleId].WaitTime < 75)
                                        {
                                            TrafficPriority.VehicleList[vehicleId].WaitTime++;
                                            TrafficPriority.VehicleList[vehicleId].CarState = CarState.Stop;
                                            maxSpeed = 0f;

                                            if (vehicleData.GetLastFrameData().m_velocity.sqrMagnitude <
                                                TrafficPriority.VehicleList[vehicleId].ReduceSpeedByValueToYield)
                                            {
                                                hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNode);

                                                if (hasIncomingCars)
                                                {
                                                    return;
                                                }
                                            }
                                            else
                                            {
                                                maxSpeed = vehicleData.GetLastFrameData().m_velocity.sqrMagnitude -
                                                           TrafficPriority.VehicleList[vehicleId]
                                                               .ReduceSpeedByValueToYield;
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            TrafficPriority.VehicleList[vehicleId].CarState = CarState.Leave;
                                        }
                                        break;
                                    case PrioritySegment.PriorityType.Main:
                                        TrafficPriority.VehicleList[vehicleId].WaitTime++;
                                        TrafficPriority.VehicleList[vehicleId].CarState = CarState.Stop;
                                        maxSpeed = 0f;

                                        hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNode);

                                        if (hasIncomingCars)
                                        {
                                            TrafficPriority.VehicleList[vehicleId].Stopped = true;
                                            return;
                                        }
                                        TrafficPriority.VehicleList[vehicleId].Stopped = false;

                                        var info3 = netManager.m_segments.m_buffer[position.m_segment].Info;
                                        if (info3.m_lanes != null && info3.m_lanes.Length > position.m_lane)
                                        {
                                            maxSpeed =
                                                CalculateTargetSpeed(vehicleId, ref vehicleData,
                                                    info3.m_lanes[position.m_lane].m_speedLimit,
                                                    netManager.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].m_curve)*0.8f;
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
                                TrafficPriority.VehicleList[vehicleId].CarState = CarState.Transit;
                            }
                        }
                    }
                }
            }

            var info2 = netManager.m_segments.m_buffer[position.m_segment].Info;
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

                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit,
                    netManager.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].m_curve);
            }
            else
            {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }
        }

        public void TmCalculateSegmentPositionPathFinder(ushort vehicleId, ref Vehicle vehicleData,
            PathUnit.Position position, uint laneId, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed)
        {
            var instance = Singleton<NetManager>.instance;
            instance.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].CalculatePositionAndDirection(offset*0.003921569f,
                out pos, out dir);
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

                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit,
                    instance.m_lanes.m_buffer[(int) ((UIntPtr) laneId)].m_curve);
            }
            else
            {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }
        }

        protected override bool StartPathFind(ushort vehicleId, ref Vehicle vehicleData, Vector3 startPos,
            Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            var info = m_info;
            var allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) !=
                                   Vehicle.Flags.None;
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
            if (!PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle,
                info.m_vehicleType, allowUnderground, requireConnect, maxDistance, out startPosA, out startPosB,
                out num, out num2) ||
                !PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle,
                    info.m_vehicleType, false, requireConnect, 32f, out endPosA, out endPosB, out num3, out num4))
                return false;
            if (!startBothWays || num < 10f)
            {
                startPosB = default(PathUnit.Position);
            }
            if (!endBothWays || num3 < 10f)
            {
                endPosB = default(PathUnit.Position);
            }
            uint path;

            if (!Singleton<CustomPathManager>.instance.CreatePath(out path,
                ref Singleton<SimulationManager>.instance.m_randomizer,
                Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB,
                default(PathUnit.Position), NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, IsHeavyVehicle(),
                IgnoreBlocked(vehicleId, ref vehicleData), false, false, vehicleData.Info.m_class.m_service))
                return false;
            if (vehicleData.m_path != 0u)
            {
                Singleton<CustomPathManager>.instance.ReleasePath(vehicleData.m_path);
            }
            vehicleData.m_path = path;
            vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
            return true;
        }
    }
}