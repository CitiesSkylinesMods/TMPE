using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.Manager.Impl {
    internal class JunctionDataExportService {
        public static readonly JunctionDataExportService Instance = new JunctionDataExportService();
        public ushort SelectedNodeId {
            get; set;
        }

        public string GenerateStepData() {
            if (SelectedNodeId == 0) return null;

            NodeGeometry nodeGeometry = NodeGeometry.Get(SelectedNodeId);

            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
            List<List<ICustomSegmentLight>> lineLights = new List<List<ICustomSegmentLight>>();
            foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
                if (end == null)
                    continue;
                ICustomSegmentLights segmentLights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode, false);
                if (segmentLights == null)
                    continue;

                NetInfo.Lane[] lanes;
                string allLines = "Node id: " + end.SegmentId + " \n ";
                end.ConnectedSegments.ToList().ForEach(segmentId => {
                    if (segmentId != 0) {
                        lanes = NetManager.instance.m_segments.m_buffer[segmentId].Info.m_lanes;
                        uint laneId = NetManager.instance.m_segments.m_buffer[segmentId].m_lanes;
                        string laneIds = "Segment " + segmentId + " lanes: \n";
                        laneIds = lanes.Aggregate(laneIds, (current, lane) => current + (" id: " + laneId + " s_limit: " + lane.m_speedLimit + "t: " + lane.m_vehicleType.ToString() + " " + (int)lane.m_vehicleType + "\n"));
                        allLines += laneIds + " \n ";
                    }
                });
                Debug.Log(allLines);
                List<ICustomSegmentLight> list = new List<ICustomSegmentLight>();
                foreach (ExtVehicleType vehicleType in segmentLights.VehicleTypes) {
                    ICustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
                    list.Add(segmentLight);
                }
                lineLights.Add(list);
            }
            return GenerateString(lineLights);
        }

        public string ExportJunctionSegmentsInfo() {
            if (SelectedNodeId == 0) return null;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Node id: " + SelectedNodeId);

            NetManager netManager = Singleton<NetManager>.instance;
            NetNode junctionNode = netManager.m_nodes.m_buffer[SelectedNodeId];

            byte connectedSegments = (byte)junctionNode.CountSegments();
            stringBuilder.AppendLine("Connected segments " + connectedSegments);
            ushort[] segmentIds = new ushort[connectedSegments];

            for (byte i = 0; i < connectedSegments; i++) {
                segmentIds[i] = junctionNode.GetSegment(i);
                stringBuilder.AppendLine("segment id: " + junctionNode.GetSegment(i));
            }

            for (byte i = 0; i < segmentIds.Length; i++) {
                ushort segmentId = segmentIds[i];
                uint laneId = netManager.m_segments.m_buffer[segmentId].m_lanes;

                while (laneId != 0) {
                    NetLane netLane = netManager.m_lanes.m_buffer[laneId];
                    //NetLane.Flags
                    stringBuilder
                        .AppendLine(" laneID:        " + laneId)
                        .AppendLine(" nextLine:      " + netLane.m_nextLane)
                        .AppendLine(" m_segment:     " + netLane.m_segment)
                        .AppendLine(" m_nodes:       " + netLane.m_nodes)
                        .AppendLine(" flags:         " + ((NetLane.Flags)netLane.m_flags).ToString())
                        .AppendLine(" length:        " + netLane.m_length + " \n");
                    laneId = netManager.m_lanes.m_buffer[laneId].m_nextLane;
                }
                bool isStart = netManager.m_segments.m_buffer[segmentId].m_startNode == SelectedNodeId;
                SegmentEndGeometry segmentEnd = new SegmentEndGeometry(segmentId, isStart);
                segmentEnd.Recalculate(GeometryCalculationMode.Init);
                NetInfo netInfo = netManager.m_segments.m_buffer[segmentId].Info;
                stringBuilder.AppendLine("NetInfo.line segmentID: " + segmentId + " isStart?: " + isStart);
                foreach (NetInfo.Lane netInfoLane in netInfo.m_lanes) {
                    stringBuilder.AppendLine(" direction:         " + netInfoLane.m_direction);
                    stringBuilder.AppendLine(" directionCalc:     " + (isStart ? netInfoLane.m_direction : NetInfo.InvertDirection(netInfoLane.m_direction)));
                    stringBuilder.AppendLine(" finalDir:          " + netInfoLane.m_finalDirection);
                    stringBuilder.AppendLine(" laneType:          " + netInfoLane.m_laneType);
                    stringBuilder.AppendLine(" vehType:           " + netInfoLane.m_vehicleType);
                    stringBuilder.AppendLine(" similarLineCount:  " + netInfoLane.m_similarLaneCount);
                    stringBuilder.AppendLine(" similarLineIndex:  " + netInfoLane.m_similarLaneIndex);
                    stringBuilder.AppendLine(" verticalOffset:    " + netInfoLane.m_verticalOffset);
                    stringBuilder.AppendLine(" stopType:          " + netInfoLane.m_stopType + "\n");

                }
                stringBuilder.AppendLine("");
                stringBuilder.AppendLine(segmentEnd + "\n");

            }
            return stringBuilder.ToString();
        }

        private string GenerateString(List<List<ICustomSegmentLight>> lineLights) {
            StringBuilder stringBuilder = new StringBuilder();
            lineLights?.ForEach(line => {
                string segment = "";
                line.ForEach(segmentLight => {
                    segment += segmentLight.CurrentMode.ToString() + "State: " + segmentLight.LightMain.ToString() + ", ";
                });
                stringBuilder.AppendLine(segment);
            });
            return stringBuilder.ToString();
        }
    }
}
