using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.API.Manager {
    public interface IExtLaneManager {

        /// <summary>
        /// Returns the Segment ID and Lane Index associated with the specified Lane ID.
        /// </summary>
        /// <param name="laneId">The Lane ID</param>
        /// <param name="segmentId">returns with the Segment ID if successful; otherwise 0</param>
        /// <param name="laneIndex">returns with the Lane Index if successful; otherwise -1</param>
        /// <returns>true if successful, or false if <paramref name="laneId"/> does not represent a valid lane</returns>
        bool GetSegmentAndIndex(uint laneId, out ushort segmentId, out int laneIndex);

        /// <summary>
        /// Returns the Lane Index associated with the specified Lane ID.
        /// </summary>
        /// <param name="laneId">The Lane ID</param>
        /// <returns>The Lane Index if successful, or -1 if <paramref name="laneId"/> does not represent a valid lane</returns>
        int GetLaneIndex(uint laneId);

        /// <summary>
        /// Returns the prefab info for the specified Lane ID.
        /// </summary>
        /// <param name="laneId">a Lane ID</param>
        /// <returns>prefab info for the lane</returns>
        NetInfo.Lane GetLaneInfo(uint laneId);
    }
}
