

namespace TrafficManager.Util {
    using TrafficManager.TrafficLight;
    using TrafficManager.Manager.Impl;
    using System.Collections.Generic;
    using ICities;
    using ColossalFramework;
    using UnityEngine;
    using GenericGameBridge.Service;
    using API.TrafficLight;
    using API.TrafficLight.Data;
    using API.Traffic.Enums;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using CSUtil.Commons;

    public static class AutoTimedTrafficLights {
        private static readonly bool NoCollidingRightTurns = true;

        private static TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
        private static INetService netService = Constants.ServiceFactory.NetService;
        private static CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
        private static JunctionRestrictionsManager junctionRestrictionsManager = JunctionRestrictionsManager.Instance;
        private static IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
        private static IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

        private enum GreenLane {
            AllRed,
            AllGreen,
            RightOnly
        }
        public enum ErrorResult {
            Success = 0,
            NoJunction,
            NoNeed,
            TTLExists,
            Other,
        }


        private static List<ushort> CWSegments(ushort nodeId) {
            List<ushort> segList = new List<ushort>();
            netService.IterateNodeSegments(
                nodeId,
                ClockDirection.Clockwise,
                (ushort segId, ref NetSegment seg) => {
                    if (CountOutgoingLanes(segId, nodeId) > 0) {
                        segList.Add(segId);
                    }
                    return true;
                });
;
            return segList;
        }

        public static bool Add(List<ushort> nodes) {
            bool ret = true;
            foreach(ushort nodeId in nodes ) {
                ret &= tlsMan.SetUpTimedTrafficLight(nodeId, nodes);
                if(!ret) {
                    break;
                }
            }
            return ret;
        }

        public static bool Add(ushort nodeId) {
            List<ushort> nodeGroup = new List<ushort>(1);
            nodeGroup.Add(nodeId);
            return tlsMan.SetUpTimedTrafficLight(nodeId, nodeGroup);
        }

        private static void SetupStep(ITimedTrafficLightsStep step, ushort nodeId, ushort segmentId, GreenLane m) {
            bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);

            //get step data for side seg
            ICustomSegmentLights liveSegmentLights = customTrafficLightsManager.GetSegmentLights(segmentId, startNode);

            //for each lane type
            foreach (ExtVehicleType vehicleType in liveSegmentLights.VehicleTypes) {
                //set light mode
                ICustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
                liveSegmentLight.CurrentMode = LightMode.All;
                //TODO do this only for the first step.
                TimedLight(nodeId).ChangeLightMode(
                    segmentId,
                    vehicleType,
                    liveSegmentLight.CurrentMode);

                // calculate directions
                bool bLeft, bRight, bForward;
                ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
                ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
                segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);
                //Debug.Log($"\n segment={segmentId} node={nodeId} vehicleType={vehicleType} \n VehicleTypes.Count={liveSegmentLights.VehicleTypes.Count}");
                //Debug.Log($"bLeft={bLeft} bRight={bRight} bForward={bForward}");

