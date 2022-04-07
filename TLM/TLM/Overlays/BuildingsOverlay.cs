namespace TrafficManager.Overlays {
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl.OverlayManagerData;
    using TrafficManager.Manager.Overlays.Layers;
    using TrafficManager.Overlays.Layers;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    public class BuildingsOverlay
        : ManagedOverlayBase, IManagedOverlay, ILabel {

        private static Color normalColor = Color.magenta;
        private static Color infoviewColor = Color.red;

        private static Color activeColor = normalColor;

        public static bool Detailed; // all labels
        private HashSet<InstanceID> detailed; // specific labels

        private LabelLayer LabelLayer { get; }

        public BuildingsOverlay()
            : base(Overlays.Buildings, EntityType.Building)
            { LabelLayer = LabelLayer.Instance; }

        public override void OnCameraMoved(ref OverlayState state) {

            LabelLayer.Add(this, ViewportCache.Instance.newBuildings);
        }

        public override void OnModifierChanged(ref OverlayState state) {
            if (state.Shift != Detailed) {
                Detailed = state.Shift;
                LabelLayer.QueueUpdate(this);
            }
        }

        public override void OnInfoViewChanged(ref OverlayState state) {
            activeColor = state.InfoMode == InfoManager.InfoMode.None
                ? normalColor
                : infoviewColor;
        }

        /* Label interface */

        public bool IsLabelInteractive(ref InstanceID id) {
            return true;
        }

        public Vector3 UpdateLabelWorldPos(ref InstanceID id, ref OverlayState state) =>
            id.GetWorldPos(fast: true);

        public string UpdateLabelStyle(ref InstanceID id, bool mouseInside, ref OverlayState state, GUIStyle style) {
            style.fontSize = 15;
            style.normal.textColor = activeColor;

            return Detailed || detailed.Contains(id)
                ? DetailedText(ref id)
                : id.GetTag();
        }

        public TaskState? OnLabelHovered(ref InstanceID id, bool mouseInside, ref OverlayState state) {
            if (mouseInside) {
                detailed.Add(id);
            } else if (detailed.Contains(id)) {
                detailed.Remove(id);
            }
            return TaskState.NeedsUpdate;
        }
        
        public TaskState? OnLabelClicked(ref InstanceID id, bool mouseInside, ref OverlayState state) => null;

        public void OnLabelHidden(ref InstanceID id) { }

        public void OnLabelDeleted(ref InstanceID id) { }

        private string DetailedText(ref InstanceID id) {
            const int rowLimit = 3;
            int count = 0;

            ref var building = ref id.Building.ToBuilding();

            var buildingFlags = building.m_flags;
            var allFlags = Enum.GetValues(typeof(NetNode.Flags));

            var text = new StringBuilder(150);

            text
                .Append(id.GetTag())
                .AppendLine();

            foreach (Building.Flags flag in allFlags) {
                if ((buildingFlags & flag) != 0) {
                    text.Append(flag);
                    if (count++ > rowLimit) {
                        count = 0;
                        text.AppendLine();
                    } else {
                        text.Append(Space);
                    }
                }
            }

            return text.ToString();
        }

    }
}
