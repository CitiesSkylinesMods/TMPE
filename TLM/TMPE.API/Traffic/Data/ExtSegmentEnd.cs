namespace TrafficManager.API.Traffic.Data
{
    using System;
    using System.Runtime.InteropServices;
    using TrafficManager.API.Traffic.Enums;
    using UnityEngine;

    [StructLayout(LayoutKind.Auto)]
    public struct ExtSegmentEnd : IEquatable<ExtSegmentEnd>
    {
        /// <summary>
        /// Segment id
        /// </summary>
        public ushort segmentId;

        /// <summary>
        /// At start node?
        /// </summary>
        public bool startNode;

        /// <summary>
        /// Node id
        /// </summary>
        public ushort nodeId;

        /// <summary>
        /// Can vehicles leave the node via this segment end?
        /// </summary>
        public bool outgoing;

        /// <summary>
        /// Can vehicles enter the node via this segment end?
        /// </summary>
        public bool incoming;

        public Vector3 LeftCorner;
        public Vector3 LeftCornerDir;
        public Vector3 RightCorner;
        public Vector3 RightCornerDir;

        /// <summary>
        /// All available Lane Arrows describing possible outgoing directions via this segment end
        /// </summary>
        public LaneArrows laneArrows;

        /// <summary>
        /// First registered vehicle id on this segment end
        /// </summary>
        public ushort firstVehicleId;

        public ExtSegmentEnd(ushort segmentId, bool startNode) {
            this.segmentId = segmentId;
            this.startNode = startNode;
            nodeId = 0;
            outgoing = false;
            incoming = false;
            firstVehicleId = 0;
            LeftCorner = Vector3.zero;
            LeftCornerDir = Vector3.zero;
            RightCorner = Vector3.zero;
            RightCornerDir = Vector3.zero;
            laneArrows = LaneArrows.None;
        }

        public override string ToString()
        {
            return string.Format(
                "[ExtSegmentEnd {0}\n\tsegmentId={1}\n\tstartNode={2}\n\tnodeId={3}\n" +
                "\toutgoing={4}\n\tincoming={5}\n\tfirstVehicleId={6}\n" +
                "\tLeftCorner={7}\n\tLeftCornerDir={8}\n\tRightCorner={9}\n\tRightCornerDir={10}\n" +
                "\tLaneArrows={11}" +
                "\nExtSegmentEnd]",
                base.ToString(),
                segmentId,
                startNode,
                nodeId,
                outgoing,
                incoming,
                firstVehicleId,
                LeftCorner,
                LeftCornerDir,
                RightCorner,
                RightCornerDir,
                laneArrows);
        }

        public bool Equals(ExtSegmentEnd otherSegEnd)
        {
            return segmentId == otherSegEnd.segmentId && startNode == otherSegEnd.startNode;
        }

        public override bool Equals(object other)
        {
            return other is ExtSegmentEnd end
                   && Equals(end);
        }

        public override int GetHashCode()
        {
            int prime = 31;
            int result = 1;
            result = prime * result + segmentId.GetHashCode();
            result = prime * result + startNode.GetHashCode();
            return result;
        }
    }
}