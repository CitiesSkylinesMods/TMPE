namespace TrafficManager.Overlays {
    using CSUtil.Commons;
    using System;
    using System.Diagnostics;
    using System.Text;
    using TrafficManager.API.Attributes;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.OverlayManagerData;

    public abstract class ManagedOverlayBase : IManagedOverlay {
        public const string Space = " "; // useful for stringbuilder

        public ManagedOverlayBase(Overlays overlay, EntityType targets) {
            Overlay = overlay;
            Targets = targets;

            OverlayManager.Instance.RegisterOverlay(this);

            DiagnoseErrors(); // DEBUG-only
        }

        public Overlays Overlay { get; private set; }

        public EntityType Targets { get; private set; }

        public virtual bool CanBeUsed => true;

        public virtual NetInfo.LaneType? LaneTypes => null;

        public virtual VehicleInfo.VehicleType? VehicleTypes => null;

        [Spike]
        public virtual void OnCameraMoved(ref OverlayState state) { }

        [Hot("Each frame while overlay active")]
        [Obsolete("This will be removed once tunnel overlay is done via patching")]
        public virtual void OnFrameChanged(ref OverlayState state) { }

        [Spike]
        public virtual void OnInfoViewChanged(ref OverlayState state) { }

        [Spike]
        public virtual void OnModifierChanged(ref OverlayState state) { }

        [Spike]
        public virtual void Reset() { }

        [Conditional("DEBUG")]
        internal void DiagnoseErrors() {
            if (!OverlayManager.IsIndividualOverlay(Overlay))
                Log.Error("Overlay must be singular");

            if (Targets == 0)
                Log.Error("Targets should _usually_ be specified");
        }
    }
}
