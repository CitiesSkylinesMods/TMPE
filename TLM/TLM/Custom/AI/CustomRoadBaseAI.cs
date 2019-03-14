using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Geometry;
using UnityEngine;
using ColossalFramework.Math;
using System.Threading;
using TrafficManager.UI;
using TrafficManager.State;
using TrafficManager.Manager;
using TrafficManager.UI.SubTools;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using TrafficManager.Geometry.Impl;
using CSUtil.Commons.Benchmark;
using TrafficManager.TrafficLight.Data;
using TrafficManager.RedirectionFramework.Attributes;

namespace TrafficManager.Custom.AI {
	[TargetType(typeof(RoadBaseAI))]
	public class CustomRoadBaseAI : RoadBaseAI {
		[RedirectMethod]
		public void CustomClickNodeButton(ushort nodeID, ref NetNode data, int index) {
			if ((data.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None &&
				Singleton<InfoManager>.instance.CurrentMode == InfoManager.InfoMode.TrafficRoutes &&
				Singleton<InfoManager>.instance.CurrentSubMode == InfoManager.SubInfoMode.WaterPower) {
				if (index == -1) {
					/*data.m_flags ^= NetNode.Flags.TrafficLights;
					data.m_flags |= NetNode.Flags.CustomTrafficLights;*/
					// NON-STOCK CODE START
					ToggleTrafficLightsTool toggleTool = (ToggleTrafficLightsTool)UIBase.GetTrafficManagerTool(true).GetSubTool(ToolMode.SwitchTrafficLight);
					toggleTool.ToggleTrafficLight(nodeID, ref data, false);
					// NON-STOCK CODE END
					this.UpdateNodeFlags(nodeID, ref data);
					Singleton<NetManager>.instance.m_yieldLights.Disable();
				} else if (index >= 1 && index <= 8 && (data.m_flags & (NetNode.Flags.TrafficLights | NetNode.Flags.OneWayIn)) == NetNode.Flags.None) {
					ushort segmentId = data.GetSegment(index - 1);
					if (segmentId != 0) {
						NetManager netManager = Singleton<NetManager>.instance;
						NetInfo info = netManager.m_segments.m_buffer[(int)segmentId].Info;
						if ((info.m_vehicleTypes & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram)) != VehicleInfo.VehicleType.None) {
							bool flag = netManager.m_segments.m_buffer[(int)segmentId].m_startNode == nodeID;
							NetSegment.Flags flags = (!flag) ? NetSegment.Flags.YieldEnd : NetSegment.Flags.YieldStart;
							netManager.m_segments.m_buffer[segmentId].m_flags ^= flags;
							netManager.m_segments.m_buffer[(int)segmentId].UpdateLanes(segmentId, true);
							Singleton<NetManager>.instance.m_yieldLights.Disable();
						}
					}
				}
			}
		}
	}
}
