namespace TrafficManager.API.Traffic {
    using System;
    using System.Collections.Generic;

    [Obsolete("should be removed when implementing issue #240")]
    public interface ISegmentEnd : ISegmentEndId {
        [Obsolete]
        ushort NodeId { get; }
        //ushort FirstRegisteredVehicleId { get; set; } // TODO private set

        void Update();

        //void Destroy();
        IDictionary<ushort, uint>[] MeasureOutgoingVehicles(bool includeStopped = true,
                                                            bool debug = false);

        //uint GetRegisteredVehicleCount();
    }
}