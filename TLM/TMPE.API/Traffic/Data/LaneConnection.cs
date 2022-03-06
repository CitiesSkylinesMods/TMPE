using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.API.Traffic.Data {
    public class LaneConnection : IEquatable<LaneConnection> {

        public uint laneId;

        public bool startNode;

        public uint[] connectedLaneIds;

        public LaneConnection(uint laneId, bool startNode) {
            this.laneId = laneId;
            this.startNode = startNode;
            connectedLaneIds = null;
        }

        public bool IsDefault() {
            return connectedLaneIds == null;
        }

        public override string ToString() {
            string connectedLanes = connectedLaneIds == null
                                    ? "null"
                                    : $"[{string.Join(",", connectedLaneIds.Select(id => id.ToString()).ToArray())}]";

            return $"[LaneConnection {base.ToString()}" +
                    $"\n\tlaneId={laneId}" +
                    $"\n\tstartNode={startNode}" +
                    $"\n\tconnectedLaneIds={connectedLanes}";
        }

        public bool Equals(LaneConnection other) {
            return laneId == other.laneId && startNode == other.startNode;
        }

        public override bool Equals(object other) {
            return other is LaneConnection connection
                   && Equals(connection);
        }

        public override int GetHashCode() {
            int prime = 31;
            int result = 1;
            result = prime * result + laneId.GetHashCode();
            result = prime * result + startNode.GetHashCode();
            return result;
        }
    }
}
