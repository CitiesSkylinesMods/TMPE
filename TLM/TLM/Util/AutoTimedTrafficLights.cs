

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


        private static List<ushort> CWSegments(ushort nodeId) {
            List<ushort> segList = new List<ushort>();
            netService.IterateNodeSegments(
                nodeId,
                ClockDirection.Clockwise,
                (ushort segId, ref NetSegment seg) => {
                    segList.Add(segId);
                    return true;
                });
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

        enum GreenLane {
            allRed,
            AllGreen,
            rightonly
        }

        private static void Config(ITimedTrafficLightsStep step, ushort nodeId, ushort segmentId, GreenLane m) {
            bool startNode = (bool)netService.IsStartNode(segmentId, nodeId);

            //get step data for side seg
            ICustomSegmentLights liveSegmentLights = customTrafficLightsManager.GetSegmentLights(segmentId, startNode);

            //get vehicle types
            //ExtVehicleType vehicleType = liveSegmentLights.VehicleTypes.First.Value;
            //Debug.Log($"\n segment={segmentId} node={nodeId} vehicleType={vehicleType} \n VehicleTypes.Count={liveSegmentLights.VehicleTypes.Count}");
            ExtVehicleType vehicleType = ExtVehicleType.RoadVehicle;

            //set light mode to single right
            ICustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
            liveSegmentLight.CurrentMode = LightMode.SingleRight;
            TimedLight(nodeId).ChangeLightMode(
                segmentId,
                vehicleType,
                liveSegmentLight.CurrentMode);

            // set light states
            var green = RoadBaseAI.TrafficLightState.Green;
            var red = RoadBaseAI.TrafficLightState.Red;
            if (m == GreenLane.AllGreen) liveSegmentLight.SetStates(green, green, green);
            if (m == GreenLane.allRed) liveSegmentLight.SetStates(red,red,red);
            if (m == GreenLane.rightonly) liveSegmentLight.SetStates(red,red, green);
            step.UpdateLights();
        }

        public static bool Setup(ushort nodeId) {
            if (!Add(nodeId)) {
                return false;
            }

            var segList = CWSegments(nodeId);
            int n = segList.Count;

            for (int i = 0; i < n; ++i) {
                ITimedTrafficLightsStep step = TimedLight(nodeId).AddStep(
                    minTime: 3,
                    maxTime: 8,
                    changeMetric: StepChangeMetric.Default,
                    waitFlowBalance: 0.3f);

                Config(step, nodeId, segList[i], GreenLane.AllGreen);
                Config(step, nodeId, segList[(i + 1) % n], GreenLane.rightonly);
                Config(step, nodeId, segList[(i + 2) % n], GreenLane.allRed);
                Config(step, nodeId, segList[(i + 3) % n], GreenLane.allRed);
            }

            Sim(nodeId).Housekeeping();
            return true;
        }





    }
}