                // set light states
                var green = RoadBaseAI.TrafficLightState.Green;
                var red = RoadBaseAI.TrafficLightState.Red;
                switch (m) {
                    case GreenLane.AllRed:
                        liveSegmentLight.SetStates(red, red, red);
                        break;

                    case GreenLane.AllGreen:
                        liveSegmentLight.SetStates(green, green, green);
                        break;

                    case GreenLane.RightOnly when bRight:
                        liveSegmentLight.SetStates(red, red, green);
                        break;

                    case GreenLane.RightOnly when bLeft:
                        // go forward instead of right
                        liveSegmentLight.SetStates(green, red , red);
                        break;

                    default:
                        Debug.LogAssertion("Unreachable code.");
                        liveSegmentLight.SetStates(green, green, green);
                        break;
                } // end switch
            } // end foreach
            step.UpdateLights(); //save
        }



        public static ErrorResult Setup(ushort nodeId) {
            if(tlsMan.HasTimedSimulation(nodeId)) {
                return ErrorResult.TTLExists;
            }

            var segList = CWSegments(nodeId);
            int n = segList.Count;

            if (n < 3) {
                return ErrorResult.NoNeed;
            }

            if (!Add(nodeId)) {
                return ErrorResult.Other;
            }

            LaneArrowManager.SeparateTurningLanes.SeparateNode(nodeId, out _);

            {
                var segList2Way = TwoWayRoads(segList, out int n2);
                Debug.Log($"n2 == {n2}");
                if (n2 < 2) {
                    return ErrorResult.NoNeed;
                }
                bool b = HasIncommingOneWaySegment(nodeId);
                Debug.Log($"HasIncommingOneWaySegment == {b}");
                if (n2 == 2 && !b) {
                    Debug.Log($"special case");
                    return SetupSpecial(nodeId, segList2Way);
                }
            }

            Debug.Log("normal case");

            for (int i = 0; i < n; ++i) {
                ITimedTrafficLightsStep step = TimedLight(nodeId).AddStep(
                    minTime: 3,
                    maxTime: 8,
                    changeMetric: StepChangeMetric.Default,
                    waitFlowBalance: 0.3f,
                    makeRed:true);

                SetupStep(step, nodeId, segList[i], GreenLane.AllGreen);

                ushort rightsegmentId = segList[(i + 1) % n];
                if ( NeedsRightOnly(rightsegmentId, nodeId)) {
                    SetupStep(step, nodeId, rightsegmentId, GreenLane.RightOnly);
                } else {
                    SetupStep(step, nodeId, rightsegmentId, GreenLane.AllRed);
                }
                for (int j = 2; j < n; ++j) {
                    SetupStep(step, nodeId, segList[(i + j) % n], GreenLane.AllRed);
                }
            }

            Sim(nodeId).Housekeeping();

            return ErrorResult.Success;
        }

        private static ErrorResult SetupSpecial(ushort nodeId, List<ushort> segList2Way) {
            /* speical case where:
             * multiple outgoing one way roads. only two 2way roads.
             *  - each 1-way road gets a go
             *  - then the two 2-way roads get a go.
             * this way we can save one step.
             */

            var segList1Way = OneWayRoads(nodeId, out var n1);

            // the two 2-way roads get a go.
            {
                ITimedTrafficLightsStep step = TimedLight(nodeId).AddStep(
                    minTime: 3,
                    maxTime: 8,
                    changeMetric: StepChangeMetric.Default,
                    waitFlowBalance: 0.3f,
                    makeRed: true);

                SetupStep(step, nodeId, segList2Way[0], GreenLane.AllGreen);
                SetupStep(step, nodeId, segList2Way[1], GreenLane.AllGreen);
                foreach (var segId in segList1Way) {
                    SetupStep(step, nodeId, segId, GreenLane.AllRed);
                }
            }

            //each 1-way road gets a go
            for (int i = 0; i < n1; ++i) {
                ITimedTrafficLightsStep step = TimedLight(nodeId).AddStep(
                    minTime: 3,
                    maxTime: 8,
                    changeMetric: StepChangeMetric.Default,
                    waitFlowBalance: 0.3f,
                    makeRed: true);

                SetupStep(step, nodeId, segList1Way[i], GreenLane.AllGreen);
                for (int j = 1; j < n1; ++j) {
                    SetupStep(step, nodeId, segList1Way[(i + j) % n1], GreenLane.AllRed);
                }
                foreach (var segId in segList2Way) {
                    SetupStep(step, nodeId, segId, GreenLane.AllRed);
                }
            }

            Sim(nodeId).Housekeeping();
            return ErrorResult.Success;
        }

        private static bool HasIncommingOneWaySegment(ushort nodeId) {
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
            for (int i = 0; i < 8; ++i) {
                var segId = node.GetSegment(i);
                if (segId != 0 && segMan.CalculateIsOneWay(segId)) {
                    int n = CountIncomingLanes(segId, nodeId);
                    int dummy = CountOutgoingLanes(segId, nodeId);
                    Debug.Log($"seg={segId} node={nodeId} CountIncomingLanes={n} CountOutgoingLanes={dummy}");
                    if (n > 0) {
                        return true;
                    }
                }
            }
            return false;
        }

        private static List<ushort> TwoWayRoads(List<ushort> segList, out int count) {
            List<ushort> segList2 = new List<ushort>();
            foreach(var segId in segList) {
                if (!segMan.CalculateIsOneWay(segId)) {
                    segList2.Add(segId);
                }
            }
            count = segList2.Count;
            return segList2;
        }

        private static List<ushort> OneWayRoads(ushort nodeId, out int count) {
            List<ushort> segList2 = new List<ushort>();
            ref NetNode node = ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
            for (int i = 0; i < 8; ++i) {
                var segId = node.GetSegment(i);
                if (segMan.CalculateIsOneWay(segId)) {
                    segList2.Add(segId);
                }
            }
            count = segList2.Count;
            return segList2;
        }

        private static bool NeedsRightOnly(ushort segmentId, ushort nodeId) {
            //These are the cases where right turn is possible only if we use the lane connection tool.
            if (!NoCollidingRightTurns) {
                return true;
            }
            ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            int nRight = CountDirSegs(segmentId, nodeId, ArrowDirection.Right);

            if (nRight > 1) {
                return false;
            }
            if (nRight == 1) {
                ushort rightSegmentId = seg.GetRightSegment(nodeId);
                return !segMan.CalculateIsOneWay(rightSegmentId);
            }
            int nForward = CountDirSegs(segmentId, nodeId, ArrowDirection.Forward);
            if (nForward > 1) {
                return false;
            }
            if (nForward == 1) {
                // if there are not segments to the right GetRightSegment() returns the forward segment.
                ushort rightSegmentId = seg.GetRightSegment(nodeId);
                return !segMan.CalculateIsOneWay(rightSegmentId);
            }
            return false;
        }


        private static ref TrafficLightSimulation Sim(ushort nodeId) => ref tlsMan.TrafficLightSimulations[nodeId];
        private static ref ITimedTrafficLights TimedLight(ushort nodeId) => ref Sim(nodeId).timedLight;

        private static int CountLanes(ushort segmentId, ushort nodeId, bool outgoing = true) {
            return Constants.ServiceFactory.NetService.GetSortedLanes(
                                segmentId,
                                ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId],
                                netService.IsStartNode(segmentId, nodeId) ^ (!outgoing),
                                LaneArrowManager.LANE_TYPES,
                                LaneArrowManager.VEHICLE_TYPES,
                                true
                                ).Count;
        }
        private static int CountOutgoingLanes(ushort segmentId, ushort nodeId) => CountLanes(segmentId, nodeId, true);
        private static int CountIncomingLanes(ushort segmentId, ushort nodeId) => CountLanes(segmentId, nodeId, false);


        private static int CountDirSegs(ushort segmentId, ushort nodeId, ArrowDirection dir) {
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
            int ret = 0;

            netService.IterateNodeSegments(
                nodeId,
                (ushort segId, ref NetSegment seg) => {
                    if (segEndMan.GetDirection(ref segEnd, segId) == dir) {
                        ret++;
                    }
                    return true;
                });
            return ret;
        }





    }
}
