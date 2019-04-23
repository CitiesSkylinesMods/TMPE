using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static TrafficManager.Configuration;

namespace TrafficManager.Custom.AssetManager
{
    /* Purpose - match the old ids of segments/nodes/lanes (as in the asset editor) with the new ones (as in the game when the intersection is built) */
    public class IdentificationTool
    {
        /* List of these is packed with the asset */
        [Serializable]
        public class AssetSegment
        {
            public AssetSegment(ushort segmentId)
            {
                NetSegment segment = NetManager.instance.m_segments.m_buffer[segmentId];
                this.Id = segmentId;
                this.StartNode = segment.m_startNode;
                this.EndNode = segment.m_endNode;
                this.Lanes = GetLanes(segment);
            }

            public ushort Id;
            public ushort StartNode;
            public ushort EndNode;
            public uint[] Lanes;
        }

        public class IdentificationToolException : Exception
        {
            public IdentificationToolException(string msg) : base(msg)
            {
            }
        }

        /* This seems to work fine, except for the lane pairs (which is pretty important) */
        public static void CreateDictionaries(List<ushort> newSegments, List<AssetSegment> oldSegments, out Dictionary<ushort, ushort> segmentPairs, 
            out Dictionary<ushort, ushort> nodePairs, out Dictionary<uint, uint> lanePairs)
        {
            if (newSegments.Count != oldSegments.Count)
                throw new IdentificationToolException("Cannot make segment/node/lane pairs: Different list sizes");

            segmentPairs = new Dictionary<ushort, ushort>();
            nodePairs = new Dictionary<ushort, ushort>();
            lanePairs = new Dictionary<uint, uint>();

            for (int i = 0; i < newSegments.Count; i++)
            {
                AssetSegment segment = oldSegments[i];
                ushort newSegmentId = newSegments[i];
                NetSegment newSegment = NetManager.instance.m_segments.m_buffer[newSegmentId];

                if(segmentPairs.ContainsKey(segment.Id))
                {
                    if (segmentPairs[segment.Id] != newSegments[newSegmentId])
                        throw new IdentificationToolException("Cannot make segment pairs: Different values for the same key");
                    continue;
                }
                segmentPairs.Add(segment.Id, newSegmentId);

                if (nodePairs.ContainsKey(segment.StartNode))
                {
                    if (nodePairs[segment.StartNode] != newSegment.m_startNode)
                        throw new IdentificationToolException("Cannot make node pairs: Different values for the same key");
                }
                else
                {
                    nodePairs[segment.StartNode] = newSegment.m_startNode;
                }

                if (nodePairs.ContainsKey(segment.EndNode))
                {
                    if (nodePairs[segment.EndNode] != newSegment.m_endNode)
                        throw new IdentificationToolException("Cannot make node pairs: Different values for the same key");
                }
                else
                {
                    nodePairs[segment.EndNode] = newSegment.m_endNode;
                }

                uint[] oldLanes = segment.Lanes;
                uint[] newLanes = GetLanes(newSegment);
                if(oldLanes.Length != newLanes.Length)
                    throw new IdentificationToolException("Cannot make lane pairs: Different list lengths");
                for(int j = 0; j < oldLanes.Length; j++)
                {
                    if(lanePairs.ContainsKey(oldLanes[j]))
                    {
                        if (lanePairs[oldLanes[j]] != newLanes[j])
                            throw new IdentificationToolException("Cannot make lane pairs: Different values for the same key");
                        continue;
                    }
                    lanePairs.Add(oldLanes[j], newLanes[j]);
                }
            }
        }

