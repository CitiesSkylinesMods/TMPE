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

    public class NetworksOverlay
        : ManagedOverlayBase, IManagedOverlay, ILabel {

        private static Color normalNodeColor = Color.cyan;
        private static Color infoviewNodeColor = Color.blue;

        private static Color normalSegmentColor = Color.red;
        private static Color infoviewSegmentColor = Color.red;

        private static Color activeNodeColor = normalNodeColor;
        private static Color activeSegmentColor = normalSegmentColor;

        public static bool Detailed; // all labels
        private HashSet<InstanceID> detailed; // specific labels

        private LabelLayer LabelLayer { get; }

        public NetworksOverlay()
            : base(Overlays.Networks, EntityType.Node | EntityType.Segment)
            { LabelLayer = LabelLayer.Instance; }

        public override void OnCameraMoved(ref OverlayState state) {

            var numLabels =
                ViewportCache.Instance.newNodes.m_size +
                ViewportCache.Instance.newSegments.m_size;

            LabelLayer.MakeRoomFor(numLabels);

            LabelLayer.Add(this, ViewportCache.Instance.newNodes);
            LabelLayer.Add(this, ViewportCache.Instance.newSegments);
        }

        public override void OnModifierChanged(ref OverlayState state) {
            if (state.Shift != Detailed) {
                Detailed = state.Shift;
                LabelLayer.QueueUpdate(this);
            }
        }

        public override void OnInfoViewChanged(ref OverlayState state) {
            if (state.InfoMode == InfoManager.InfoMode.None) {
                activeNodeColor = normalNodeColor;
                activeSegmentColor = normalSegmentColor;
            } else {
                activeNodeColor = infoviewNodeColor;
                activeSegmentColor = infoviewSegmentColor;
            }
        }

        /* ILabel */

        public bool IsLabelInteractive(ref InstanceID id) {
            return true;
        }

        public Vector3 UpdateLabelWorldPos(ref InstanceID id, ref OverlayState state) =>
            id.GetWorldPos(fast: true);

        public string UpdateLabelStyle(ref InstanceID id, bool mouseInside, ref OverlayState data, GUIStyle style) {
            style.fontSize = 15;
            switch (id.Type) {
                case InstanceType.NetNode:
                    style.normal.textColor = activeNodeColor;
                    return Detailed || detailed.Contains(id)
                        ? DetailedNodeText(ref id)
                        : id.GetTag();
                case InstanceType.NetSegment:
                    style.normal.textColor = activeSegmentColor;
                    return Detailed || detailed.Contains(id)
                        ? DetailedSegmentText(ref id)
                        : id.GetTag();
                default:
                    style.normal.textColor = Color.red;
                    return id.GetTag();
            }
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

        // todo: scroll wheel support to select what gets shown
        private string DetailedNodeText(ref InstanceID id) {
            const int rowLimit = 3;
            int count = 0;

            ref var node = ref id.NetNode.ToNode();

            var nodeFlags = node.m_flags;
            var allFlags = Enum.GetValues(typeof(NetNode.Flags));

            var text = new StringBuilder(150);

            text
                .AppendLine(id.GetTag())
                .Append("Lane: ")
                .Append(node.m_lane)
                .AppendLine();

            foreach (NetNode.Flags flag in allFlags) {
                if ((nodeFlags & flag) != 0) {
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

        // todo: scroll wheel support to select what gets shown
        private string DetailedSegmentText(ref InstanceID id) {
            const int rowLimit = 3;
            int count = 0;

            ref var segment = ref id.NetSegment.ToSegment();

            var info = segment.Info;
            var segmentFlags = segment.m_flags;
            var allFlags = Enum.GetValues(typeof(NetSegment.Flags));

            var text = new StringBuilder(150);

            text
                .AppendLine(id.GetTag())
                .Append(info.GetService().ToString("f"))
                .Append(" > ")
                .Append(info.GetSubService().ToString("f"))
                .AppendLine();

            foreach (NetSegment.Flags flag in allFlags) {
                if ((segmentFlags & flag) != 0) {
                    text.Append(flag);
                    if (count++ > rowLimit) {
                        text.AppendLine();
                        count = 0;
                    } else {
                        text.Append(Space);
                    }
                }
            }

            return text.ToString();
        }
    }
}
