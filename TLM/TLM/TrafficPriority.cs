using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

namespace TrafficManager
{
    class TrafficSegment
    {
        public ushort node_1 = 0;
        public ushort node_2 = 0;

        public PrioritySegment instance_1;
        public PrioritySegment instance_2;
    }

    class TrafficPriority
    {
        public static bool leftHandDrive = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;

        public enum SegmentDirection
        {
            None = 0,
            Forward = 1,
            Back = 2,
            Left = 4,
            Right = 8,
            ForwardLeft = 5,
            ForwardRight = 9,
            BackLeft = 6,
            BackRight = 10
        }

        public static Dictionary<int, TrafficSegment> prioritySegments = new Dictionary<int, TrafficSegment>();

        public static Dictionary<ushort, PriorityCar> vehicleList = new Dictionary<ushort, PriorityCar>();

        public static void addPrioritySegment(ushort nodeID, int segmentID, PrioritySegment.PriorityType type)
        {
            if (prioritySegments.ContainsKey(segmentID))
            {
                var prioritySegment = prioritySegments[segmentID];

                prioritySegment.node_2 = nodeID;
                prioritySegment.instance_2 = new PrioritySegment(nodeID, segmentID, type);
            }
            else
            {
                prioritySegments.Add(segmentID, new TrafficSegment());
                prioritySegments[segmentID].node_1 = nodeID;
                prioritySegments[segmentID].instance_1 = new PrioritySegment(nodeID, segmentID, type);
            }
        }

        public static void removePrioritySegment(ushort nodeID, int segmentID)
        {
            var prioritySegment = prioritySegments[segmentID];

            if (prioritySegment.node_1 == nodeID)
            {
                prioritySegment.node_1 = 0;
                prioritySegment.instance_1 = null;
            }
            else
            {
                prioritySegment.node_2 = 0;
                prioritySegment.instance_2 = null;
            }

            if (prioritySegment.node_1 == 0 && prioritySegment.node_2 == 0)
            {
                prioritySegments.Remove(segmentID);
            }
        }

        public static bool isPrioritySegment(ushort nodeID, int segmentID)
        {
            if (prioritySegments.ContainsKey(segmentID))
            {
                var prioritySegment = prioritySegments[segmentID];

                if (prioritySegment.node_1 == nodeID || prioritySegment.node_2 == nodeID)
                {
                    return true;
                }
            }

            return false;
        }

        public static PrioritySegment getPrioritySegment(ushort nodeID, int segmentID)
        {
            if (prioritySegments.ContainsKey(segmentID))
            {
                var prioritySegment = prioritySegments[segmentID];

                if (prioritySegment.node_1 == nodeID)
                {
                    return prioritySegment.instance_1;
                }
                if (prioritySegment.node_2 == nodeID)
                {
                    return prioritySegment.instance_2;
                }
            }

            return null;
        }

