namespace TrafficManager.Custom.AI {
    using ColossalFramework;
    using RedirectionFramework.Attributes;
    using UI;
    using UI.SubTools;

    [TargetType(typeof(RoadBaseAI))]
    public class CustomRoadBaseAI : RoadBaseAI {
        [RedirectMethod]
        public void CustomClickNodeButton(ushort nodeId, ref NetNode data, int index) {
            if ((data.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None ||
                Singleton<InfoManager>.instance.CurrentMode != InfoManager.InfoMode.TrafficRoutes ||
                Singleton<InfoManager>.instance.CurrentSubMode != InfoManager.SubInfoMode.WaterPower) {
                return;
            }

            if (index == -1) {
                /*data.m_flags ^= NetNode.Flags.TrafficLights;
                    data.m_flags |= NetNode.Flags.CustomTrafficLights;*/

                // NON-STOCK CODE START
                var toggleTool = (ToggleTrafficLightsTool)UIBase
                                                          .GetTrafficManagerTool(true)
                                                          .GetSubTool(ToolMode.SwitchTrafficLight);
                toggleTool.ToggleTrafficLight(nodeId, ref data, false);

                // NON-STOCK CODE END
                UpdateNodeFlags(nodeId, ref data);
                Singleton<NetManager>.instance.m_yieldLights.Disable();
            } else if (index >= 1
                       && index <= 8
                       && (data.m_flags & (NetNode.Flags.TrafficLights
                                           | NetNode.Flags.OneWayIn)) == NetNode.Flags.None) {
                var segmentId = data.GetSegment(index - 1);
                if (segmentId == 0) {
                    return;
                }

                var netManager = Singleton<NetManager>.instance;
                var info = netManager.m_segments.m_buffer[segmentId].Info;
                if ((info.m_vehicleTypes & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram)) ==
                    VehicleInfo.VehicleType.None) {
                    return;
                }

                var flag = netManager.m_segments.m_buffer[segmentId].m_startNode == nodeId;
                var flags = (!flag) ? NetSegment.Flags.YieldEnd : NetSegment.Flags.YieldStart;
                netManager.m_segments.m_buffer[segmentId].m_flags ^= flags;
                netManager.m_segments.m_buffer[segmentId].UpdateLanes(segmentId, true);
                Singleton<NetManager>.instance.m_yieldLights.Disable();
            }
        }
    }
}