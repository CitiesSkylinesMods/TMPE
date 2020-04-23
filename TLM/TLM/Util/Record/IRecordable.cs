using System.Collections.Generic;

namespace TrafficManager.Util.Record {
    public interface IRecordable {
        void Record();
        void Restore();
    }
}
