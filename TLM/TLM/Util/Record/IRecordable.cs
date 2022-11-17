using System.Linq;
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

        /// <summary>
        /// Detects if record is empty to help releasing empty records from memory.
        /// </summary>
        /// <returns>true if record stores only default values. false if record stores any useful information.</returns>
        bool IsDefault();

        byte[] Serialize();
    }

    public static class RecordExtensions {
        public static bool AreDefault<T>(this IEnumerable<T> records)
            where T : IRecordable =>
            records == null || records.All(record => record == null || record.IsDefault());
    }
}
