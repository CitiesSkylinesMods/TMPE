namespace TrafficManager.Overlays.Layers {
    using System.Collections.Generic;
    using TrafficManager.API.Attributes;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl.OverlayManagerData;

    public interface ILayer {

        /* Layer state */

        LayerState LayerState { get; }

        /* Add tasks */

        void MakeRoomFor(int numItems);

        /* Remove all tasks */

        void Clear();

        void Release();

        /* Reposition tasks */

        void QueueReposition(InstanceID id);

        void QueueReposition(HashSet<InstanceID> hash, bool deleteOthers = false);

        void QueueReposition(Overlays overlay);

        [Spike]
        void Reposition(ref OverlayState state);



    }
}