        public static bool incomingVehicles(ushort targetCar, ushort nodeID)
        {
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            uint frame = currentFrameIndex >> 5;
            var node = TrafficLightTool.GetNetNode(nodeID);

            var fromPrioritySegment = getPrioritySegment(nodeID, vehicleList[targetCar].fromSegment);

            List<ushort> removeCarList = new List<ushort>();

            var numCars = 0;

            // get all cars
            for (int s = 0; s < node.CountSegments(); s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0 && segment != vehicleList[targetCar].fromSegment)
                {
                    if (isPrioritySegment(nodeID, segment))
                    {
                        var prioritySegment = getPrioritySegment(nodeID, segment);

                        // select outdated cars
                        foreach (var car in prioritySegment.cars)
                        {
                            var frameReduce = vehicleList[car].lastSpeed < 70 ? 3u : 2u;

                            if (vehicleList[car].lastFrame < frame - frameReduce)
                            {
                                removeCarList.Add(car);
                            }
                        }

                        // remove outdated cars
                        foreach (var rcar in removeCarList)
                        {
                            vehicleList[rcar].resetCar();
                            prioritySegment.RemoveCar(rcar);
                        }

                        removeCarList.Clear();

                        if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None)
                        {
                            if (fromPrioritySegment.type == PrioritySegment.PriorityType.Main)
                            {
                                if (prioritySegment.type == PrioritySegment.PriorityType.Main)
                                {
                                    numCars += prioritySegment.numCars;

                                    foreach (var car in prioritySegment.cars)
                                    {
                                        if (vehicleList[car].lastSpeed > 0.1f)
                                        {
                                            numCars = checkSameRoadIncomingCar(targetCar, car, nodeID)
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
                                numCars += prioritySegment.numCars;

                                foreach (var car in prioritySegment.cars)
                                {
                                    if (prioritySegment.type == PrioritySegment.PriorityType.Main)
                                    {
                                        if (!vehicleList[car].stopped)
                                        {
                                            numCars = checkPriorityRoadIncomingCar(targetCar, car, nodeID)
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
                                        if (vehicleList[car].lastSpeed > 0.1f)
                                        {
                                            numCars = checkSameRoadIncomingCar(targetCar, car, nodeID)
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
                            if (TrafficLightsManual.IsSegmentLight(nodeID, segment))
                            {
                                var segmentLight = TrafficLightsManual.GetSegmentLight(nodeID, segment);

                                if (segmentLight.GetLightMain() == RoadBaseAI.TrafficLightState.Green)
                                {
                                    numCars += prioritySegment.numCars;

                                    foreach (var car in prioritySegment.cars)
                                    {
                                        if (vehicleList[car].lastSpeed > 1f)
                                        {
                                            numCars = checkSameRoadIncomingCar(targetCar, car, nodeID)
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

        public static bool checkSameRoadIncomingCar(ushort targetCarID, ushort incomingCarID, ushort nodeID)
        {
            if (leftHandDrive)
            {
                return _checkSameRoadIncomingCarLeftHandDrive(targetCarID, incomingCarID, nodeID);
            }
            else
            {
                return _checkSameRoadIncomingCarRightHandDrive(targetCarID, incomingCarID, nodeID);
            }
        }

        protected static bool _checkSameRoadIncomingCarLeftHandDrive(ushort targetCarID, ushort incomingCarID,
            ushort nodeID)
        {
            var targetCar = vehicleList[targetCarID];
            var incomingCar = vehicleList[incomingCarID];

            if (isRightSegment(targetCar.fromSegment, incomingCar.fromSegment, nodeID, true) >= 0)
            {
                if (isRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0)
                {
                    if (isLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0)
                    {
                        return true;
                    }
                }
                else if (targetCar.toSegment == incomingCar.toSegment && targetCar.toLaneID != incomingCar.toLaneID)
                {
                    return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                }
            }
            else if (isLeftSegment(targetCar.fromSegment, incomingCar.fromSegment, nodeID, true) >= 0)
                // incoming is on the left
            {
                return true;
            }
            else // incoming is in front or elsewhere
            {
                if (isRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) < 0)
                    // target car not going left
                {
                    return true;
                }
                else if (isLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0)
                {
                    if (targetCar.toLaneID != incomingCar.toLaneID)
                    {
                        return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                }
                else if (isLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0 && isLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0) // both left turns
                {
                    return true;
                }
            }

            return false;
        }

        protected static bool _checkSameRoadIncomingCarRightHandDrive(ushort targetCarID, ushort incomingCarID,
            ushort nodeID)
        {
            var targetCar = vehicleList[targetCarID];
            var incomingCar = vehicleList[incomingCarID];

            if (isRightSegment(targetCar.fromSegment, incomingCar.fromSegment, nodeID, true) >= 0)
            {
                if (isRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0)
                {
                    return true;
                }
                else if (targetCar.toSegment == incomingCar.toSegment && targetCar.toLaneID != incomingCar.toLaneID)
                {
                    return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                }
            }
            else if (isLeftSegment(targetCar.fromSegment, incomingCar.fromSegment, nodeID, true) >= 0)
                // incoming is on the left
            {
                return true;
            }
            else // incoming is in front or elsewhere
            {
                if (isLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) < 0)
                    // target car not going left
                {
                    return true;
                }
                else if (isRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0)
                {
                    if (targetCar.toLaneID != incomingCar.toLaneID)
                    {
                        return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                }
                else if (isLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0 && isLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0) // both left turns
                {
                    return true;
                }
            }

            return false;
        }


        public static bool checkPriorityRoadIncomingCar(ushort targetCarID, ushort incomingCarID, ushort nodeID)
        {
            if (leftHandDrive)
            {
                return _checkPriorityRoadIncomingCarLeftHandDrive(targetCarID, incomingCarID, nodeID);
            }
            else
            {
                return _checkPriorityRoadIncomingCarRightHandDrive(targetCarID, incomingCarID, nodeID);
            }
        }

        protected static bool _checkPriorityRoadIncomingCarLeftHandDrive(ushort targetCarID, ushort incomingCarID,
            ushort nodeID)
        {
            var targetCar = vehicleList[targetCarID];
            var incomingCar = vehicleList[incomingCarID];

            if (incomingCar.toSegment == targetCar.toSegment)
            {
                if (incomingCar.toLaneID != targetCar.toLaneID)
                {
                    if (isRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0)
                    // target car goes right
                    {
                        // go if incoming car is in the left lane
                        return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                    else if (isLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0)
                    // target car goes left
                    {
                        // go if incoming car is in the right lane
                        return laneOrderCorrect(targetCar.toSegment, incomingCar.toLaneID, targetCar.toLaneID);
                    }
                    else // target car goes straight (or other road)
                    {
                        if (isRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0) // incoming car goes right
                        {
                            // go if incoming car is in the left lane
                            return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, targetCar.toLaneID);
                        }
                        else if (isLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0) // incoming car goes left
                        {
                            // go if incoming car is in the right lane
                            return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID,
                                incomingCar.toLaneID);
                        }
                    }
                }
            }
            else if (incomingCar.toSegment == targetCar.fromSegment)
            {
                if (isLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0)
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
                if (isLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0)
                {
                    return true;
                }
                else if (isLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0) // incoming car turning right
                {
                    return true;
                }
            }

            return false;
        }

        protected static bool _checkPriorityRoadIncomingCarRightHandDrive(ushort targetCarID, ushort incomingCarID,
            ushort nodeID)
        {
            var targetCar = vehicleList[targetCarID];
            var incomingCar = vehicleList[incomingCarID];

            if (incomingCar.toSegment == targetCar.toSegment)
            {
                if (incomingCar.toLaneID != targetCar.toLaneID)
                {
                    if (isRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0)
                    // target car goes right
                    {
                        // go if incoming car is in the left lane
                        return laneOrderCorrect(targetCar.toSegment, incomingCar.toLaneID, targetCar.toLaneID);
                    }
                    else if (isLeftSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0)
                    // target car goes left
                    {
                        // go if incoming car is in the right lane
                        return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                    }
                    else // target car goes straight (or other road)
                    {
                        if (isRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0) // incoming car goes right
                        {
                            // go if incoming car is in the left lane
                            return laneOrderCorrect(targetCar.toSegment, targetCar.toLaneID, incomingCar.toLaneID);
                        }
                        else if (isLeftSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0) // incoming car goes left
                        {
                            // go if incoming car is in the right lane
                            return laneOrderCorrect(targetCar.toSegment, incomingCar.toLaneID,
                                targetCar.toLaneID);
                        }
                    }
                }
            }
            else if (incomingCar.toSegment == targetCar.fromSegment)
            {
                if (isRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0)
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
                if (isRightSegment(targetCar.fromSegment, targetCar.toSegment, nodeID, true) >= 0)
                {
                    return true;
                }
                else if (isRightSegment(incomingCar.fromSegment, incomingCar.toSegment, nodeID, true) >= 0) // incoming car turning right
                {
                    return true;
                }
            }

            return false;
        }

        public static bool laneOrderCorrect(int segmentid, uint leftLane, uint rightLane)
        {
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[segmentid];
            var info = segment.Info;

            uint num2 = segment.m_lanes;
            int num3 = 0;

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

        public static int isRightSegment(int segmentSource, int rightSegment, ushort nodeid, bool recursionCheck)
        {
            var dir = GetSegmentDirection(segmentSource, nodeid);
            var dir2 = GetSegmentDirection(rightSegment, nodeid);
            var node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeid];
            var numSgmnts = node.CountSegments();

            var dirNum = -1;

            if (dir == SegmentDirection.Forward)
            {
                if (dir2 == SegmentDirection.ForwardLeft)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.Left)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.BackLeft)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.ForwardLeft)
            {
                if (dir2 == SegmentDirection.Left)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.BackLeft)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.Back)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.Left)
            {
                if (dir2 == SegmentDirection.BackLeft)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.Back)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.BackRight)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.BackLeft)
            {
                if (dir2 == SegmentDirection.Back)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.BackRight)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.Right)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.Back)
            {
                if (dir2 == SegmentDirection.BackRight)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.Right)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.ForwardRight)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.BackRight)
            {
                if (dir2 == SegmentDirection.Right)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.ForwardRight)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.Forward)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.Right)
            {
                if (dir2 == SegmentDirection.ForwardRight)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.Forward)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.ForwardLeft)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.ForwardRight)
            {
                if (dir2 == SegmentDirection.Forward)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.ForwardLeft)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.Left)
                {
                    dirNum = 3;
                }
            }

