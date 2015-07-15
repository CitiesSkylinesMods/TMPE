using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.Traffic
{
    class TrafficPriority
    {
        public static bool LeftHandDrive;

        public static Dictionary<int, TrafficSegment> PrioritySegments = new Dictionary<int, TrafficSegment>();

        public static Dictionary<ushort, PriorityCar> VehicleList = new Dictionary<ushort, PriorityCar>();

        public static void AddPrioritySegment(ushort nodeId, int segmentId, PrioritySegment.PriorityType type)
        {
            if (PrioritySegments.ContainsKey(segmentId))
            {
                var prioritySegment = PrioritySegments[segmentId];

                prioritySegment.Node2 = nodeId;
                prioritySegment.Instance2 = new PrioritySegment(nodeId, segmentId, type);
            }
            else
            {
                PrioritySegments.Add(segmentId, new TrafficSegment());
                PrioritySegments[segmentId].Segment = segmentId;
                PrioritySegments[segmentId].Node1 = nodeId;
                PrioritySegments[segmentId].Instance1 = new PrioritySegment(nodeId, segmentId, type);
            }
        }

        public static void RemovePrioritySegment(ushort nodeId, int segmentId)
        {
            var prioritySegment = PrioritySegments[segmentId];

            if (prioritySegment.Node1 == nodeId)
            {
                prioritySegment.Node1 = 0;
                prioritySegment.Instance1 = null;
            }
            else
            {
                prioritySegment.Node2 = 0;
                prioritySegment.Instance2 = null;
            }

            if (prioritySegment.Node1 == 0 && prioritySegment.Node2 == 0)
            {
                PrioritySegments.Remove(segmentId);
            }
        }

        public static bool IsPrioritySegment(ushort nodeId, int segmentId)
        {
            if (PrioritySegments.ContainsKey(segmentId))
            {
                var prioritySegment = PrioritySegments[segmentId];

                if (prioritySegment.Node1 == nodeId || prioritySegment.Node2 == nodeId)
                {
                    return true;
                }
            }

            return false;
        }

        public static PrioritySegment GetPrioritySegment(ushort nodeId, int segmentId)
        {
            if (PrioritySegments.ContainsKey(segmentId))
            {
                var prioritySegment = PrioritySegments[segmentId];

                if (prioritySegment.Node1 == nodeId)
                {
                    return prioritySegment.Instance1;
                }
                if (prioritySegment.Node2 == nodeId)
                {
                    return prioritySegment.Instance2;
                }
            }

            return null;
        }

        public static bool IncomingVehicles(ushort targetCar, ushort nodeId)
        {
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            var frame = currentFrameIndex >> 4;
            var node = TrafficLightTool.GetNetNode(nodeId);

            var fromPrioritySegment = GetPrioritySegment(nodeId, VehicleList[targetCar].fromSegment);

            List<ushort> removeCarList = new List<ushort>();

            var numCars = 0;

            // get all cars
            for (int s = 0; s < 8; s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0 && segment != VehicleList[targetCar].fromSegment)
                {
                    if (IsPrioritySegment(nodeId, segment))
                    {
                        var prioritySegment = GetPrioritySegment(nodeId, segment);

                        // select outdated cars
                        removeCarList.AddRange(from car in prioritySegment.Cars let frameReduce = VehicleList[car].lastSpeed < 70 ? 4u : 2u where VehicleList[car].lastFrame < frame - frameReduce select car);

                        // remove outdated cars
                        foreach (var rcar in removeCarList)
                        {
                            VehicleList[rcar].resetCar();
                            prioritySegment.RemoveCar(rcar);
                        }

                        removeCarList.Clear();

                        if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None)
                        {
                            if (fromPrioritySegment.Type == PrioritySegment.PriorityType.Main)
                            {
                                if (prioritySegment.Type == PrioritySegment.PriorityType.Main)
                                {
                                    numCars += prioritySegment.NumCars;

                                    foreach (var car in prioritySegment.Cars)
                                    {
                                        if (VehicleList[car].lastSpeed > 0.1f)
                                        {
                                            numCars = CheckSameRoadIncomingCar(targetCar, car, nodeId)
                                                ? numCars - 1
                                                : numCars;
                                        }
                                        else
                                        {
                                            numCars--;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                numCars += prioritySegment.NumCars;

                                foreach (var car in prioritySegment.Cars)
                                {
                                    if (prioritySegment.Type == PrioritySegment.PriorityType.Main)
                                    {
                                        if (!VehicleList[car].stopped)
                                        {
                                            numCars = CheckPriorityRoadIncomingCar(targetCar, car, nodeId)
                                                ? numCars - 1
                                                : numCars;
                                        }
                                        else
                                        {
                                            numCars--;
                                        }
                                    }
                                    else
                                    {
                                        if (VehicleList[car].lastSpeed > 0.1f)
                                        {
                                            numCars = CheckSameRoadIncomingCar(targetCar, car, nodeId)
                                                ? numCars - 1
                                                : numCars;
                                        }
                                        else
                                        {
                                            numCars--;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (TrafficLightsManual.IsSegmentLight(nodeId, segment))
                            {
                                var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segment);

                                if (segmentLight.GetLightMain() == RoadBaseAI.TrafficLightState.Green)
                                {
                                    numCars += prioritySegment.NumCars;

                                    foreach (var car in prioritySegment.Cars)
                                    {
                                        if (VehicleList[car].lastSpeed > 1f)
                                        {
                                            numCars = CheckSameRoadIncomingCar(targetCar, car, nodeId)
                                                ? numCars - 1
                                                : numCars;
                                        }
                                        else
                                        {
                                            numCars--;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (numCars > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool CheckSameRoadIncomingCar(ushort targetCarId, ushort incomingCarId, ushort nodeId)
        {
            if (LeftHandDrive)
            {
                return _checkSameRoadIncomingCarLeftHandDrive(targetCarId, incomingCarId, nodeId);
            }
            return _checkSameRoadIncomingCarRightHandDrive(targetCarId, incomingCarId, nodeId);
        }

        protected static bool _checkSameRoadIncomingCarLeftHandDrive(ushort targetCarId, ushort incomingCarId,
            ushort nodeId)
        {
            var targetCar = VehicleList[targetCarId];
            var incomingCar = VehicleList[incomingCarId];

            if (IsRightSegment(targetCar.fromSegment, incomingCar.fromSegment, nodeId))
            {
                if (IsRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                {
                    if (IsLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId))
                    {
                        return true;
                    }
                }
                else if (targetCar.toSegment == incomingCar.toSegment && targetCar.toLaneID != incomingCar.toLaneID)
                {
                    return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                }
            }
            else if (IsLeftSegment(targetCar.fromSegment, incomingCar.fromSegment, nodeId))
                // incoming is on the left
            {
                return true;
            }
            else // incoming is in front or elsewhere
            {
                if (!IsRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                    // target car not going left
                {
                    return true;
                }
                else if (IsLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId))
                {
                    if (targetCar.toLaneID != incomingCar.toLaneID)
                    {
                        return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                }
                else if (IsLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeId) && IsLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId)) // both left turns
                {
                    return true;
                }
            }

            return false;
        }

        protected static bool _checkSameRoadIncomingCarRightHandDrive(ushort targetCarId, ushort incomingCarId,
            ushort nodeId)
        {
            var targetCar = VehicleList[targetCarId];
            var incomingCar = VehicleList[incomingCarId];

            if (IsRightSegment(targetCar.fromSegment, incomingCar.fromSegment, nodeId))
            {
                if (IsRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                {
                    return true;
                }
                else if (targetCar.toSegment == incomingCar.toSegment && targetCar.toLaneID != incomingCar.toLaneID)
                {
                    return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                }
            }
            else if (IsLeftSegment(targetCar.fromSegment, incomingCar.fromSegment, nodeId))
                // incoming is on the left
            {
                return true;
            }
            else // incoming is in front or elsewhere
            {
                if (!IsLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                    // target car not going left
                {
                    return true;
                }
                else if (IsRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId))
                {
                    if (targetCar.toLaneID != incomingCar.toLaneID)
                    {
                        return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                }
                else if (IsLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeId) && IsLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId)) // both left turns
                {
                    return true;
                }
            }

            return false;
        }


        public static bool CheckPriorityRoadIncomingCar(ushort targetCarId, ushort incomingCarId, ushort nodeId)
        {
            if (LeftHandDrive)
            {
                return _checkPriorityRoadIncomingCarLeftHandDrive(targetCarId, incomingCarId, nodeId);
            }
            return _checkPriorityRoadIncomingCarRightHandDrive(targetCarId, incomingCarId, nodeId);
        }

        protected static bool _checkPriorityRoadIncomingCarLeftHandDrive(ushort targetCarId, ushort incomingCarId,
            ushort nodeId)
        {
            var targetCar = VehicleList[targetCarId];
            var incomingCar = VehicleList[incomingCarId];

            if (incomingCar.toSegment == targetCar.toSegment)
            {
                if (incomingCar.toLaneID != targetCar.toLaneID)
                {
                    if (IsRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                    // target car goes right
                    {
                        // go if incoming car is in the left lane
                        return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                    else if (IsLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                    // target car goes left
                    {
                        // go if incoming car is in the right lane
                        return LaneOrderCorrect(targetCar.toSegment, incomingCar.toLaneID, targetCar.toLaneID);
                    }
                    else // target car goes straight (or other road)
                    {
                        if (IsRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId)) // incoming car goes right
                        {
                            // go if incoming car is in the left lane
                            return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, targetCar.toLaneID);
                        }
                        else if (IsLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId)) // incoming car goes left
                        {
                            // go if incoming car is in the right lane
                            return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID,
                                incomingCar.toLaneID);
                        }
                    }
                }
            }
            else if (incomingCar.toSegment == targetCar.fromSegment)
            {
                if (IsLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId))
                {
                    return true;
                }
                else if (targetCar.toSegment == incomingCar.fromSegment)
                {
                    return true;
                }
            }
            else // if no segment match
            {
                // target car turning right
                if (IsLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                {
                    return true;
                }
                if (IsLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId)) // incoming car turning right
                {
                    return true;
                }
            }

            return false;
        }

        protected static bool _checkPriorityRoadIncomingCarRightHandDrive(ushort targetCarId, ushort incomingCarId,
            ushort nodeId)
        {
            var targetCar = VehicleList[targetCarId];
            var incomingCar = VehicleList[incomingCarId];

            if (incomingCar.toSegment == targetCar.toSegment)
            {
                if (incomingCar.toLaneID != targetCar.toLaneID)
                {
                    if (IsRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                    // target car goes right
                    {
                        // go if incoming car is in the left lane
                        return LaneOrderCorrect(targetCar.toSegment, incomingCar.toLaneID, targetCar.toLaneID);
                    }
                    if (IsLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                        // target car goes left
                    {
                        // go if incoming car is in the right lane
                        return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                    if (IsRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId)) // incoming car goes right
                    {
                        // go if incoming car is in the left lane
                        return LaneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                    if (IsLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId)) // incoming car goes left
                    {
                        // go if incoming car is in the right lane
                        return LaneOrderCorrect(targetCar.toSegment, incomingCar.toLaneID,
                            targetCar.toLaneID);
                    }
                }
            }
            else if (incomingCar.toSegment == targetCar.fromSegment)
            {
                if (IsRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId))
                {
                    return true;
                }
                if (targetCar.toSegment == incomingCar.fromSegment)
                {
                    return true;
                }
            }
            else // if no segment match
            {
                // target car turning right
                if (IsRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeId))
                {
                    return true;
                }
                if (IsRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeId)) // incoming car turning right
                {
                    return true;
                }
            }

            return false;
        }

        public static bool LaneOrderCorrect(int segmentid, uint leftLane, uint rightLane)
        {
            var instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[segmentid];
            var info = segment.Info;

            var num2 = segment.m_lanes;
            var num3 = 0;

            var oneWaySegment = true;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    (info.m_lanes[num3].m_direction == NetInfo.Direction.Backward))
                {
                    oneWaySegment = false;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            num3 = 0;
            float leftLanePosition = 0f;
            float rightLanePosition = 0f;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (num2 == leftLane)
                {
                    leftLanePosition = info.m_lanes[num3].m_position;
                }

                if (num2 == rightLane)
                {
                    rightLanePosition = info.m_lanes[num3].m_position;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            if (oneWaySegment)
            {
                if (leftLanePosition < rightLanePosition)
                {
                    return true;
                }
            }
            else
            {
                if (leftLanePosition > rightLanePosition)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsRightSegment(int fromSegment, int toSegment, ushort nodeid)
        {
            return IsLeftSegment(toSegment, fromSegment, nodeid);
        }

        public static bool IsLeftSegment(int fromSegment, int toSegment, ushort nodeid)
        {
            Vector3 fromDir = GetSegmentDir(fromSegment, nodeid);
            fromDir.y = 0;
            fromDir.Normalize();
            Vector3 toDir = GetSegmentDir(toSegment, nodeid);
            toDir.y = 0;
            toDir.Normalize();
            return Vector3.Cross(fromDir, toDir).y >= 0.5;
        }

        public static bool HasLeftSegment(int segmentId, ushort nodeId, bool debug = false)
        {
            var node = TrafficLightTool.GetNetNode(nodeId);

            for (int s = 0; s < 8; s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0 && segment != segmentId)
                {
                    if (IsLeftSegment(segmentId, segment, nodeId))
                    {
                        if (debug)
                        {
                            Debug.Log("LEFT: " + segment + " " + GetSegmentDir(segment, nodeId));
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasRightSegment(int segmentId, ushort nodeId, bool debug = false)
        {
            var node = TrafficLightTool.GetNetNode(nodeId);

            for (int s = 0; s < 8; s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0 && segment != segmentId)
                {
                    if (IsRightSegment(segmentId, segment, nodeId))
                    {
                        if (debug)
                        {
                            Debug.Log("RIGHT: " + segment + " " + GetSegmentDir(segment, nodeId));
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasForwardSegment(int segmentId, ushort nodeId, bool debug = false)
        {
            var node = TrafficLightTool.GetNetNode(nodeId);

            for (var s = 0; s < 8; s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0 && segment != segmentId)
                {
                    if (!IsRightSegment(segmentId, segment, nodeId) && !IsLeftSegment(segmentId, segment, nodeId))
                    {
                        if (debug)
                        {
                            Debug.Log("FORWARD: " + segment + " " + GetSegmentDir(segment, nodeId));
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasLeftLane(ushort nodeId, int segmentId)
        {
            var instance = Singleton<NetManager>.instance;
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            var dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == nodeId)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var info = segment.Info;

            var num2 = segment.m_lanes;
            var num3 = 0;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[num2].m_flags;

                if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.None)
                {
                    return true;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            return false;
        }

        public static bool HasForwardLane(ushort nodeId, int segmentId)
        {
            var instance = Singleton<NetManager>.instance;
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            NetInfo.Direction dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == nodeId)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var info = segment.Info;

            var num2 = segment.m_lanes;
            var num3 = 0;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[num2].m_flags;

                if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.Forward)
                {
                    return true;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            return false;
        }

        public static bool HasRightLane(ushort nodeId, int segmentId)
        {
            var instance = Singleton<NetManager>.instance;
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            var dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == nodeId)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var info = segment.Info;

            var num2 = segment.m_lanes;
            var num3 = 0;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[num2].m_flags;

                if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.Right)
                {
                    return true;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            return false;
        }

        public static Vector3 GetSegmentDir(int segment, ushort nodeid)
        {
            var instance = Singleton<NetManager>.instance;

            Vector3 dir;

            if (instance.m_segments.m_buffer[segment].m_startNode == nodeid)
            {
                dir = instance.m_segments.m_buffer[segment].m_startDirection;
            }
            else
            {
                dir = instance.m_segments.m_buffer[segment].m_endDirection;
            }

            return dir;
        }
    }
}