        public static void TranslateGameConfiguration(Configuration config, Dictionary<ushort,ushort> segmentPairs, Dictionary<ushort, ushort> nodePairs,
            Dictionary<uint, uint> lanePairs)
        {
            // we don't need these
            config.ExtCitizens = new List<ExtCitizenData>();
            config.ExtCitizenInstances = new List<ExtCitizenInstanceData>();

            // toggled traffic lights
            config.ToggledTrafficLights.RemoveAll(item => !nodePairs.ContainsKey(item.nodeId));
            foreach (NodeTrafficLight item in config.ToggledTrafficLights)
            {
                item.nodeId = nodePairs[item.nodeId];
            }

            // lane connections
            config.LaneConnections.RemoveAll(item => !lanePairs.ContainsKey(item.lowerLaneId));
            config.LaneConnections.RemoveAll(item => !lanePairs.ContainsKey(item.higherLaneId));
            foreach (LaneConnection item in config.LaneConnections)
            {
                item.lowerLaneId = lanePairs[item.lowerLaneId];
                item.higherLaneId = lanePairs[item.higherLaneId];
                // (dead end)
                /*if (item.lowerLaneId > item.higherLaneId)
                {
                    uint buffer = item.lowerLaneId;
                    item.lowerLaneId = item.higherLaneId;
                    item.higherLaneId = buffer;
                    item.lowerStartNode = !item.lowerStartNode;
                }*/
            }

            // lane arrows
            config.LaneArrows.RemoveAll(item => !lanePairs.ContainsKey(item.laneId));
            foreach (var item in config.LaneArrows)
            {
                item.laneId = lanePairs[item.laneId];
            }

            // lane speed limits
            config.LaneSpeedLimits.RemoveAll(item => !lanePairs.ContainsKey(item.laneId));
            foreach (var item in config.LaneSpeedLimits)
            {
                item.laneId = lanePairs[item.laneId];
            }

            // lane allowed vehicles
            config.LaneAllowedVehicleTypes.RemoveAll(item => !lanePairs.ContainsKey(item.laneId));
            foreach (var item in config.LaneAllowedVehicleTypes)
            {
                item.laneId = lanePairs[item.laneId];
            }

            // timed lights
            // TODO: remove all failed matches
            config.TimedLights.RemoveAll(item => !segmentPairs.ContainsKey(item.nodeId));
            foreach (var item in config.TimedLights)
            {
                item.nodeId = nodePairs[item.nodeId];
                for (int i = 0; i < item.nodeGroup.Count; i++)
                {
                    item.nodeGroup[i] = nodePairs[item.nodeGroup[i]];
                }

                for (int i = 0; i < item.timedSteps.Count; i++)
                {
                    Dictionary<ushort, CustomSegmentLights> newTimedSteps = new Dictionary<ushort, CustomSegmentLights>();
                    foreach (var pair in item.timedSteps[i].segmentLights)
                    {
                        var costumSegmentLights = pair.Value;
                        costumSegmentLights.nodeId = nodePairs[costumSegmentLights.nodeId];
                        costumSegmentLights.segmentId = segmentPairs[costumSegmentLights.segmentId];
                        foreach (var pair2 in costumSegmentLights.customLights)
                        {
                            pair2.Value.nodeId = nodePairs[pair2.Value.nodeId];
                            pair2.Value.segmentId = segmentPairs[pair2.Value.segmentId];
                        }
                        newTimedSteps[nodePairs[pair.Key]] = pair.Value;
                    }
                    item.timedSteps[i].segmentLights = newTimedSteps;
                }
            }

            // junction settings
            config.SegmentNodeConfs.RemoveAll(item => !segmentPairs.ContainsKey(item.segmentId));
            foreach (SegmentNodeConf item in config.SegmentNodeConfs)
            {
                item.segmentId = segmentPairs[item.segmentId];
            }

            // we don't need this
            config.CustomDefaultSpeedLimits = new Dictionary<string, float>();

            // priority segments
            config.CustomPrioritySegments.RemoveAll(item => !nodePairs.ContainsKey(item.nodeId));
            config.CustomPrioritySegments.RemoveAll(item => !segmentPairs.ContainsKey(item.segmentId));
            foreach (PrioritySegment item in config.CustomPrioritySegments)
            {
                item.segmentId = segmentPairs[item.segmentId];
                item.nodeId = nodePairs[item.nodeId];
            }

            // parking restrictions
            config.ParkingRestrictions.RemoveAll(item => !segmentPairs.ContainsKey(item.segmentId));
            foreach (ParkingRestriction item in config.ParkingRestrictions)
            {
                item.segmentId = segmentPairs[item.segmentId];
            }
        }

        /* I am not sure about this method - does it always return lane ids in the right order or whatever? */
        public static uint[] GetLanes(NetSegment segment)
        {
            NetInfo info = segment.Info;
            uint[] lanes = new uint[info.m_lanes.Length];

            int i = 0;
            uint nextLane = segment.m_lanes;
            while (i < info.m_lanes.Length && nextLane != 0u)
            {
                lanes[i] = nextLane;
                nextLane = Singleton<NetManager>.instance.m_lanes.m_buffer[nextLane].m_nextLane;
                i++;
            }

            return lanes;
        }

        /// <summary>
        /// Returns the asset name without the part before the dot, if there is any
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string GetNameWithoutPrefix(string str)
        {
            int num = str.IndexOf(".");
            if (num > -1)
            {
                return str.Substring(num + 1);
            }
            return str;
        }
    }
}