            if (dirNum >= 0 && recursionCheck)
            {
                for (var i = 0; i < numSgmnts; i++)
                {
                    var segment = node.GetSegment(i);

                    if (segment != 0 && segment != segmentSource && segment != rightSegment)
                    {
                        var right = isRightSegment(segmentSource, segment, nodeid, false);

                        if (right < dirNum && right >= 0)
                        {
                            dirNum = -1;
                            break;
                        }
                    }
                }
            }

            return dirNum;
        }

        public static int isLeftSegment(int segmentSource, int leftSegment, ushort nodeid, bool recursionCheck)
        {
            var dir = GetSegmentDirection(segmentSource, nodeid);
            var dir2 = GetSegmentDirection(leftSegment, nodeid);
            var node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeid];
            var numSgmnts = node.CountSegments();

            var dirNum = -1;

            if (dir == SegmentDirection.Forward)
            {
                if (dir2 == SegmentDirection.ForwardRight)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.Right)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.BackRight)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.ForwardRight)
            {
                if (dir2 == SegmentDirection.Right)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.BackRight)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.Back)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.Right)
            {
                if (dir2 == SegmentDirection.BackRight)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.Back)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.BackLeft)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.BackRight)
            {
                if (dir2 == SegmentDirection.Back)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.BackLeft)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.Left)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.Back)
            {
                if (dir2 == SegmentDirection.BackLeft)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.Left)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.ForwardLeft)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.BackLeft)
            {
                if (dir2 == SegmentDirection.Left)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.ForwardLeft)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.Forward)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.Left)
            {
                if (dir2 == SegmentDirection.ForwardLeft)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.Forward)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.ForwardRight)
                {
                    dirNum = 3;
                }
            }
            else if (dir == SegmentDirection.ForwardLeft)
            {
                if (dir2 == SegmentDirection.Forward)
                {
                    dirNum = 1;
                }
                else if (dir2 == SegmentDirection.ForwardRight)
                {
                    dirNum = 2;
                }
                else if (dir2 == SegmentDirection.Right)
                {
                    dirNum = 3;
                }
            }

            if (dirNum >= 0 && recursionCheck)
            {
                for (var i = 0; i < numSgmnts; i++)
                {
                    var segment = node.GetSegment(i);

                    if (segment != 0 && segment != segmentSource && segment != leftSegment)
                    {
                        var left = isLeftSegment(segmentSource, segment, nodeid, false);

                        if (left < dirNum && left >= 0)
                        {
                            dirNum = -1;
                            break;
                        }
                    }
                }
            }

            return dirNum;
        }

        public static bool HasLeftSegment(int segmentID, ushort nodeID, bool debug)
        {
            var node = TrafficLightTool.GetNetNode(nodeID);

            for (int s = 0; s < node.CountSegments(); s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0 && segment != segmentID)
                {
                    if (isLeftSegment(segmentID, segment, nodeID, true) >= 0)
                    {
                        if (debug)
                        {
                            Debug.Log("LEFT: " + segment + " " + GetSegmentDirection(segment, nodeID) + " " + GetSegmentDir(segment, nodeID));
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasRightSegment(int segmentID, ushort nodeID, bool debug)
        {
            var node = TrafficLightTool.GetNetNode(nodeID);

            for (int s = 0; s < node.CountSegments(); s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0 && segment != segmentID)
                {
                    if (isRightSegment(segmentID, segment, nodeID, true) >= 0)
                    {
                        if (debug)
                        {
                            Debug.Log("RIGHT: " + segment + " " + GetSegmentDirection(segment, nodeID) + " " + GetSegmentDir(segment, nodeID));
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasForwardSegment(int segmentID, ushort nodeID, bool debug)
        {
            var node = TrafficLightTool.GetNetNode(nodeID);

            if (node.CountSegments() <= 3)
            {
                var hasLeftSegment = false;
                var hasRightSegment = false;

                for (int s = 0; s < node.CountSegments(); s++)
                {
                    var segment = node.GetSegment(s);

                    if (segment != 0 && segment != segmentID)
                    {
                        if (isRightSegment(segmentID, segment, nodeID, true) >= 0)
                        {
                            if (debug)
                            {
                                Debug.Log("FORWARD RIGHT: " + segment + " " + GetSegmentDirection(segment, nodeID) + " " + GetSegmentDir(segment, nodeID));
                            }

                            hasRightSegment = true;
                        }

                        if (isLeftSegment(segmentID, segment, nodeID, true) >= 0)
                        {
                            if (debug)
                            {
                                Debug.Log("FORWARDLEFT: " + segment + " " + GetSegmentDirection(segment, nodeID) + " " + GetSegmentDir(segment, nodeID));
                            }
                            hasLeftSegment = true;
                        }
                    }
                }

                if (hasLeftSegment && hasRightSegment)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool hasLeftLane(ushort nodeID, int segmentID)
        {
            var instance = Singleton<NetManager>.instance;
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentID];
            NetInfo.Direction dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == nodeID)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = TrafficPriority.leftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var info = segment.Info;

            var maxValue = 0f;

            var num2 = segment.m_lanes;
            var num3 = 0;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[(int)num2].m_flags;

                if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.None)
                {
                    return true;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            return false;
        }

        public static bool hasForwardLane(ushort nodeID, int segmentID)
        {
            var instance = Singleton<NetManager>.instance;
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentID];
            NetInfo.Direction dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == nodeID)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = TrafficPriority.leftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var info = segment.Info;

            var maxValue = 0f;

            var num2 = segment.m_lanes;
            var num3 = 0;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[(int)num2].m_flags;

                if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.Forward)
                {
                    return true;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            return false;
        }

        public static bool hasRightLane(ushort nodeID, int segmentID)
        {
            var instance = Singleton<NetManager>.instance;
            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentID];
            NetInfo.Direction dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == nodeID)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = TrafficPriority.leftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var info = segment.Info;

            var maxValue = 0f;

            var num2 = segment.m_lanes;
            var num3 = 0;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[(int)num2].m_flags;

                if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.Right)
                {
                    return true;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            return false;
        }

        public static SegmentDirection GetSegmentDirection(int segment, ushort nodeid)
        {
            NetManager instance = Singleton<NetManager>.instance;

            Vector3 dir;

            if (instance.m_segments.m_buffer[(int)segment].m_startNode == nodeid)
            {
                dir = instance.m_segments.m_buffer[(int)segment].m_startDirection;
            }
            else
            {
                dir = instance.m_segments.m_buffer[(int)segment].m_endDirection;
            }

            var direction = SegmentDirection.None;

            if (dir.z > 0.5f)
                direction |= SegmentDirection.Forward;

            if (dir.z < -0.5f)
                direction |= SegmentDirection.Back;

            if (dir.x > 0.5f)
                direction |= SegmentDirection.Right;

            if (dir.x < -0.5f)
                direction |= SegmentDirection.Left;

            return direction;
        }

        public static Vector3 GetSegmentDir(int segment, ushort nodeid)
        {
            NetManager instance = Singleton<NetManager>.instance;

            Vector3 dir;

            if (instance.m_segments.m_buffer[(int)segment].m_startNode == nodeid)
            {
                dir = instance.m_segments.m_buffer[(int)segment].m_startDirection;
            }
            else
            {
                dir = instance.m_segments.m_buffer[(int)segment].m_endDirection;
            }

            return dir;
        }
    }

    class PriorityCar
    {
        public enum CarState
        {
            None,
            Enter,
            Transit,
            Stop,
            Leave
        }

        public CarState carState = CarState.None;

        public int waitTime = 0;

        public ushort toNode;
        public int fromSegment;
        public int toSegment;
        public uint toLaneID;
        public uint fromLaneID;
        public ushort fromLaneFlags;
        public float lastSpeed;
        public float yieldSpeedReduce;
        public bool stopped = false;

        public uint lastFrame;

        public void resetCar()
        {
            toNode = 0;
            fromSegment = 0;
            toSegment = 0;
            toLaneID = 0;
            fromLaneID = 0;
            fromLaneFlags = 0;
            stopped = false;

            waitTime = 0;
            carState = CarState.None;
        }
    }

    public class PrioritySegment
    {
        public enum PriorityType
        {
            None = 0,
            Main = 1,
            Stop = 2,
            Yield = 3
        }

        public ushort nodeid;
        public int segmentid;


        public PriorityType type = PriorityType.Main;

        public int numCars = 0;

        public List<ushort> cars = new List<ushort>(); 

        public int[] carsOnLanes = new int[24]; 

        public PrioritySegment(ushort nodeid, int segmentid, PriorityType type)
        {
            this.nodeid = nodeid;
            this.segmentid = segmentid;
            this.type = type;
        }

        public void AddCar(ushort vehicleID)
        {
            if (!cars.Contains(vehicleID))
            {
                cars.Add(vehicleID);
                numCars++;
                _numCarsOnLane();
            }
        }

        public void RemoveCar(ushort vehicleID)
        {
            if (cars.Contains(vehicleID))
            {
                cars.Remove(vehicleID);
                numCars--;
                _numCarsOnLane();
            }
        }

        public bool HasCar(ushort vehicleID)
        {
            return cars.Contains(vehicleID);
        }

        public int getCarsOnLane(int lane)
        {
            return carsOnLanes[lane];
        }

        private void _numCarsOnLane()
        {
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[this.segmentid];
            var info = segment.Info;

            uint num2 = segment.m_lanes;
            int num3 = 0;

            carsOnLanes = new int[16];

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian) {
                    for (var i = 0; i < cars.Count; i++)
                    {
                        if (TrafficPriority.vehicleList[cars[i]].fromLaneID == num2)
                        {
                            carsOnLanes[num3]++;
                        }
                    }
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }
        }
    }
}
