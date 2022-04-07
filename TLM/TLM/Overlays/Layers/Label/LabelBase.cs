namespace TrafficManager.Manager.Overlays.Layers {
    using CSUtil.Commons;
    using System.Diagnostics;
    using System.Text;
    using TrafficManager.API.Attributes;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Manager.Impl.OverlayManagerData;
    using TrafficManager.Overlays.Layers;
    using UnityEngine;

    public abstract class LabelBase : ILabel {
        protected const string Space = " "; // useful for stringbuilder
        protected const byte TEXT_SIZE = 15;

        [Spike]
        public LabelBase(Overlays overlay, InstanceID id) {
            Overlay = overlay;
            ID = id;
            Style = new();
            DiagnoseErrors(); // DEBUG-only
        }

        public Overlays Overlay { get; private set; }

        public InstanceID ID { get; private set; }

        [Spike]
        public abstract Vector3 GetLabelWorldPos(InstanceID id);

        public virtual string UpdateLabelStyle(InstanceID id, bool mouseInside, ref OverlayState state, GUIStyle style) {
            // todo style

            return new StringBuilder(15 + 6)
                .Append(ID.Type.ToString("f"))
                .Append(Space)
                .Append(ID.RawData & 0xFFFFFFu)
                .ToString();
        }

        [Cold]
        public virtual bool IsLabelInteractive(InstanceID id) => false;

        [Cold]
        public virtual TaskState? OnLabelHovered(InstanceID id, bool mouseInside, ref OverlayState data) => null;

        [Cold]
        public virtual TaskState? OnLabelClicked(InstanceID id, bool mouseInside, ref OverlayState data) => null;

        [Spike]
        public virtual void OnLabelHidden(InstanceID id) { }

        [Spike]
        public virtual void OnLabelDeleted(InstanceID id) { }

        [Spike]
        public GUIStyle Style { get; protected set; }

        [Spike]
        public virtual byte TextSize => 15;

        [Conditional("DEBUG")]
        internal void DiagnoseErrors() {
            if (!OverlayManager.IsIndividualOverlay(Overlay))
                Log.Error("label.Overlay must be singular");

            if (ID.IsEmpty)
                Log.Error("label.ID should _usually_ be > 0");
        }
    }
}
