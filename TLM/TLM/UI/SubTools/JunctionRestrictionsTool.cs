namespace TrafficManager.UI.SubTools {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using UnityEngine;
    using TrafficManager.State.Keybinds;
    using TrafficManager.UI.SubTools.PrioritySigns;
    using TrafficManager.Util;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu.OSD;

    public class JunctionRestrictionsTool
        : LegacySubTool,
          UI.MainMenu.IOnscreenDisplayProvider
    {
        private readonly HashSet<ushort> currentRestrictedNodeIds;

        /// <summary>
        /// Set to true in render, if any of the overlay clickable icons has mouse in them.
        /// </summary>
        private bool isAnyOverlayHandleHovered;

        private readonly float junctionRestrictionsSignSize = 80f;

        public JunctionRestrictionsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            currentRestrictedNodeIds = new HashSet<ushort>();
        }

        public override void OnToolGUI(Event e) {
            // handle delete
            if (KeybindSettingsBase.RestoreDefaultsKey.KeyDown(e)) {
                netService.IterateNodeSegments(
                    SelectedNodeId,
                    (ushort segmmentId, ref NetSegment segment) => {
                        // TODO: #568 provide unified delete key for all managers.
                        bool startNode = (bool)netService.IsStartNode(segmmentId, SelectedNodeId);
                        JunctionRestrictionsManager.Instance.ClearSegmentEnd(segmmentId, startNode);
                        return true;
                    });
            }
        }

        public override void RenderOverlayForOtherTools(RenderManager.CameraInfo cameraInfo) { }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (SelectedNodeId != 0) {
                // draw selected node
                MainTool.DrawNodeCircle(cameraInfo, SelectedNodeId, true);
            }

            if ((HoveredNodeId != 0) && (HoveredNodeId != SelectedNodeId) &&
                ((Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags &
                  (NetNode.Flags.Junction | NetNode.Flags.Bend)) != NetNode.Flags.None)) {
                // draw hovered node
                MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, Input.GetMouseButton(0));
            }
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly &&
                !(Options.junctionRestrictionsOverlay
                || MassEditOverlay.IsActive)) {
                return;
            }

            if (SelectedNodeId != 0) {
                isAnyOverlayHandleHovered = false;
            }

            ShowGUIOverlay_ShowSigns(viewOnly);
        }

        private void ShowGUIOverlay_ShowSigns(bool viewOnly) {
#if DEBUG
            bool logJunctions = !viewOnly && DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logJunctions = false;
#endif
            NetManager netManager = Singleton<NetManager>.instance;
            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;

            if (!viewOnly && (SelectedNodeId != 0)) {
                currentRestrictedNodeIds.Add(SelectedNodeId);
            }

            ushort updatedNodeId = 0;
            bool handleHovered = false;
            bool cursorInPanel = IsCursorInPanel();
            TrafficRulesOverlay overlay = new TrafficRulesOverlay(
                mainTool: this.MainTool,
                debug: logJunctions,
                handleClick: !cursorInPanel);

            foreach (ushort nodeId in currentRestrictedNodeIds) {
                if (!Constants.ServiceFactory.NetService.IsNodeValid(nodeId)) {
                    continue;
                }

                Vector3 nodePos = netManager.m_nodes.m_buffer[nodeId].m_position;
                Vector3 diff = nodePos - camPos;

                if (diff.sqrMagnitude > TrafficManagerTool.MAX_OVERLAY_DISTANCE_SQR) {
                    continue; // do not draw if too distant
                }

                bool visible = GeometryUtil.WorldToScreenPoint(nodePos, out Vector3 _);

                if (!visible) {
                    continue;
                }

                // draw junction restrictions
                overlay.ViewOnly = viewOnly || (nodeId != SelectedNodeId);
                if (overlay.DrawSignHandles(nodeId: nodeId,
                                            node: ref netManager.m_nodes.m_buffer[nodeId],
                                            camPos: ref camPos,
                                            stateUpdated: out bool update))
                {
                    handleHovered = true;
                }

                if (update) {
                    updatedNodeId = nodeId;
                }
            }

            isAnyOverlayHandleHovered = handleHovered;

            if (updatedNodeId != 0) {
                RefreshCurrentRestrictedNodeIds(updatedNodeId);
            }
        }

        public override void OnPrimaryClickOverlay() {
#if DEBUG
            bool logJunctions = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logJunctions = false;
#endif
            if (HoveredNodeId == 0) {
                return;
            }

            if (isAnyOverlayHandleHovered) {
                return;
            }

            if (!logJunctions &&
                ((Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags &
                  (NetNode.Flags.Junction | NetNode.Flags.Bend)) == NetNode.Flags.None)) {
                return;
            }

            SelectedNodeId = HoveredNodeId;
            MainTool.RequestOnscreenDisplayUpdate();

            // prevent accidential activation of signs on node selection (TODO [issue #740] improve this !)
            MainTool.CheckClicked();
        }

        public override void OnSecondaryClickOverlay() {
            if (SelectedNodeId != 0) {
                SelectedNodeId = 0;
                MainTool.RequestOnscreenDisplayUpdate();
            } else {
                MainTool.SetToolMode(ToolMode.None);
            }
        }

        public override void OnActivate() {
            base.OnActivate();
            Log._Debug("LaneConnectorTool: OnActivate");
            SelectedNodeId = 0;
            MainTool.RequestOnscreenDisplayUpdate();
            RefreshCurrentRestrictedNodeIds();
        }

        public override void Cleanup() {
            foreach (ushort nodeId in currentRestrictedNodeIds) {
                JunctionRestrictionsManager.Instance.RemoveJunctionRestrictionsIfNecessary(nodeId);
            }

            RefreshCurrentRestrictedNodeIds();
        }

        public override void Initialize() {
            base.Initialize();
            Cleanup();
            if (Options.junctionRestrictionsOverlay ||
                MassEditOverlay.IsActive) {
                RefreshCurrentRestrictedNodeIds();
            } else {
                currentRestrictedNodeIds.Clear();
            }
        }

        private void RefreshCurrentRestrictedNodeIds(ushort forceNodeId = 0) {
            if (forceNodeId == 0) {
                currentRestrictedNodeIds.Clear();
            } else {
                currentRestrictedNodeIds.Remove(forceNodeId);
            }

            for (uint nodeId = forceNodeId == 0 ? 1u : forceNodeId;
                 nodeId <= (forceNodeId == 0 ? NetManager.MAX_NODE_COUNT - 1 : forceNodeId);
                 ++nodeId) {
                if (!Constants.ServiceFactory.NetService.IsNodeValid((ushort)nodeId)) {
                    continue;
                }

                if (JunctionRestrictionsManager.Instance.HasJunctionRestrictions((ushort)nodeId)) {
                    currentRestrictedNodeIds.Add((ushort)nodeId);
                }
            }
        }

        private static string T(string key) => Translation.JunctionRestrictions.Get(key);

        public void UpdateOnscreenDisplayPanel() {
            if (SelectedNodeId == 0) {
                // Select mode
                var items = new List<OsdItem>();
                items.Add(new UI.MainMenu.OSD.ModeDescription(T("JR.OnscreenHint.Mode:Select")));
                OnscreenDisplay.Display(items);
            } else {
                // Edit mode
                var items = new List<OsdItem>();
                items.Add(new UI.MainMenu.OSD.ModeDescription(T("JR.OnscreenHint.Mode:Edit")));
                items.Add(
                    new UI.MainMenu.OSD.Shortcut(
                        keybindSetting: KeybindSettingsBase.RestoreDefaultsKey,
                        localizedText: T("JR.OnscreenHint.Reset:Reset to default")));

                items.Add(OnscreenDisplay.RightClick_LeaveNode());
                OnscreenDisplay.Display(items);
            }

            // Default: no hint
            // OnscreenDisplay.Clear();
        }
    } // end class
}