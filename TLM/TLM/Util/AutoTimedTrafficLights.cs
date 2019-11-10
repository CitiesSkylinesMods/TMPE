

namespace TrafficManager.Util {
    using TrafficManager.TrafficLight;
    using TrafficManager.Manager.Impl;
    using System.Collections.Generic;
    using ICities;
    using ColossalFramework;
    using UnityEngine;
    //using CitiesGameBridge;
    //using GenericGameBridge.Factory;
    using GenericGameBridge.Service;
    using API.TrafficLight;
    using API.TrafficLight.Data;
    using API.Traffic.Enums;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;



    //using static Constants.ServiceFactory.NetService;


    public static class AutoTimedTrafficLights {
        private static TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
        private static INetService netService = Constants.ServiceFactory.NetService;
        private static CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
        private static JunctionRestrictionsManager junctionRestrictionsManager = JunctionRestrictionsManager.Instance;
        private static IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
        private static IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

        private static ref TrafficLightSimulation Sim(ushort nodeId) => ref tlsMan.TrafficLightSimulations[nodeId];
        private static ref ITimedTrafficLights TimedLight(ushort nodeId) => ref Sim(nodeId).timedLight;


        private static int CountOutgoingLanes(ushort segmentId, ushort nodeId) {
            return Constants.ServiceFactory.NetService.GetSortedLanes(
                                segmentId,
                                ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId],
                                netService.IsStartNode(segmentId, nodeId),
                                NetInfo.LaneType.All,
                                LaneArrowManager.VEHICLE_TYPES,
                                true
                                ).Count;
        }

        private static List<ushort> CWSegments(ushort nodeId) {
            List<ushort> segList = new List<ushort>();
            netService.IterateNodeSegments(
                nodeId,
                ClockDirection.Clockwise,
                (ushort segId, ref NetSegment seg) => {
                    if ( CountOutgoingLanes(segId,nodeId) > 0 ) {
                        segList.Add(segId);
                    }
                    return true;
                });
            return segList;
        }

        enum GreenLane {
            allRed,
            AllGreen,
            rightonly
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
                //Debug.Log($"\n segment={segmentId} node={nodeId} vehicleType={vehicleType} \n VehicleTypes.Count={liveSegmentLights.VehicleTypes.Count}");

                //set light mode to All
                ICustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
                liveSegmentLight.CurrentMode = LightMode.All;
                TimedLight(nodeId).ChangeLightMode(
                    segmentId,
                    vehicleType,
                    liveSegmentLight.CurrentMode);

                // calculate lane counts and directions
                bool bLeft, bRight, bForward;
                ref ExtSegmentEnd segEnd = ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, nodeId)];
                NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
                segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref node, out bLeft, out bForward, out bRight);
                //Debug.Log($"bLeft={bLeft} bRight={bRight} bForward={bForward}");

                // set light states
                var green = RoadBaseAI.TrafficLightState.Green;
                var red = RoadBaseAI.TrafficLightState.Red;
                switch (m) {
                    case GreenLane.AllGreen:
                        liveSegmentLight.SetStates(red, red, red);
                        break;

                    case GreenLane.allRed:
                        liveSegmentLight.SetStates(green, green, green);
                        break;

                    case GreenLane.rightonly when bRight:
                        liveSegmentLight.SetStates(red, red, green);
                        break;

                    case GreenLane.rightonly when bLeft:
                        liveSegmentLight.SetStates(green, red , red); // go forward instead of right
                        break;

                    default:
                        liveSegmentLight.SetStates(green, green, green); // there is only one direction.
                        break;
                } // end switch
            } // end foreach
            step.UpdateLights();
        }

        public static void Setup(ushort nodeId) {
            if (!Add(nodeId)) {
                return;
            }

            var segList = CWSegments(nodeId);
            int n = segList.Count;
            if(n < 3) {
                return; // not a junction
            }

            LaneArrowManager.SeparateTurningLanes.SeparateNode(nodeId, out _);

            for (int i = 0; i < n; ++i) {
                ITimedTrafficLightsStep step = TimedLight(nodeId).AddStep(
                    minTime: 3,
                    maxTime: 8,
                    changeMetric: StepChangeMetric.Default,
                    waitFlowBalance: 0.3f);

                SetupStep(step, nodeId, segList[i], GreenLane.AllGreen);
                SetupStep(step, nodeId, segList[(i + 1) % n], GreenLane.rightonly);
                for (int j = 2; j < n; ++j) {
                    //SetupStep(step, nodeId, segList[(i + j) % n], GreenLane.allRed);
                }
            }

            Sim(nodeId).Housekeeping();
        }





    }
}
