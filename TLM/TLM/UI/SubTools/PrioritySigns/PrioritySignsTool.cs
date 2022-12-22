namespace TrafficManager.UI.SubTools.PrioritySigns {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using static Util.SegmentTraverser;
    using ColossalFramework.UI;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Util.Record;
    using static Util.Shortcuts;
    using TrafficManager.Util.Extensions;

    public class PrioritySignsTool
        : LegacySubTool,
          UI.MainMenu.IOnscreenDisplayProvider
    {
        public enum PrioritySignsMassEditMode {
            Min = 0,
            MainYield = 0,
            MainStop = 1,
            YieldMain = 2,
            StopMain = 3,
            Undo = 4,
            Max = 4,
        }

        IRecordable record_;

        enum ModifyMode {
            None,
            PriorityRoad,
            HighPriorityRoad,
            HighPriorityJunction,
            Roundabout,
        }
        static class PrevHoveredState {
            public static ushort SegmentId;
            public static ushort NodeId;
            public static ModifyMode Mode;
        }

        private readonly HashSet<ushort> currentPriorityNodeIds;
        private PrioritySignsMassEditMode massEditMode = PrioritySignsMassEditMode.Min;

        public PrioritySignsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            currentPriorityNodeIds = new HashSet<ushort>();
        }

        public override void OnPrimaryClickOverlay() {
            if(ControlIsPressed || ShiftIsPressed) {
                if (HoveredSegmentId == 0) {
                    return;
                }
                SelectedNodeId = 0;
                MainTool.RequestOnscreenDisplayUpdate();
            }

            if(massEditMode == PrioritySignsMassEditMode.Undo) {
                record_?.Restore();
            }
            else if (ControlIsPressed && ShiftIsPressed) {
                Log.Info("Before FixRoundabout/FixRoad."); // log time for benchmarking.
                bool isRoundabout = RoundaboutMassEdit.Instance.FixRoundabout(
                    HoveredSegmentId, out record_);
                if (!isRoundabout) {
                    record_ = PriorityRoad.FixRoad(HoveredSegmentId);
                }
                // TODO: benchmark why bulk setup takes a long time.
                Log.Info("After FixRoundabout/FixRoad. Before RefreshMassEditOverlay"); // log time for benchmarking.
                RefreshMassEditOverlay();
                Log.Info("After RefreshMassEditOverlay."); // log time for benchmarking.
            } else if (ControlIsPressed) {
                record_ = new TrafficRulesRecord();
                (record_ as TrafficRulesRecord).AddNodeAndSegmentEnds(HoveredNodeId);
                PriorityRoad.FixHighPriorityJunction(HoveredNodeId);
            }
            else if (ShiftIsPressed) {
                bool isRoundabout = RoundaboutMassEdit.Instance.TraverseLoop(HoveredSegmentId, out var segmentList);
                if (!isRoundabout) {
                    var segments = SegmentTraverser.Traverse(
                        HoveredSegmentId,
                        TraverseDirection.AnyDirection,
                        TraverseSide.Straight,
                        SegmentStopCriterion.None,
                        (_) => true);
                    segmentList = new List<ushort>(segments);
                }

                PriorityRoad.FixPrioritySigns(massEditMode, segmentList);
                record_ = null;
            } else {
                if (TrafficPriorityManager.Instance.HasNodePrioritySign(HoveredNodeId)) {
                    return;
                }

                if (!MayNodeHavePrioritySigns(HoveredNodeId)) {
                    return;
                }

                SelectedNodeId = HoveredNodeId;
                MainTool.RequestOnscreenDisplayUpdate();
                // Log._Debug($"PrioritySignsTool.OnPrimaryClickOverlay: SelectedNodeId={SelectedNodeId}");
            }

            // cycle mass edit mode
            if (ControlIsPressed) {
                massEditMode =
                    massEditMode != PrioritySignsMassEditMode.MainYield ?
                    PrioritySignsMassEditMode.MainYield :
                    PrioritySignsMassEditMode.Undo;
            } else if (ShiftIsPressed) {
                massEditMode++;
                if (massEditMode > PrioritySignsMassEditMode.Max) {
                    massEditMode = PrioritySignsMassEditMode.Min;
                }
            }

            // refresh cache
            if(ControlIsPressed)
                RefreshMassEditOverlay();
            else
                RefreshCurrentPriorityNodeIds();
        }

        public override void OnSecondaryClickOverlay() {
            MainTool.SetToolMode(ToolMode.None);
        }

        public override void OnToolGUI(Event e) { }

        /// <summary>
        /// refreshes all subtools influenced by mass edit.
        /// the mass edit overlay active while processing
        /// and remains active for one extra second so that
        /// </summary>
        private void RefreshMassEditOverlay() {
            // processing takes while.
            // Keep mass edit overlay active so that user has visual feedback
            // that something is happening.
            // this is also to make sure overlay is refreshed
            // even when the user lets go of the mass edit overlay hotkey.
            MassEditOverlay.SetTimer(float.MaxValue);

            ModUI.GetTrafficManagerTool()?.InitializeSubTools();
            RefreshCurrentPriorityNodeIds();

            // keep active for one more second so that the user
            // has a chance to see the new traffic rules.
            MassEditOverlay.SetTimer(1);
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (MainTool.GetToolController().IsInsideUI || !Cursor.visible) {
                return;
            }

            ModifyMode mode = ModifyMode.None;

            MassEditOverlay.Show = ControlIsPressed;

            if (HoveredSegmentId == 0) {
                massEditMode = PrioritySignsMassEditMode.Min;
                return;
            }

            if (Shortcuts.ShiftIsPressed) {
                bool isRoundabout = RoundaboutMassEdit.Instance.TraverseLoop(HoveredSegmentId, out var segmentList);
                Color color = MainTool.GetToolColor(Input.GetMouseButton(0), false);
                if (isRoundabout) {
                    foreach (ushort segmentId in segmentList) {
                        NetTool.RenderOverlay(
                            cameraInfo,
                            ref segmentId.ToSegment(),
                            color,
                            color);
                    } // end foreach
                } else {
                    SegmentTraverser.Traverse(
                        HoveredSegmentId,
                        TraverseDirection.AnyDirection,
                        TraverseSide.Straight,
                        SegmentStopCriterion.None,
                        data => {
                            NetTool.RenderOverlay(
                                cameraInfo,
                                ref data.CurSeg.segmentId.ToSegment(),
                                color,
                                color);
                            return true;
                        });
                }
                if (!ControlIsPressed)
                    mode = ModifyMode.PriorityRoad;
                else if (!isRoundabout)
                    mode = ModifyMode.HighPriorityRoad;
                else
                    mode = ModifyMode.Roundabout;

                if (mode != PrevHoveredState.Mode || HoveredSegmentId != PrevHoveredState.SegmentId) {
                    massEditMode = PrioritySignsMassEditMode.Min;
                }
            } else if (ControlIsPressed) {
                Highlight.DrawNodeCircle(
                    cameraInfo: cameraInfo,
                    nodeId: HoveredNodeId,
                    warning: Input.GetMouseButton(0));

                mode = ModifyMode.HighPriorityJunction;

                if (mode != PrevHoveredState.Mode || HoveredNodeId != PrevHoveredState.NodeId) {
                    massEditMode = PrioritySignsMassEditMode.Min;
                }
            } else {
                massEditMode = PrioritySignsMassEditMode.Min;

                if (HoveredNodeId == SelectedNodeId) {
                    return;
                }

                // no highlight for existing priority node in sign mode
                if (TrafficPriorityManager.Instance.HasNodePrioritySign(HoveredNodeId)) {
                    // Log._Debug($"PrioritySignsTool.RenderOverlay: HasNodePrioritySign({HoveredNodeId})=true");
                    return;
                }

                if (!TrafficPriorityManager.Instance.MayNodeHavePrioritySigns(HoveredNodeId)) {
                    // Log._Debug($"PrioritySignsTool.RenderOverlay: MayNodeHavePrioritySigns({HoveredNodeId})=false");
                    return;
                }

                Highlight.DrawNodeCircle(
                    cameraInfo: cameraInfo,
                    nodeId: HoveredNodeId,
                    warning: Input.GetMouseButton(0));
            }

            PrevHoveredState.Mode = mode;
            PrevHoveredState.SegmentId = HoveredSegmentId;
            PrevHoveredState.NodeId = HoveredNodeId;
        }

        private void RefreshCurrentPriorityNodeIds() {
            TrafficPriorityManager tpm = TrafficPriorityManager.Instance;

            currentPriorityNodeIds.Clear();
            for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                ref NetNode netNode = ref ((ushort)nodeId).ToNode();

                if (!netNode.IsValid()) {
                    continue;
                }

                if (!tpm.MayNodeHavePrioritySigns((ushort)nodeId)) {
                    continue;
                }

                if (!tpm.HasNodePrioritySign((ushort)nodeId) && nodeId != SelectedNodeId) {
                    continue;
                }

                /*if (! MainTool.IsNodeWithinViewDistance(nodeId)) {
                        continue;
                }*/

                currentPriorityNodeIds.Add((ushort)nodeId);
            }

            // Log._Debug($"PrioritySignsTool.RefreshCurrentPriorityNodeIds:
            //     currentPriorityNodeIds={string.Join(", ", currentPriorityNodeIds.Select(
            //     x => x.ToString()).ToArray())}");
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (viewOnly
                && !(SavedGameOptions.Instance.prioritySignsOverlay
                     || UI.SubTools.PrioritySigns.MassEditOverlay.IsActive)) {
                return;
            }

            if (ModUI.GetTrafficManagerTool()?.GetToolMode()
                == ToolMode.JunctionRestrictions)
            {
                return;
            }

            ShowGUI(viewOnly);
        }

        private void ShowGUI(bool viewOnly) {
            try {
                ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
                IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
                TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

                Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;

                bool clicked = !viewOnly && MainTool.CheckClicked();

                ushort removedNodeId = 0;
                bool showRemoveButton = false;

                foreach (ushort nodeId in currentPriorityNodeIds) {
                    ref NetNode netNode = ref nodeId.ToNode();

                    if (!netNode.IsValid()) {
                        continue;
                    }

                    if (!TrafficManagerTool.IsNodeWithinViewDistance(nodeId)) {
                        continue;
                    }

                    ref NetNode node = ref nodeId.ToNode();
                    Vector3 nodePos = node.m_position;

                    for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                        ushort segmentId = node.GetSegment(segmentIndex);
                        if (segmentId == 0) {
                            continue;
                        }

                        bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
                        ExtSegment seg = segMan.ExtSegments[segmentId];
                        ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];

                        if (seg.oneWay && segEnd.outgoing) {
                            continue;
                        }

                        // calculate sign position
                        ref NetSegment segment = ref segmentId.ToSegment();
                        Vector3 signPos = nodePos + (10f * (startNode ? segment.m_startDirection : segment.m_endDirection));

                        if (!GeometryUtil.WorldToScreenPoint(signPos, out Vector3 _)) {
                            continue;
                        }

                        // draw sign and handle input
                        PriorityType sign = prioMan.GetPrioritySign(segmentId, startNode);
                        if (viewOnly && sign == PriorityType.None) {
                            continue;
                        }

                        if (!viewOnly && sign != PriorityType.None) {
                            showRemoveButton = true;
                        }

                        if (Highlight.DrawGenericSquareOverlayTexture(
                                texture: RoadSignThemeManager.ActiveTheme.Priority(sign),
                                camPos: camPos,
                                worldPos: signPos,
                                size: 90f,
                                canHover: !viewOnly) && clicked)
                        {
                            PriorityType? newSign;
                            switch (sign) {
                                case PriorityType.Main: {
                                    newSign = PriorityType.Yield;
                                    break;
                                }

                                case PriorityType.Yield: {
                                    newSign = PriorityType.Stop;
                                    break;
                                }

                                case PriorityType.Stop: {
                                    newSign = PriorityType.Main;
                                    break;
                                }

                                // also: case PriorityType.None:
                                default: {
                                    newSign = prioMan.CountPrioritySignsAtNode(
                                                  nodeId,
                                                  PriorityType.Main) >= 2
                                                  ? PriorityType.Yield
                                                  : PriorityType.Main;
                                    break;
                                }
                            }

                            // newSign is never null here
                            SetPrioritySign(segmentId, startNode, (PriorityType)newSign);
                        } // draw sign
                    } // foreach segment end

                    if (viewOnly) {
                        continue;
                    }

                    // draw remove button and handle click
                    if (showRemoveButton
                        && Highlight.DrawHoverableSquareOverlayTexture(
                            texture: RoadUI.Instance.SignClear,
                            camPos: camPos,
                            worldPos: nodePos,
                            size: 90f)
                        && clicked)
                    {
                        prioMan.RemovePrioritySignsFromNode(nodeId);
                        Log._Debug($"PrioritySignsTool.ShowGUI: Removed priority signs from node {nodeId}");
                        removedNodeId = nodeId;
                    }
                } // foreach node

                if (removedNodeId != 0) {
                    currentPriorityNodeIds.Remove(removedNodeId);
                    SelectedNodeId = 0;
                    MainTool.RequestOnscreenDisplayUpdate();
                }
            } catch (Exception e) {
                Log.Error(e.ToString());
            }
        }

        private bool SetPrioritySign(ushort segmentId, bool startNode, PriorityType sign) {
            ref NetSegment netSegment = ref segmentId.ToSegment();
            ushort nodeId = startNode ? netSegment.m_startNode : netSegment.m_endNode;

            // check for restrictions
            if (!MayNodeHavePrioritySigns(nodeId)) {
                Log._Debug($"PrioritySignsTool.SetPrioritySign: MayNodeHavePrioritySigns({nodeId})=false");
                return false;
            }

            bool success = TrafficPriorityManager.Instance.SetPrioritySign(segmentId, startNode, sign);
            Log._Debug($"PrioritySignsTool.SetPrioritySign: SetPrioritySign({segmentId}, " +
                       $"{startNode}, {sign})={success}");

            if (!success || (sign != PriorityType.Stop && sign != PriorityType.Yield)) {
                return success;
            }

            // make all undefined segments a main road
            Log._Debug("PrioritySignsTool.SetPrioritySign: flagging remaining segments at node " +
                       $"{nodeId} as main road.");

            ref NetNode node = ref nodeId.ToNode();
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort otherSegmentId = node.GetSegment(segmentIndex);
                if (otherSegmentId == 0 || otherSegmentId == segmentId) {
                    continue;
                }

                bool otherStartNode = otherSegmentId.ToSegment().IsStartNode(nodeId);

                if (TrafficPriorityManager.Instance.GetPrioritySign(otherSegmentId, otherStartNode)
                    == PriorityType.None)
                {
                    Log._Debug("PrioritySignsTool.SetPrioritySign: setting main priority sign " +
                               $"for segment {otherSegmentId} @ {nodeId}");
                    TrafficPriorityManager.Instance.SetPrioritySign(
                        otherSegmentId,
                        otherStartNode,
                        PriorityType.Main);
                }
            }

            return success;
        }

        public override void Cleanup() {
        }

        public override void OnActivate() {
            base.OnActivate();
            RefreshCurrentPriorityNodeIds();
            MainTool.RequestOnscreenDisplayUpdate();
        }

        public override void Initialize() {
            base.Initialize();
            Cleanup();

            if (SavedGameOptions.Instance.prioritySignsOverlay
                || UI.SubTools.PrioritySigns.MassEditOverlay.IsActive) {
                RefreshCurrentPriorityNodeIds();
            } else {
                currentPriorityNodeIds.Clear();
            }
        }

        private bool MayNodeHavePrioritySigns(ushort nodeId) {
            SetPrioritySignError reason;

            if (!TrafficPriorityManager.Instance.MayNodeHavePrioritySigns(nodeId, out reason)) {
                if (reason == SetPrioritySignError.HasTimedLight) {
                    MainTool.WarningPrompt(
                        Translation.TrafficLights.Get("Dialog.Text:Node has timed TL script"));
                }

                return false;
            }

            return true;
        }

        private static string T(string key) => Translation.PrioritySigns.Get(key);

        public void UpdateOnscreenDisplayPanel() {
            if (SelectedNodeId == 0) {
                // Select mode
                var items = new List<OsdItem>();
                items.Add(new Label(localizedText: T("Prio.OnscreenHint.Mode:Select")));
                items.Add(
                    new HardcodedMouseShortcut(
                        button: UIMouseButton.Left,
                        shift: false,
                        ctrl: true,
                        alt: false,
                        localizedText: T("Prio.Click:Quick setup prio junction")));
                items.Add(
                    new HardcodedMouseShortcut(
                        button: UIMouseButton.Left,
                        shift: true,
                        ctrl: false,
                        alt: false,
                        localizedText: T("Prio.Click:Quick setup prio road/roundabout")));
                items.Add(
                    new HardcodedMouseShortcut(
                        button: UIMouseButton.Left,
                        shift: true,
                        ctrl: true,
                        alt: false,
                        localizedText: T("Prio.Click:Quick setup high prio road/roundabout")));
                OnscreenDisplay.Display(items);
            } else {
                // Modify traffic light settings
                var items = new List<OsdItem>();
                items.Add(new Label(localizedText: T("Prio.OnscreenHint.Mode:Edit")));
                // items.Add(OnscreenDisplay.RightClick_LeaveNode());
                OnscreenDisplay.Display(items);
            }
        }
    }
}
