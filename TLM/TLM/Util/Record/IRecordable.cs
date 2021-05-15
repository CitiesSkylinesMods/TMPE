using System;
using System.Collections.Generic;

namespace TrafficManager.Util.Record {
    public interface IRecordable {
        /// <summary>
        /// records network state.
        /// </summary>
        void Record();

        /// <summary>
        /// restores network state.
        /// </summary>
        void Restore();

        /// <summary>
        /// Restores recorded network state to another network.
        /// </summary>
        /// <param name="map">maps old Instance IDs to new Instance IDs</param>
        void Transfer(Dictionary<InstanceID, InstanceID> map);

        byte[] Serialize();
    }
}
