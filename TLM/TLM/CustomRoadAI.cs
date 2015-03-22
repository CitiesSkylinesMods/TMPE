using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager
{
    internal class CustomRoadAI : RoadAI
    {
        public override void GetNodeState(ushort nodeID, ref NetNode nodeData, ushort segmentID, ref NetSegment segmentData, out NetNode.Flags flags, out Color color)
        {
            base.GetNodeState(nodeID, ref nodeData, segmentID, ref segmentData, out flags, out color);
        }
        public override void SimulationStep(ushort nodeID, ref NetNode data)
        {
            var tfsDictionary = ToolTrafficLight.getNodeSimulation(nodeID);

            if (tfsDictionary != null)
            {
                tfsDictionary.SimulationStep(ref data);
            }
            else
            {
                base.SimulationStep(nodeID, ref data);
            }
        }
    }
}
