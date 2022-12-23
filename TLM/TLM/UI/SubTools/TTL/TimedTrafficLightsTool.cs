namespace TrafficManager.UI.SubTools.TTL {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.TrafficLight.Impl;
    using TrafficManager.U;
    using TrafficManager.UI.Helpers;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    public class TimedTrafficLightsTool
        : LegacySubTool,
          UI.MainMenu.IOnscreenDisplayProvider {
        private static float minSensitivity_ = 0.001f;
        private static float maxSensitivity_ = 10;
        private static float logMinSensitivity_ = Mathf.Log10(minSensitivity_);
        private static float logMaxSensitivity_ = Mathf.Log10(maxSensitivity_);

        private readonly GUI.WindowFunction _guiTimedControlPanelDelegate;
        private readonly GUI.WindowFunction _guiTimedTrafficLightsNodeWindowDelegate;
        private readonly GUI.WindowFunction _guiTimedTrafficLightsPasteWindowDelegate;

        private TTLToolMode ttlToolMode_ = TTLToolMode.SelectNode;

        private readonly GUIStyle _counterStyle = new GUIStyle();
        private readonly int[] _hoveredButton = new int[2];
        private bool nodeSelectionLocked;
        private List<ushort> selectedNodeIds = new List<ushort>();
        private bool _cursorInSecondaryPanel;
        private Rect _windowRect = TrafficManagerTool.GetDefaultScreenPositionForRect(new Rect(0, 0, 480, 350));
        private Rect _windowRect2 = TrafficManagerTool.GetDefaultScreenPositionForRect(new Rect(0, 0, 300, 150));
        private bool _timedPanelAdd;
        private int _timedEditStep = -1;
        private ushort _hoveredNode;
        private bool _timedShowNumbers;
        private int _timedViewedStep = -1;
        private int _stepMinValue = 1;
        private int _stepMaxValue = 1;
        private StepChangeMetric _stepMetric = StepChangeMetric.Default;
        private float _waitFlowBalance = GlobalConfig.Instance.TimedTrafficLights.FlowToWaitRatio;
        private string _stepMinValueStr = "1";
        private string _stepMaxValueStr = "1";
        private bool timedLightActive;
        private int currentStep = -1;
        private int numSteps;
        private bool inTestMode;
        private ushort nodeIdToCopy;
        private HashSet<ushort> currentTimedNodeIds;

        private GUIStyle layout = new GUIStyle { normal = { textColor = new Color(1f, 1f, 1f) } };
        private GUIStyle layoutRed = new GUIStyle { normal = { textColor = new Color(1f, 0f, 0f) } };
        private GUIStyle layoutGreen = new GUIStyle { normal = { textColor = new Color(0f, .8f, 0f) } };
        private GUIStyle layoutYellow = new GUIStyle { normal = { textColor = new Color(1f, .7f, 0f) } };

        public TimedTrafficLightsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            _guiTimedControlPanelDelegate = GuiTimedControlPanel;
            _guiTimedTrafficLightsNodeWindowDelegate = GuiTimedTrafficLightsNodeWindow;
            _guiTimedTrafficLightsPasteWindowDelegate = GuiTimedTrafficLightsPasteWindow;

            currentTimedNodeIds = new HashSet<ushort>();
        }

        public override bool IsCursorInPanel() {
            return base.IsCursorInPanel() || _cursorInSecondaryPanel;
        }

        private void RefreshCurrentTimedNodeIds(ushort forceNodeId = 0) {
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            if (forceNodeId == 0) {
                currentTimedNodeIds.Clear();
            } else {
                currentTimedNodeIds.Remove(forceNodeId);
            }

            for (uint nodeId = forceNodeId == 0 ? 1u : forceNodeId;
                 nodeId <= (forceNodeId == 0 ? NetManager.MAX_NODE_COUNT - 1 : forceNodeId);
                 ++nodeId) {
                ref NetNode netNode = ref ((ushort)nodeId).ToNode();

                if (!netNode.IsValid()) {
                    continue;
                }

                if (tlsMan.HasTimedSimulation((ushort)nodeId)) {
                    currentTimedNodeIds.Add((ushort)nodeId);
                }
            }
        }

        public override void OnActivate() {
            base.OnActivate();

            SetToolMode(TTLToolMode.SelectNode);
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            RefreshCurrentTimedNodeIds();

            nodeSelectionLocked = false;
            foreach (ushort nodeId in currentTimedNodeIds) {
                ref NetNode netNode = ref nodeId.ToNode();

                if (!netNode.IsValid()) {
                    continue;
                }

                tlsMan.TrafficLightSimulations[nodeId].Housekeeping();
            }

            MainTool.RequestOnscreenDisplayUpdate();
        }

        public override void OnSecondaryClickOverlay() {
            if (!IsCursorInPanel()) {
                if (ttlToolMode_ != TTLToolMode.SelectNode) {
                    Cleanup();
                    SetToolMode(TTLToolMode.SelectNode);
                } else {
                    MainTool.SetToolMode(ToolMode.None);
                }
            }
        }

        private void SetToolMode(TTLToolMode t) {
            ttlToolMode_ = t;
        }

        public override void OnPrimaryClickOverlay() {
            if (HoveredNodeId <= 0 || nodeSelectionLocked || !Flags.MayHaveTrafficLight(HoveredNodeId)) {
                return;
            }
            if (Shortcuts.ControlIsPressed) {
                AutoTimedTrafficLights.ErrorResult res;
                if (Shortcuts.AltIsPressed) {
                    res = AutoTimedTrafficLights.SetupSingleFar(HoveredNodeId);
                } else {
                    res = AutoTimedTrafficLights.Setup(HoveredNodeId);
                }
                string message = null;

                Log._Debug(res.ToString());
                switch (res) {
                    case AutoTimedTrafficLights.ErrorResult.NotSupported:
                        MainTool.Guide.Activate("TimedTrafficLightsTool_Auto TL no need");
                        break;
                    case AutoTimedTrafficLights.ErrorResult.Success:
                        MainTool.Guide.Deactivate("TimedTrafficLightsTool_Auto TL no need");
                        break;
                    case AutoTimedTrafficLights.ErrorResult.TTLExists:
                        message = "Dialog.Text:Node has timed TL script";
                        break;
                    default: //Unreachable code
                        message = $"error = {res}";
                        break;
                }
                if (message != null) {
                    message =
                        Translation.TrafficLights.Get("Dialog.Text:Auto TL create failed because") +
                        "\n" +
                        Translation.TrafficLights.Get(message);
                    MainTool.WarningPrompt(message);
                    return;
                }
                RefreshCurrentTimedNodeIds(HoveredNodeId);
                SetToolMode(TTLToolMode.ShowLights);
            }

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            switch (ttlToolMode_) {
                case TTLToolMode.SelectNode:
                case TTLToolMode.ShowLights: {
                        if (ttlToolMode_ == TTLToolMode.ShowLights) {
                            SetToolMode(TTLToolMode.SelectNode);
                            ClearSelectedNodes();
                        }

                        if (!tlsMan.HasTimedSimulation(HoveredNodeId)) {
                            if (IsNodeSelected(HoveredNodeId)) {
                                RemoveSelectedNode(HoveredNodeId);
                            } else {
                                AddSelectedNode(HoveredNodeId);
                            }
                        } else {
                            if (selectedNodeIds.Count == 0) {
                                // timedSim.housekeeping();
                                TimedTrafficLights timedLight =
                                    tlsMan.TrafficLightSimulations[HoveredNodeId].timedLight;

                                if (timedLight != null) {
                                    selectedNodeIds = new List<ushort>(timedLight.NodeGroup);
                                    SetToolMode(TTLToolMode.ShowLights);
                                }
                            } else {
                                MainTool.WarningPrompt(T("Dialog.Text:Node has timed TL script"));
                            }
                        }

                        break;
                    }

                case TTLToolMode.AddNode: {
                        if (selectedNodeIds.Count <= 0) {
                            SetToolMode(TTLToolMode.SelectNode);
                            return;
                        }

                        if (selectedNodeIds.Contains(HoveredNodeId)) {
                            return;
                        }

                        // bool mayEnterBlocked = SavedGameOptions.Instance.mayEnterBlockedJunctions;
                        TimedTrafficLights existingTimedLight = null;
                        foreach (ushort nodeId in selectedNodeIds) {
                            if (!tlsMan.HasTimedSimulation(nodeId)) {
                                continue;
                            }

                            // mayEnterBlocked = timedNode.vehiclesMayEnterBlockedJunctions;
                            existingTimedLight = tlsMan.TrafficLightSimulations[nodeId].timedLight;
                        }

                        // if (timedSim2 != null)
                        //     timedSim2.housekeeping();
                        TimedTrafficLights timedLight2;

                        if (!tlsMan.HasTimedSimulation(HoveredNodeId)) {
                            var nodeGroup = new List<ushort>();
                            nodeGroup.Add(HoveredNodeId);
                            tlsMan.SetUpTimedTrafficLight(HoveredNodeId, nodeGroup);
                        }

                        timedLight2 = tlsMan.TrafficLightSimulations[HoveredNodeId].timedLight;
                        timedLight2.Join(existingTimedLight);
                        ClearSelectedNodes();

                        foreach (ushort nodeId in timedLight2.NodeGroup) {
                            RefreshCurrentTimedNodeIds(nodeId);
                            AddSelectedNode(nodeId);
                        }

                        SetToolMode(TTLToolMode.ShowLights);
                        break;
                    }

                case TTLToolMode.RemoveNode: {
                        if (selectedNodeIds.Count <= 0) {
                            SetToolMode(TTLToolMode.SelectNode);
                            return;
                        }

                        if (selectedNodeIds.Contains(HoveredNodeId)) {
                            tlsMan.RemoveNodeFromSimulation(
                                nodeId: HoveredNodeId,
                                destroyGroup: false,
                                removeTrafficLight: false);
                            RefreshCurrentTimedNodeIds(HoveredNodeId);
                        }

                        RemoveSelectedNode(HoveredNodeId);
                        SetToolMode(TTLToolMode.ShowLights);
                        break;
                    }

                case TTLToolMode.CopyLights: {
                        if (nodeIdToCopy == 0 || !tlsMan.HasTimedSimulation(nodeIdToCopy)) {
                            SetToolMode(TTLToolMode.SelectNode);
                            return;
                        }

                        // compare geometry
                        int numSourceSegments = nodeIdToCopy.ToNode().CountSegments();
                        int numTargetSegments = HoveredNodeId.ToNode().CountSegments();

                        if (numSourceSegments != numTargetSegments) {
                            MainTool.WarningPrompt(
                                T("Dialog.Text:Incompatible traffic light script"));
                            return;
                        }

                        // check for existing simulation
                        if (tlsMan.HasTimedSimulation(HoveredNodeId)) {
                            MainTool.WarningPrompt(
                                T("Dialog.Text:Node has timed TL script"));
                            return;
                        }

                        TimedTrafficLights sourceTimedLights =
                            tlsMan.TrafficLightSimulations[nodeIdToCopy].timedLight;

                        // copy `nodeIdToCopy` to `HoveredNodeId`
                        tlsMan.SetUpTimedTrafficLight(
                            HoveredNodeId,
                            new List<ushort> { HoveredNodeId });

                        tlsMan.TrafficLightSimulations[HoveredNodeId].timedLight
                              .PasteSteps(sourceTimedLights);
                        RefreshCurrentTimedNodeIds(HoveredNodeId);

                        Cleanup();
                        AddSelectedNode(HoveredNodeId);
                        SetToolMode(TTLToolMode.ShowLights);
                        break;
                    }
            }
        }

        public override void OnToolGUI(Event e) {
            base.OnToolGUI(e);

            switch (ttlToolMode_) {
                case TTLToolMode.SelectNode: {
                        if (!Shortcuts.ControlIsPressed) {
                            GuiTimedTrafficLightsNode();
                        }
                        break;
                    }

                case TTLToolMode.ShowLights:
                case TTLToolMode.AddNode:
                case TTLToolMode.RemoveNode: {
                        GuiTimedTrafficLights();
                        break;
                    }

                case TTLToolMode.CopyLights: {
                        GuiTimedTrafficLightsCopy();
                        break;
                    }
            }

            if (_cursorInSecondaryPanel) {
                UIInput.MouseUsed();
            }
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            bool onlySelected = ttlToolMode_ == TTLToolMode.RemoveNode;

            // Log._Debug($"nodeSelLocked={nodeSelectionLocked} HoveredNodeId={HoveredNodeId}
            //     IsNodeSelected={IsNodeSelected(HoveredNodeId)} onlySelected={onlySelected}
            //     isinsideui={MainTool.GetToolController().IsInsideUI} cursorVis={Cursor.visible}");
            if (!nodeSelectionLocked
                && HoveredNodeId != 0
                && !IsNodeSelected(HoveredNodeId) ^ onlySelected
                && !MainTool.GetToolController().IsInsideUI
                && Cursor.visible
                && Flags.MayHaveTrafficLight(HoveredNodeId)) {
                Highlight.DrawNodeCircle(cameraInfo, HoveredNodeId);
            }

            if (selectedNodeIds.Count <= 0) {
                return;
            }

            foreach (ushort index in selectedNodeIds) {
                Highlight.DrawNodeCircle(cameraInfo, index, true);
            }
        }

        private void GuiTimedControlPanel(int num) {
            // Log._Debug("guiTimedControlPanel");
            try {
                TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

                if (ttlToolMode_ == TTLToolMode.AddNode ||
                    ttlToolMode_ == TTLToolMode.RemoveNode) {
                    GUILayout.Label(T("TTL.Label:Select junction"), EmptyOptionsArray);
                    if (GUILayout.Button(T("Button:Cancel"), EmptyOptionsArray)) {
                        SetToolMode(TTLToolMode.ShowLights);
                    } else {
                        DragWindow(ref _windowRect);
                        return;
                    }
                }

                if (!tlsMan.HasTimedSimulation(selectedNodeIds[0])) {
                    SetToolMode(TTLToolMode.SelectNode);
                    // Log._Debug("nodesim or timednodemain is null");
                    DragWindow(ref _windowRect);
                    return;
                }

                TimedTrafficLights timedNodeMain = tlsMan.TrafficLightSimulations[selectedNodeIds[0]].timedLight;

                if (Event.current.type == EventType.Layout) {
                    timedLightActive = tlsMan.HasActiveTimedSimulation(selectedNodeIds[0]);
                    currentStep = timedNodeMain.CurrentStep;
                    inTestMode = timedNodeMain.IsInTestMode();
                    numSteps = timedNodeMain.NumSteps();
                }

                if (!timedLightActive && numSteps > 0 && !_timedPanelAdd && _timedEditStep < 0 &&
                    _timedViewedStep < 0) {
                    _timedViewedStep = 0;
                    foreach (ushort nodeId in selectedNodeIds) {
                        tlsMan.TrafficLightSimulations[nodeId].timedLight?.GetStep(_timedViewedStep)
                              .UpdateLiveLights(true);
                    }
                }

                for (var i = 0; i < timedNodeMain.NumSteps(); i++) {
                    GUILayout.BeginHorizontal(EmptyOptionsArray);

                    if (_timedEditStep != i) {
                        if (timedLightActive) {
                            if (i == currentStep) {
                                GUILayout.BeginVertical(EmptyOptionsArray);
                                GUILayout.Space(5);

                                string labelStr = string.Format(
                                    T("Format:State {0}: (min/max) {1}..{2}"),
                                    i + 1,
                                    timedNodeMain.GetStep(i).MinTimeRemaining(),
                                    timedNodeMain.GetStep(i).MaxTimeRemaining());

                                float flow = float.NaN;
                                float wait = float.NaN;

                                if (inTestMode) {
                                    try {
                                        timedNodeMain
                                            .GetStep(timedNodeMain.CurrentStep).CalcWaitFlow(
                                                true,
                                                timedNodeMain.CurrentStep,
                                                out wait,
                                                out flow);
                                    }
                                    catch (Exception e) {
                                        Log.Warning(
                                            "calcWaitFlow in UI: This is not thread-safe: " +
                                            e.ToString());
                                    }
                                } else {
                                    wait = timedNodeMain.GetStep(i).CurrentWait;
                                    flow = timedNodeMain.GetStep(i).CurrentFlow;
                                }

                                if (!float.IsNaN(flow) && !float.IsNaN(wait)) {
                                    labelStr += string.Format(
                                        T("Format:Avg. flow: {0:0.##} avg. wait: {1:0.##}"),
                                        flow,
                                        wait);
                                }

                                GUIStyle labelLayout;
                                if (inTestMode && !float.IsNaN(wait) && !float.IsNaN(flow)) {
                                    labelLayout = timedNodeMain.GetStep(i)
                                                               .ShouldGoToNextStep(flow, wait, out float metric)
                                                      ? layoutRed : layoutGreen;
                                } else {
                                    bool inEndTransition = false;
                                    try {
                                        inEndTransition = timedNodeMain.GetStep(i).IsInEndTransition();
                                    }
                                    catch (Exception e) {
                                        Log.Error("Error while determining if timed traffic light " +
                                                  "is in end transition: " + e);
                                    }

                                    labelLayout = inEndTransition ? layoutYellow : layoutGreen;
                                }

                                GUILayout.Label(labelStr, labelLayout, EmptyOptionsArray);
                                GUILayout.Space(5);
                                GUILayout.EndVertical();

                                if (GUILayout.Button(
                                    T("TTL.Button:Skip"),
                                    GUILayout.Width(80))) {
                                    foreach (ushort nodeId in selectedNodeIds) {
                                        tlsMan.TrafficLightSimulations[nodeId]
                                              .timedLight
                                              ?.SkipStep();
                                    }
                                }
                            } else {
                                GUILayout.Label(
                                    string.Format(
                                        T("Format:State {0}: {1}..{2}"),
                                        i + 1,
                                        timedNodeMain.GetStep(i).MinTime,
                                        timedNodeMain.GetStep(i).MaxTime),
                                    layout,
                                    EmptyOptionsArray);
                            }
                        } else {
                            GUIStyle labelLayout = layout;

                            if (_timedViewedStep == i) {
                                labelLayout = layoutGreen;
                            }

                            GUILayout.Label(
                                string.Format(
                                    T("Format:State {0}: {1}..{2}"),
                                    i + 1,
                                    timedNodeMain.GetStep(i).MinTime,
                                    timedNodeMain.GetStep(i).MaxTime),
                                labelLayout,
                                EmptyOptionsArray);

                            if (_timedEditStep < 0) {
                                GUILayout.BeginHorizontal(GUILayout.Width(100));

                                if (i > 0) {
                                    if (GUILayout.Button(
                                        T("TTL.Button:up"),
                                        GUILayout.Width(48))) {
                                        foreach (ushort nodeId in selectedNodeIds) {
                                            tlsMan.TrafficLightSimulations[nodeId].timedLight
                                                  ?.MoveStep(i, i - 1);
                                        }

                                        _timedViewedStep = i - 1;
                                    }
                                } else {
                                    GUILayout.Space(50);
                                }

                                if (i < numSteps - 1) {
                                    if (GUILayout.Button(
                                        T("TTL.Button:down"),
                                        GUILayout.Width(48))) {
                                        foreach (ushort nodeId in selectedNodeIds) {
                                            tlsMan.TrafficLightSimulations[nodeId].timedLight
                                                  ?.MoveStep(i, i + 1);
                                        }

                                        _timedViewedStep = i + 1;
                                    }
                                } else {
                                    GUILayout.Space(50);
                                }

                                GUILayout.EndHorizontal();

                                GUI.color = Color.red;
                                if (GUILayout.Button(
                                    T("Button:Delete"),
                                    GUILayout.Width(70))) {
                                    RemoveStep(i);
                                }

                                GUI.color = Color.white;

                                if (GUILayout.Button(
                                    T("Button:Edit"),
                                    GUILayout.Width(65))) {
                                    _timedPanelAdd = false;
                                    _timedEditStep = i;
                                    _timedViewedStep = -1;
                                    _stepMinValue = timedNodeMain.GetStep(i).MinTime;
                                    _stepMaxValue = timedNodeMain.GetStep(i).MaxTime;
                                    _stepMetric = timedNodeMain.GetStep(i).ChangeMetric;
                                    _waitFlowBalance = timedNodeMain.GetStep(i).WaitFlowBalance;
                                    _stepMinValueStr = _stepMinValue.ToString();
                                    _stepMaxValueStr = _stepMaxValue.ToString();
                                    nodeSelectionLocked = true;

                                    foreach (ushort nodeId in selectedNodeIds) {
                                        tlsMan.TrafficLightSimulations[nodeId]
                                              .timedLight
                                              ?.GetStep(i)
                                              .UpdateLiveLights(true);
                                    }
                                }

                                if (GUILayout.Button(
                                    T("Button:View"),
                                    GUILayout.Width(70))) {
                                    _timedPanelAdd = false;
                                    _timedViewedStep = i;

                                    foreach (ushort nodeId in selectedNodeIds) {
                                        tlsMan.TrafficLightSimulations[nodeId]
                                              .timedLight
                                              ?.GetStep(i)
                                              .UpdateLiveLights(true);
                                    }
                                }
                            }
                        }
                    } else {
                        nodeSelectionLocked = true;
                        int oldStepMinValue = _stepMinValue;
                        int oldStepMaxValue = _stepMaxValue;

                        // Editing step
                        GUILayout.Label(
                            T("TTL.Label:Min. time:"),
                            GUILayout.Width(75));

                        _stepMinValueStr = GUILayout.TextField(
                            _stepMinValueStr,
                            GUILayout.Height(20));

                        if (!int.TryParse(_stepMinValueStr, out _stepMinValue)) {
                            _stepMinValue = oldStepMinValue;
                        }

                        GUILayout.Label(
                            T("TTL.Label:Max. time:"),
                            GUILayout.Width(75));

                        _stepMaxValueStr = GUILayout.TextField(
                            _stepMaxValueStr,
                            GUILayout.Height(20));

                        if (!int.TryParse(_stepMaxValueStr, out _stepMaxValue)) {
                            _stepMaxValue = oldStepMaxValue;
                        }

                        if (GUILayout.Button(
                            T("Button:Save"),
                            GUILayout.Width(70))) {
                            if (_stepMinValue < 0) {
                                _stepMinValue = 0;
                            }

                            if (_stepMaxValue <= 0) {
                                _stepMaxValue = 1;
                            }

                            if (_stepMaxValue < _stepMinValue) {
                                _stepMaxValue = _stepMinValue;
                            }

                            if (_waitFlowBalance <= 0) {
                                _waitFlowBalance = GlobalConfig
                                                   .Instance.TimedTrafficLights.FlowToWaitRatio;
                            }

                            foreach (ushort nodeId in selectedNodeIds) {
                                TimedTrafficLightsStep step = tlsMan
                                                               .TrafficLightSimulations[nodeId]
                                                               .timedLight
                                                               ?.GetStep(_timedEditStep);

                                if (step != null) {
                                    step.MinTime = _stepMinValue;
                                    step.MaxTime = _stepMaxValue;
                                    step.ChangeMetric = _stepMetric;
                                    step.WaitFlowBalance = _waitFlowBalance;
                                    step.UpdateLights();
                                }
                            }

                            _timedViewedStep = _timedEditStep;
                            _timedEditStep = -1;
                            nodeSelectionLocked = false;
                        }

                        GUILayout.EndHorizontal();

                        BuildStepChangeMetricDisplay(true);
                        BuildFlowPolicyDisplay(true);
                        GUILayout.BeginHorizontal(EmptyOptionsArray);
                    }

                    GUILayout.EndHorizontal();
                } // foreach step

                GUILayout.BeginHorizontal(EmptyOptionsArray);

                if (_timedEditStep < 0 && !timedLightActive) {
                    if (_timedPanelAdd) {
                        nodeSelectionLocked = true;

                        // new step
                        int oldStepMinValue = _stepMinValue;
                        int oldStepMaxValue = _stepMaxValue;

                        GUILayout.Label(
                            T("TTL.Label:Min. time:"),
                            GUILayout.Width(65));

                        _stepMinValueStr = GUILayout.TextField(_stepMinValueStr, GUILayout.Height(20));

                        if (!int.TryParse(_stepMinValueStr, out _stepMinValue)) {
                            _stepMinValue = oldStepMinValue;
                        }

                        GUILayout.Label(
                            T("TTL.Label:Max. time:"),
                            GUILayout.Width(65));

                        _stepMaxValueStr = GUILayout.TextField(_stepMaxValueStr, GUILayout.Height(20));

                        if (!int.TryParse(_stepMaxValueStr, out _stepMaxValue)) {
                            _stepMaxValue = oldStepMaxValue;
                        }

                        if (GUILayout.Button("X", GUILayout.Width(22))) {
                            _timedPanelAdd = false;
                        }

                        if (GUILayout.Button(
                            T("Button:Add"),
                            GUILayout.Width(70))) {
                            TrafficManagerTool.ShowAdvisor(GetType().Name + "_AddStep");
                            if (_stepMinValue < 0) {
                                _stepMinValue = 0;
                            }

                            if (_stepMaxValue <= 0) {
                                _stepMaxValue = 1;
                            }

                            if (_stepMaxValue < _stepMinValue) {
                                _stepMaxValue = _stepMinValue;
                            }

                            if (_waitFlowBalance <= 0) {
                                _waitFlowBalance = 1f;
                            }

                            foreach (ushort nodeId in selectedNodeIds) {
                                tlsMan.TrafficLightSimulations[nodeId].timedLight?.AddStep(
                                    _stepMinValue,
                                    _stepMaxValue,
                                    _stepMetric,
                                    _waitFlowBalance);
                            }

                            _timedPanelAdd = false;
                            _timedViewedStep = timedNodeMain.NumSteps() - 1;
                        }

                        GUILayout.EndHorizontal();

                        BuildStepChangeMetricDisplay(true);
                        BuildFlowPolicyDisplay(true);
                        GUILayout.BeginHorizontal(EmptyOptionsArray);
                    } else {
                        if (_timedEditStep < 0) {
                            if (GUILayout.Button(T("TTL.Button:Add step"), EmptyOptionsArray)) {
                                TrafficManagerTool.ShowAdvisor(GetType().Name + "_AddStep");
                                _timedPanelAdd = true;
                                nodeSelectionLocked = true;
                                _timedViewedStep = -1;
                                _timedEditStep = -1;
                                _stepMetric = StepChangeMetric.Default;
                            }
                        }
                    }
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                if (numSteps > 1 && _timedEditStep < 0) {
                    if (timedLightActive) {
                        if (GUILayout.Button(
                            _timedShowNumbers
                                ? T("TTL.Button:Hide counters")
                                : T("TTL.Button:Show counters"),
                            EmptyOptionsArray)) {
                            _timedShowNumbers = !_timedShowNumbers;
                        }

                        if (GUILayout.Button(T("TTL.Button:Stop"), EmptyOptionsArray)) {
                            foreach (ushort nodeId in selectedNodeIds) {
                                tlsMan.TrafficLightSimulations[nodeId]
                                      .timedLight
                                      ?.Stop();
                            }
                        }

                        // bool isInTestMode = false;
                        // foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
                        //     if (sim.TimedLight.IsInTestMode()) {
                        //         isInTestMode = true;
                        //         break;
                        //     }
                        // }
                        int curStep = timedNodeMain.CurrentStep;
                        TimedTrafficLightsStep currentStep = timedNodeMain.GetStep(curStep);
                        _stepMetric = currentStep.ChangeMetric;

                        if (currentStep.MaxTime > currentStep.MinTime) {
                            BuildStepChangeMetricDisplay(false);
                        }

                        _waitFlowBalance = timedNodeMain.GetStep(curStep).WaitFlowBalance;
                        BuildFlowPolicyDisplay(inTestMode);

                        foreach (ushort nodeId in selectedNodeIds) {
                            TimedTrafficLightsStep step = tlsMan
                                                           .TrafficLightSimulations[nodeId]
                                                           .timedLight?.GetStep(curStep);
                            if (step != null) {
                                step.WaitFlowBalance = _waitFlowBalance;
                            }
                        }

                        // var mayEnterIfBlocked = GUILayout.Toggle(
                        //     timedNodeMain.vehiclesMayEnterBlockedJunctions,
                        //     Translation.GetString("Vehicles_may_enter_blocked_junctions"),
                        //     new GUILayoutOption[] { });
                        bool testMode = GUILayout.Toggle(
                            inTestMode,
                            T("TTL.Checkbox:Pause in this step"),
                            EmptyOptionsArray);

                        foreach (ushort nodeId in selectedNodeIds) {
                            tlsMan.TrafficLightSimulations[nodeId].timedLight?.SetTestMode(testMode);
                        }
                    } else {
                        if (_timedEditStep < 0 && !_timedPanelAdd) {
                            if (GUILayout.Button(T("TTL.Button:Start"), EmptyOptionsArray)) {
                                _timedPanelAdd = false;
                                nodeSelectionLocked = false;

                                foreach (ushort nodeId in selectedNodeIds) {
                                    tlsMan.TrafficLightSimulations[nodeId]
                                          .timedLight
                                          ?.Start();
                                }
                            }
                        }
                    }
                }

                if (_timedEditStep >= 0) {
                    DragWindow(ref _windowRect);
                    return;
                }

                GUILayout.Space(30);

                if (selectedNodeIds.Count == 1 && timedNodeMain.NumSteps() > 0) {
                    GUILayout.BeginHorizontal(EmptyOptionsArray);

                    if (GUILayout.Button(T("TTL.Button:Rotate left"), EmptyOptionsArray)) {
                        timedNodeMain.RotateLeft();
                        _timedViewedStep = 0;
                    }

                    if (GUILayout.Button(T("TTL.Button:Copy"), EmptyOptionsArray)) {
                        TrafficManagerTool.ShowAdvisor(GetType().Name + "_Copy");
                        nodeIdToCopy = selectedNodeIds[0];
                        SetToolMode(TTLToolMode.CopyLights);
                    }

                    if (GUILayout.Button(T("TTL.Button:Rotate right"), EmptyOptionsArray)) {
                        timedNodeMain.RotateRight();
                        _timedViewedStep = 0;
                    }

                    GUILayout.EndHorizontal();
                }

                if (!timedLightActive) {
                    GUILayout.Space(30);

                    if (GUILayout.Button(
                        T("TTL.Button:Add junction to TTL"),
                        EmptyOptionsArray)) {
                        TrafficManagerTool.ShowAdvisor(GetType().Name + "_AddJunction");
                        SetToolMode(TTLToolMode.AddNode);
                    }

                    if (selectedNodeIds.Count > 1) {
                        if (GUILayout.Button(
                            T("TTL.Button:Remove junction from TTL"),
                            EmptyOptionsArray)) {
                            TrafficManagerTool.ShowAdvisor(GetType().Name + "_RemoveJunction");
                            SetToolMode(TTLToolMode.RemoveNode);
                        }
                    }

                    GUILayout.Space(30);

                    if (GUILayout.Button(T("TTL.Button:Remove entire TTL"), EmptyOptionsArray)) {
                        DisableTimed();
                        ClearSelectedNodes();
                        SetToolMode(TTLToolMode.SelectNode);
                    }
                }

                DragWindow(ref _windowRect);
            }
            catch (Exception e) {
                Log.Error($"TimedTrafficLightsTool._guiTimedControlPanel: {e}");
            }
        }

        private void RemoveStep(int stepIndex) {
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            _timedPanelAdd = false;
            _timedViewedStep = -1;

            foreach (ushort nodeId in selectedNodeIds) {
                tlsMan.TrafficLightSimulations[nodeId].timedLight?.RemoveStep(stepIndex);
            }
        }

        public override void Cleanup() {
            SelectedNodeId = 0;
            ClearSelectedNodes();

            _timedShowNumbers = false;
            _timedPanelAdd = false;
            _timedEditStep = -1;
            _hoveredNode = 0;
            _timedShowNumbers = false;
            _timedViewedStep = -1;
            timedLightActive = false;
            nodeIdToCopy = 0;
        }

        public override void Initialize() {
            base.Initialize();
            Cleanup();

            if (SavedGameOptions.Instance.timedLightsOverlay) {
                RefreshCurrentTimedNodeIds();
            } else {
                currentTimedNodeIds.Clear();
            }
        }

        private void BuildStepChangeMetricDisplay(bool editable) {
            GUILayout.BeginVertical();

            if (editable) {
                GUILayout.Label(
                    T("TTL.Label:After min. time go to next step if") + ":",
                    EmptyOptionsArray);

                if (GUILayout.Toggle(
                    _stepMetric == StepChangeMetric.Default,
                    GetStepChangeMetricDescription(StepChangeMetric.Default),
                    EmptyOptionsArray)) {
                    _stepMetric = StepChangeMetric.Default;
                }

                if (GUILayout.Toggle(
                    _stepMetric == StepChangeMetric.NoFlow,
                    GetStepChangeMetricDescription(StepChangeMetric.NoFlow),
                    EmptyOptionsArray)) {
                    _stepMetric = StepChangeMetric.NoFlow;
                }

                if (GUILayout.Toggle(
                    _stepMetric == StepChangeMetric.FirstWait,
                    GetStepChangeMetricDescription(StepChangeMetric.FirstWait),
                    EmptyOptionsArray)) {
                    _stepMetric = StepChangeMetric.FirstWait;
                }

                if (GUILayout.Toggle(
                    _stepMetric == StepChangeMetric.FirstFlow,
                    GetStepChangeMetricDescription(StepChangeMetric.FirstFlow),
                    EmptyOptionsArray)) {
                    _stepMetric = StepChangeMetric.FirstFlow;
                }

                if (GUILayout.Toggle(
                    _stepMetric == StepChangeMetric.NoWait,
                    GetStepChangeMetricDescription(StepChangeMetric.NoWait),
                    EmptyOptionsArray)) {
                    _stepMetric = StepChangeMetric.NoWait;
                }
            } else {
                GUILayout.Label(
                    T("TTL.Label:Adaptive step switching") + ": " +
                    GetStepChangeMetricDescription(_stepMetric),
                    EmptyOptionsArray);
            }

            GUILayout.EndVertical();
        }

        private void BuildFlowPolicyDisplay(bool editable) {
            if(_stepMetric != StepChangeMetric.Default) {
                return;
            }

            string flowBalanceText;
            if (_waitFlowBalance < 0.01f) {
                flowBalanceText = $"{_waitFlowBalance:0.###}";
            } else if (_waitFlowBalance < 0.1f) {
                flowBalanceText = $"{_waitFlowBalance:0.##}";
            } else {
                flowBalanceText = $"{_waitFlowBalance:0.#}";
            }

            GUILayout.BeginHorizontal(EmptyOptionsArray);
            string sensText = T("TTL.Label:Flow sensitivity");

            // TODO: Clarify for the user what this means, more help text, simpler UI
            if (editable) {
                GUILayout.Label(
                    $"{sensText} ({flowBalanceText}, {GetWaitFlowBalanceInfo()}):",
                    EmptyOptionsArray);
                GUILayout.EndHorizontal();


                // logarithmic slider
                float logValue = GUILayout.HorizontalSlider(
                    value: Mathf.Log10(_waitFlowBalance),
                    leftValue: logMinSensitivity_,
                    rightValue: logMaxSensitivity_,
                    options: EmptyOptionsArray);
                _waitFlowBalance = Mathf.Pow(10, logValue);

                // clamp + step snapping
                if (_waitFlowBalance < minSensitivity_) {
                    _waitFlowBalance = 0.001f;
                } else if (_waitFlowBalance < 0.01f) {
                    _waitFlowBalance = Mathf.Round(_waitFlowBalance * 1000f) * 0.001f;
                } else if (_waitFlowBalance < 0.1f) {
                    _waitFlowBalance = Mathf.Round(_waitFlowBalance * 100f) * 0.01f;
                } else if (_waitFlowBalance < maxSensitivity_) {
                    _waitFlowBalance = Mathf.Round(_waitFlowBalance * 10f) * 0.1f;
                } else {
                    _waitFlowBalance = maxSensitivity_;
                }

                GUILayout.BeginHorizontal(EmptyOptionsArray);
                GUIStyle style = new GUIStyle {
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.LowerLeft,
                };
                GUILayout.Label(
                    T("TTL.Label:Low"),
                    style,
                    GUILayout.Height(10));
                style.alignment = TextAnchor.LowerRight;
                GUILayout.Label(
                    T("TTL.Label:High"),
                    style,
                    GUILayout.Height(10));
            } else {
                GUILayout.Label($"{sensText}: {flowBalanceText} ({GetWaitFlowBalanceInfo()})", EmptyOptionsArray);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);
        }

        private string GetStepChangeMetricDescription(StepChangeMetric metric) {
            switch (metric) {
                // also: case StepChangeMetric.Default:
                default: {
                        return T("TTL.Label:Flow ratio below wait ratio");
                    }

                case StepChangeMetric.FirstFlow: {
                        return T("TTL.Label:Flow ratio > 0");
                    }

                case StepChangeMetric.FirstWait: {
                        return T("TTL.Label:Wait ratio > 0");
                    }

                case StepChangeMetric.NoFlow: {
                        return T("TTL.Label:Flow ratio is 0");
                    }

                case StepChangeMetric.NoWait: {
                        return T("TTL.Label:Wait ratio is 0");
                    }
            }
        }

        private void GuiTimedTrafficLightsNode() {
            _cursorInSecondaryPanel = false;

            var oldMatrix = GUI.matrix;
            GUI.matrix = UIScaler.ScaleMatrix;
            _windowRect2 = GUILayout.Window(
                id: 252,
                screenRect: _windowRect2,
                func: _guiTimedTrafficLightsNodeWindowDelegate,
                text: T("TTL.Window.Title:Select nodes"),
                style: WindowStyle,
                options: EmptyOptionsArray);
            GUI.matrix = oldMatrix;

            _cursorInSecondaryPanel = _windowRect2.Contains(Event.current.mousePosition);
        }

        /// <summary>
        /// Main TTL manager window
        /// </summary>
        private void GuiTimedTrafficLights() {
            _cursorInSecondaryPanel = false;

            var oldMatrix = GUI.matrix;
            GUI.matrix = UIScaler.ScaleMatrix;
            _windowRect = GUILayout.Window(
                id: 253,
                screenRect: _windowRect,
                func: _guiTimedControlPanelDelegate,
                text: T("Dialog.Title:Timed traffic lights manager"),
                style: WindowStyle,
                options: EmptyOptionsArray);

            _cursorInSecondaryPanel = _windowRect.Contains(Event.current.mousePosition);
            GUI.matrix = oldMatrix;

            ShowGUI();
        }

        /// <summary>
        /// Copy and paste TTL settings window
        /// </summary>
        private void GuiTimedTrafficLightsCopy() {
            _cursorInSecondaryPanel = false;

            var oldMatrix = GUI.matrix;
            GUI.matrix = UIScaler.ScaleMatrix;
            _windowRect2 = GUILayout.Window(
                id: 255,
                screenRect: _windowRect2,
                func: _guiTimedTrafficLightsPasteWindowDelegate,
                text: T("TTL.Window.Title:Paste"),
                style: WindowStyle,
                options: EmptyOptionsArray);

            _cursorInSecondaryPanel = _windowRect2.Contains(Event.current.mousePosition);
            GUI.matrix = oldMatrix;
        }

        private void GuiTimedTrafficLightsPasteWindow(int num) {
            GUILayout.Label(T("TTL.Label:Select junction"), EmptyOptionsArray);
        }

        /// <summary>
        /// :Select nodes for TTL" window
        /// </summary>
        /// <param name="num">Not used</param>
        private void GuiTimedTrafficLightsNodeWindow(int num) {
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            if (selectedNodeIds.Count < 1) {
                GUILayout.Label(T("Label:Select nodes"), EmptyOptionsArray);
            } else {
                string txt = selectedNodeIds.Aggregate(
                    string.Empty,
                    (current, t) => {
                        string format = T("TTL.Label.Format:Node {0}") + "\n";
                        return current + string.Format(format, t.ToString());
                    });

                GUILayout.Label(txt, EmptyOptionsArray);

                if (selectedNodeIds.Count > 0 &&
                    GUILayout.Button(T("TTL.Button:Deselect all nodes"), EmptyOptionsArray)) {
                    ClearSelectedNodes();
                }

                if (GUILayout.Button(T("TTL.Button:Setup timed traffic light"), EmptyOptionsArray)) {
                    _waitFlowBalance = GlobalConfig.Instance.TimedTrafficLights.FlowToWaitRatio;

                    foreach (ushort nodeId in selectedNodeIds) {
                        tlsMan.SetUpTimedTrafficLight(nodeId, selectedNodeIds);
                        RefreshCurrentTimedNodeIds(nodeId);
                    }

                    SetToolMode(TTLToolMode.ShowLights);
                } else {
                    return;
                }
            }

            DragWindow(ref _windowRect2);
        }

        private string GetWaitFlowBalanceInfo() {
            if (_waitFlowBalance < 0.1f) {
                return T("TTL.FlowBalance:Extreme long green/red phases");
            }

            if (_waitFlowBalance < 0.5f) {
                return T("TTL.FlowBalance:Very long green/red phases");
            }

            if (_waitFlowBalance < 0.75f) {
                return T("TTL.FlowBalance:Long green/red phases");
            }

            if (_waitFlowBalance < 1.25f) {
                return T("TTL.FlowBalance:Moderate green/red phases");
            }

            if (_waitFlowBalance < 1.5f) {
                return T("TTL.FlowBalance:Short green/red phases");
            }

            if (_waitFlowBalance < 2.5f) {
                return T("TTL.FlowBalance:Very short green/red phases");
            }

            return T("TTL.FlowBalance:Extreme short green/red phases");
        }

        private void DisableTimed() {
            if (selectedNodeIds.Count <= 0) {
                return;
            }

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            foreach (ushort selectedNodeId in selectedNodeIds) {
                tlsMan.RemoveNodeFromSimulation(selectedNodeId, true, false);
                RefreshCurrentTimedNodeIds(selectedNodeId);
            }
        }

        private void AddSelectedNode(ushort node) {
            selectedNodeIds.Add(node);
        }

        private bool IsNodeSelected(ushort node) {
            return selectedNodeIds.Contains(node);
        }

        private void RemoveSelectedNode(ushort node) {
            selectedNodeIds.Remove(node);
        }

        private void ClearSelectedNodes() {
            selectedNodeIds.Clear();
        }

        private void DrawStraightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
            switch (state) {
                case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.GreenLightStraight);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.GreenToRed: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLight);
                        break;
                    }

                // also: case RoadBaseAI.TrafficLightState.Red:
                default: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.RedLightStraight);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.RedToGreen: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLightStraight);
                        break;
                    }
            }
        }

        private void DrawForwardLeftLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
            switch (state) {
                case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.GreenLightForwardLeft);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.GreenToRed: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLight);
                        break;
                    }

                // also: case RoadBaseAI.TrafficLightState.Red:
                default: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.RedLightForwardLeft);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.RedToGreen: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLightForwardLeft);
                        break;
                    }
            }
        }

        private void DrawForwardRightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
            switch (state) {
                case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.GreenLightForwardRight);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.GreenToRed: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLight);
                        break;
                    }

                // also: case RoadBaseAI.TrafficLightState.Red:
                default: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.RedLightForwardRight);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.RedToGreen: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLightForwardRight);
                        break;
                    }
            }
        }

        private void DrawLeftLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
            switch (state) {
                case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.GreenLightLeft);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.GreenToRed: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLight);
                        break;
                    }

                // also: case RoadBaseAI.TrafficLightState.Red:
                default: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.RedLightLeft);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.RedToGreen: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLightLeft);
                        break;
                    }
            }
        }

        private void DrawRightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
            switch (state) {
                case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.GreenLightRight);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.GreenToRed: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLight);
                        break;
                    }

                // also: case RoadBaseAI.TrafficLightState.Red:
                default: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.RedLightRight);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.RedToGreen: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLightRight);
                        break;
                    }
            }
        }

        private void DrawMainLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
            switch (state) {
                case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.GreenLight);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.GreenToRed: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowLight);
                        break;
                    }

                // also: case RoadBaseAI.TrafficLightState.Red:
                default: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.RedLight);
                        break;
                    }

                case RoadBaseAI.TrafficLightState.RedToGreen: {
                        GUI.DrawTexture(rect, TrafficLightTextures.Instance.YellowRedLight);
                        break;
                    }
            }
        }

        public override void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) {
            if (!ToolMode.TimedTrafficLights.Equals(toolMode)) {
                return;
            }

            if (viewOnly && !SavedGameOptions.Instance.timedLightsOverlay) {
                return;
            }

            Vector3 camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            foreach (ushort nodeId in currentTimedNodeIds) {
                ref NetNode netNode = ref nodeId.ToNode();

                if (!netNode.IsValid()) {
                    continue;
                }

                if (selectedNodeIds.Contains(nodeId)) {
                    continue;
                }

                if (tlsMan.HasTimedSimulation(nodeId)) {
                    TimedTrafficLights timedNode =
                        tlsMan.TrafficLightSimulations[nodeId].timedLight;

                    Texture2D tex = timedNode.IsStarted()
                                        ? timedNode.IsInTestMode()
                                               ? TrafficLightTextures.Instance.ClockTest
                                               : TrafficLightTextures.Instance.ClockPlay
                                        : TrafficLightTextures.Instance.ClockPause;
                    Highlight.DrawGenericSquareOverlayTexture(
                        texture: tex,
                        camPos: camPos,
                        worldPos: netNode.m_position,
                        size: 120f,
                        canHover: false);
                }
            }
        }

        private void ShowGUI() {
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
            JunctionRestrictionsManager junctionRestrictionsManager = JunctionRestrictionsManager.Instance;
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            var vehicleInfoSignTextures = RoadUI.Instance.VehicleInfoSignTextures;
            var hoveredSegment = false;

            foreach (ushort nodeId in selectedNodeIds) {
                if (!tlsMan.HasTimedSimulation(nodeId)) {
                    continue;
                }

                ref NetNode netNode = ref nodeId.ToNode();

                TimedTrafficLights timedNode = tlsMan.TrafficLightSimulations[nodeId].timedLight;

                Vector3 nodePos = netNode.m_position;

                bool nodeVisible = GeometryUtil.WorldToScreenPoint(nodePos, out Vector3 _);
                if (!nodeVisible) {
                    continue;
                }

                Vector3 diff = nodePos - InGameUtil.Instance.CachedCameraTransform.position;
                float zoom = 1.0f / diff.magnitude * 100f * MainTool.GetBaseZoom();

                ref NetNode node = ref nodeId.ToNode();

                for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                    ushort srcSegmentId = node.GetSegment(segmentIndex);
                    if (srcSegmentId == 0) {
                        continue;
                    }

                    bool startNode = srcSegmentId.ToSegment().IsStartNode(nodeId);

                    CustomSegmentLights liveSegmentLights =
                        customTrafficLightsManager.GetSegmentLights(srcSegmentId, startNode, false);

                    if (liveSegmentLights == null) {
                        continue;
                    }

                    bool showPedLight = liveSegmentLights.PedestrianLightState != null &&
                                        junctionRestrictionsManager.IsPedestrianCrossingAllowed(
                                            liveSegmentLights.SegmentId,
                                            liveSegmentLights.StartNode);

                    bool timedActive = timedNode.IsStarted();
                    if (!timedActive) {
                        liveSegmentLights.MakeRedOrGreen();
                    }

                    var offset = 17f;
                    Vector3 segmentLightPos = nodePos;

                    ref NetSegment sourceSegment = ref srcSegmentId.ToSegment();
                    if (sourceSegment.m_startNode == nodeId) {
                        segmentLightPos.x += sourceSegment.m_startDirection.x * offset;
                        segmentLightPos.y += sourceSegment.m_startDirection.y;
                        segmentLightPos.z += sourceSegment.m_startDirection.z * offset;
                    } else {
                        segmentLightPos.x += sourceSegment.m_endDirection.x * offset;
                        segmentLightPos.y += sourceSegment.m_endDirection.y;
                        segmentLightPos.z += sourceSegment.m_endDirection.z * offset;
                    }

                    bool segmentLightVisible = GeometryUtil.WorldToScreenPoint(
                        segmentLightPos,
                        out Vector3 screenPos);

                    if (!segmentLightVisible) {
                        continue;
                    }

                    Color guiColor = GUI.color;

                    float manualPedestrianWidth = 36f * zoom;
                    float manualPedestrianHeight = 35f * zoom;

                    float pedestrianWidth = 36f * zoom;
                    float pedestrianHeight = 61f * zoom;

                    // original / 2.5
                    float lightWidth = 41f * zoom;
                    float lightHeight = 97f * zoom;

                    // SWITCH MODE BUTTON
                    float modeWidth = 41f * zoom;
                    float modeHeight = 38f * zoom;

                    if (showPedLight) {
                        // pedestrian light

                        // SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
                        if (!timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
                            guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                _hoveredButton[0] == srcSegmentId &&
                                (_hoveredButton[1] == 1 || _hoveredButton[1] == 2) &&
                                _hoveredNode == nodeId);
                            GUI.color = guiColor;

                            var myRect2 = new Rect(
                                x: screenPos.x - manualPedestrianWidth / 2 -
                                   (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) +
                                   5f * zoom,
                                y: screenPos.y - manualPedestrianHeight / 2 - 9f * zoom,
                                width: manualPedestrianWidth,
                                height: manualPedestrianHeight);

                            GUI.DrawTexture(
                                myRect2,
                                liveSegmentLights.ManualPedestrianMode
                                    ? TrafficLightTextures.Instance.PedestrianModeManual
                                    : TrafficLightTextures.Instance.PedestrianModeAutomatic);

                            if (myRect2.Contains(Event.current.mousePosition) &&
                                !IsCursorInPanel()) {
                                _hoveredButton[0] = srcSegmentId;
                                _hoveredButton[1] = 1;
                                _hoveredNode = nodeId;
                                hoveredSegment = true;

                                if (MainTool.CheckClicked()) {
                                    liveSegmentLights.ManualPedestrianMode =
                                        !liveSegmentLights.ManualPedestrianMode;
                                }
                            }
                        }

                        // SWITCH PEDESTRIAN LIGHT
                        guiColor.a = TrafficManagerTool.GetHandleAlpha(
                            _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 2 &&
                            _hoveredNode == nodeId);

                        GUI.color = guiColor;

                        var myRect3 = new Rect(
                            screenPos.x - pedestrianWidth / 2 -
                            (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom,
                            screenPos.y - pedestrianHeight / 2 + 22f * zoom,
                            pedestrianWidth,
                            pedestrianHeight);

                        switch (liveSegmentLights.PedestrianLightState) {
                            case RoadBaseAI.TrafficLightState.Green: {
                                    GUI.DrawTexture(myRect3, TrafficLightTextures.Instance.PedestrianGreenLight);
                                    break;
                                }

                            // also: case RoadBaseAI.TrafficLightState.Red:
                            default: {
                                    GUI.DrawTexture(myRect3, TrafficLightTextures.Instance.PedestrianRedLight);
                                    break;
                                }
                        }

                        if (myRect3.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
                            _hoveredButton[0] = srcSegmentId;
                            _hoveredButton[1] = 2;
                            _hoveredNode = nodeId;
                            hoveredSegment = true;

                            if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
                                if (!liveSegmentLights.ManualPedestrianMode) {
                                    liveSegmentLights.ManualPedestrianMode = true;
                                } else {
                                    liveSegmentLights.ChangeLightPedestrian();
                                }
                            }
                        }
                    }

                    int lightOffset = -1;

                    foreach (ExtVehicleType vehicleType in liveSegmentLights.VehicleTypes) {
                        HashSet<byte> laneIndices = new HashSet<byte>();
                        for (byte laneIndex = 0;
                             laneIndex < liveSegmentLights.VehicleTypeByLaneIndex.Length;
                             ++laneIndex) {
                            if (liveSegmentLights.VehicleTypeByLaneIndex[laneIndex] ==
                                vehicleType) {
                                laneIndices.Add(laneIndex);
                            }
                        }

                        // Log._Debug($"Traffic light @ seg. {srcSegmentId} node {nodeId}. Lane
                        //     indices for vehicleType {vehicleType}: {string.Join(",", laneIndices
                        //     .Select(x => x.ToString()).ToArray())}");
                        ++lightOffset;
                        CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);
                        Vector3 offsetScreenPos = screenPos;
                        offsetScreenPos.y -= (lightHeight + 10f * zoom) * lightOffset;

                        if (!timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
                            guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == -1 &&
                                _hoveredNode == nodeId);
                            GUI.color = guiColor;

                            var myRect1 = new Rect(
                                offsetScreenPos.x - modeWidth / 2,
                                offsetScreenPos.y - modeHeight / 2 + modeHeight - 7f * zoom,
                                modeWidth,
                                modeHeight);

                            GUI.DrawTexture(myRect1, TrafficLightTextures.Instance.LightMode);

                            if (myRect1.Contains(Event.current.mousePosition) &&
                                !IsCursorInPanel()) {
                                _hoveredButton[0] = srcSegmentId;
                                _hoveredButton[1] = -1;
                                _hoveredNode = nodeId;
                                hoveredSegment = true;

                                if (MainTool.CheckClicked()) {
                                    liveSegmentLight.ToggleMode();
                                    timedNode.ChangeLightMode(
                                        srcSegmentId,
                                        vehicleType,
                                        liveSegmentLight.CurrentMode);
                                }
                            }
                        }

                        if (vehicleType != ExtVehicleType.None) {
                            // Info sign
                            float infoWidth = 56.125f * zoom;
                            float infoHeight = 51.375f * zoom;

                            int numInfos = 0;

                            for (int k = 0; k < TrafficManagerTool.InfoSignsToDisplay.Length; ++k) {
                                if ((TrafficManagerTool.InfoSignsToDisplay[k] & vehicleType) ==
                                    ExtVehicleType.None) {
                                    continue;
                                }

                                var infoRect = new Rect(
                                    offsetScreenPos.x + modeWidth / 2f +
                                    7f * zoom * (numInfos + 1) + infoWidth * numInfos,
                                    offsetScreenPos.y - infoHeight / 2f,
                                    infoWidth,
                                    infoHeight);
                                guiColor.a = TrafficManagerTool.GetHandleAlpha(false);
                                GUI.DrawTexture(
                                    infoRect,
                                    vehicleInfoSignTextures[TrafficManagerTool.InfoSignsToDisplay[k]]);
                                ++numInfos;
                            }
                        }

#if DEBUG
                        if (timedActive /*&& _timedShowNumbers*/) {
                            // var prioSeg = TrafficPriorityManager.Instance.GetPrioritySegment(
                            // nodeId, srcSegmentId);
                            float counterSize = 20f * zoom;
                            float yOffset = counterSize + 77f * zoom - modeHeight * 2;

                            // var carNumRect = new Rect(offsetScreenPos.x, offsetScreenPos.y - yOffset,
                            //    counterSize, counterSize);
                            var segIdRect = new Rect(
                                offsetScreenPos.x,
                                offsetScreenPos.y - yOffset - counterSize - 2f,
                                counterSize,
                                counterSize);

                            _counterStyle.fontSize = (int)(15f * zoom);
                            _counterStyle.normal.textColor = new Color(1f, 0f, 0f);

                            //    String labelStr = "n/a";
                            //    if (prioSeg != null) {
                            //        labelStr =
                            //            prioSeg.GetRegisteredVehicleCount(laneIndices).ToString() +
                            //            " " + Translation.GetString("incoming");
                            //    }
                            //
                            //    GUI.Label(carNumRect, labelStr, _counterStyle);
                            _counterStyle.normal.textColor = new Color(1f, 0f, 0f);
                            GUI.Label(
                                segIdRect,
                                string.Format(
                                    T("TTL.Label.Format:Segment {0}"),
                                    srcSegmentId),
                                _counterStyle);
                        }
#endif

                        if (lightOffset == 0 && showPedLight) {
                            // PEDESTRIAN COUNTER
                            if (timedActive && _timedShowNumbers) {
                                float counterSize = 20f * zoom;

                                long counter = timedNode.CheckNextChange(
                                    srcSegmentId,
                                    startNode,
                                    vehicleType,
                                    3);

                                float numOffset;

                                if (liveSegmentLights.PedestrianLightState ==
                                    RoadBaseAI.TrafficLightState.Red) {
                                    // TODO check this
                                    numOffset = counterSize + 53f * zoom - modeHeight * 2;
                                } else {
                                    numOffset = counterSize + 29f * zoom - modeHeight * 2;
                                }

                                var myRectCounterNum =
                                    new Rect(
                                        offsetScreenPos.x - counterSize + 15f * zoom +
                                        (counter >= 10
                                             ? counter >= 100 ? -10 * zoom : -5 * zoom
                                             : 1f) + 24f * zoom - pedestrianWidth / 2,
                                        offsetScreenPos.y - numOffset,
                                        counterSize,
                                        counterSize);

                                _counterStyle.fontSize = (int)(15f * zoom);
                                _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                if (myRectCounterNum.Contains(Event.current.mousePosition) &&
                                    !IsCursorInPanel()) {
                                    _hoveredButton[0] = srcSegmentId;
                                    _hoveredButton[1] = 2;
                                    _hoveredNode = nodeId;
                                    hoveredSegment = true;
                                }
                            }
                        }

                        ExtSegment seg = extSegmentManager.ExtSegments[srcSegmentId];
                        ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(srcSegmentId, startNode)];

                        if (seg.oneWay && segEnd.outgoing) {
                            continue;
                        }

                        segEndMan.CalculateOutgoingLeftStraightRightSegments(
                            ref segEnd,
                            ref nodeId.ToNode(),
                            out bool hasOutgoingLeftSegment,
                            out bool hasOutgoingForwardSegment,
                            out bool hasOutgoingRightSegment);

                        bool hasOtherLight = false;

                        switch (liveSegmentLight.CurrentMode) {
                            case LightMode.Simple: {
                                    // no arrow light
                                    guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                        _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 &&
                                        _hoveredNode == nodeId);

                                    GUI.color = guiColor;

                                    var myRect4 =
                                        new Rect(
                                            offsetScreenPos.x - lightWidth / 2 -
                                            (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) -
                                            pedestrianWidth + 5f * zoom,
                                            offsetScreenPos.y - lightHeight / 2,
                                            lightWidth,
                                            lightHeight);

                                    DrawMainLightTexture(liveSegmentLight.LightMain, myRect4);

                                    if (myRect4.Contains(Event.current.mousePosition) &&
                                        !IsCursorInPanel()) {
                                        _hoveredButton[0] = srcSegmentId;
                                        _hoveredButton[1] = 3;
                                        _hoveredNode = nodeId;
                                        hoveredSegment = true;

                                        if (MainTool.CheckClicked() && !timedActive &&
                                            (_timedPanelAdd || _timedEditStep >= 0)) {
                                            liveSegmentLight.ChangeMainLight();
                                        }
                                    }

                                    // COUNTER
                                    if (timedActive && _timedShowNumbers) {
                                        float counterSize = 20f * zoom;

                                        long counter = timedNode.CheckNextChange(
                                            srcSegmentId,
                                            startNode,
                                            vehicleType,
                                            0);

                                        float numOffset;

                                        if (liveSegmentLight.LightMain ==
                                            RoadBaseAI.TrafficLightState.Red) {
                                            numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                        } else {
                                            numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                        }

                                        var myRectCounterNum =
                                            new Rect(
                                                offsetScreenPos.x - counterSize + 15f * zoom +
                                                (counter >= 10
                                                     ? counter >= 100 ? -10 * zoom : -5 * zoom
                                                     : 0f) - pedestrianWidth + 5f * zoom,
                                                offsetScreenPos.y - numOffset,
                                                counterSize,
                                                counterSize);

                                        _counterStyle.fontSize = (int)(18f * zoom);
                                        _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                        GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                        if (myRectCounterNum.Contains(Event.current.mousePosition) &&
                                            !IsCursorInPanel()) {
                                            _hoveredButton[0] = srcSegmentId;
                                            _hoveredButton[1] = 3;
                                            _hoveredNode = nodeId;
                                            hoveredSegment = true;
                                        }
                                    }

                                    GUI.color = guiColor;
                                    break;
                                }

                            case LightMode.SingleLeft: {
                                    if (hasOutgoingLeftSegment) {
                                        // left arrow light
                                        guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                            _hoveredButton[0] == srcSegmentId &&
                                            _hoveredButton[1] == 3 && _hoveredNode == nodeId);

                                        GUI.color = guiColor;

                                        var myRect4 =
                                            new Rect(
                                                offsetScreenPos.x - lightWidth / 2 -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? lightWidth * 2
                                                     : lightWidth) - pedestrianWidth + 5f * zoom,
                                                offsetScreenPos.y - lightHeight / 2,
                                                lightWidth,
                                                lightHeight);

                                        DrawLeftLightTexture(liveSegmentLight.LightLeft, myRect4);

                                        if (myRect4.Contains(Event.current.mousePosition) &&
                                            !IsCursorInPanel()) {
                                            _hoveredButton[0] = srcSegmentId;
                                            _hoveredButton[1] = 3;
                                            _hoveredNode = nodeId;
                                            hoveredSegment = true;

                                            if (MainTool.CheckClicked() && !timedActive &&
                                                (_timedPanelAdd || _timedEditStep >= 0)) {
                                                liveSegmentLight.ChangeLeftLight();
                                            }
                                        }

                                        // COUNTER
                                        if (timedActive && _timedShowNumbers) {
                                            float counterSize = 20f * zoom;

                                            long counter = timedNode.CheckNextChange(
                                                srcSegmentId,
                                                startNode,
                                                vehicleType,
                                                1);

                                            float numOffset;

                                            if (liveSegmentLight.LightLeft ==
                                                RoadBaseAI.TrafficLightState.Red) {
                                                numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                            } else {
                                                numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                            }

                                            var myRectCounterNum =
                                                new Rect(
                                                    offsetScreenPos.x - counterSize + 15f * zoom +
                                                    (counter >= 10
                                                         ? counter >= 100 ? -10 * zoom : -5 * zoom
                                                         : 0f) - pedestrianWidth + 5f * zoom -
                                                    (_timedPanelAdd || _timedEditStep >= 0
                                                         ? lightWidth * 2
                                                         : lightWidth),
                                                    offsetScreenPos.y - numOffset,
                                                    counterSize,
                                                    counterSize);

                                            _counterStyle.fontSize = (int)(18f * zoom);
                                            _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                            GUI.Label(
                                                myRectCounterNum,
                                                counter.ToString(),
                                                _counterStyle);

                                            if (myRectCounterNum.Contains(
                                                    Event.current.mousePosition) &&
                                                !IsCursorInPanel()) {
                                                _hoveredButton[0] = srcSegmentId;
                                                _hoveredButton[1] = 3;
                                                _hoveredNode = nodeId;
                                                hoveredSegment = true;
                                            }
                                        }
                                    }

                                    // forward-right arrow light
                                    guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                        _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 &&
                                        _hoveredNode == nodeId);

                                    GUI.color = guiColor;

                                    var myRect5 =
                                        new Rect(
                                            offsetScreenPos.x - lightWidth / 2 - pedestrianWidth -
                                            (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) +
                                            5f * zoom,
                                            offsetScreenPos.y - lightHeight / 2,
                                            lightWidth,
                                            lightHeight);

                                    if (hasOutgoingForwardSegment && hasOutgoingRightSegment) {
                                        DrawForwardRightLightTexture(liveSegmentLight.LightMain, myRect5);
                                        hasOtherLight = true;
                                    } else if (hasOutgoingForwardSegment) {
                                        DrawStraightLightTexture(liveSegmentLight.LightMain, myRect5);
                                        hasOtherLight = true;
                                    } else if (hasOutgoingRightSegment) {
                                        DrawRightLightTexture(liveSegmentLight.LightMain, myRect5);
                                        hasOtherLight = true;
                                    }

                                    if (hasOtherLight &&
                                        myRect5.Contains(Event.current.mousePosition) &&
                                        !IsCursorInPanel()) {
                                        _hoveredButton[0] = srcSegmentId;
                                        _hoveredButton[1] = 4;
                                        _hoveredNode = nodeId;
                                        hoveredSegment = true;

                                        if (MainTool.CheckClicked() && !timedActive &&
                                            (_timedPanelAdd || _timedEditStep >= 0)) {
                                            liveSegmentLight.ChangeMainLight();
                                        }
                                    }

                                    // COUNTER
                                    if (timedActive && _timedShowNumbers) {
                                        float counterSize = 20f * zoom;
                                        long counter = timedNode.CheckNextChange(
                                            srcSegmentId,
                                            startNode,
                                            vehicleType,
                                            0);

                                        float numOffset;
                                        if (liveSegmentLight.LightMain ==
                                            RoadBaseAI.TrafficLightState.Red) {
                                            numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                        } else {
                                            numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                        }

                                        var myRectCounterNum =
                                            new Rect(
                                                offsetScreenPos.x - counterSize + 15f * zoom +
                                                (counter >= 10
                                                     ? counter >= 100 ? -10 * zoom : -5 * zoom
                                                     : 0f) - pedestrianWidth + 5f * zoom -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? lightWidth
                                                     : 0f),
                                                offsetScreenPos.y - numOffset,
                                                counterSize,
                                                counterSize);

                                        _counterStyle.fontSize = (int)(18f * zoom);
                                        _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                        GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                        if (myRectCounterNum.Contains(Event.current.mousePosition) &&
                                            !IsCursorInPanel()) {
                                            _hoveredButton[0] = srcSegmentId;
                                            _hoveredButton[1] = 4;
                                            _hoveredNode = nodeId;
                                            hoveredSegment = true;
                                        }
                                    }
                                    break;
                                }

                            case LightMode.SingleRight: {
                                    // forward-left light
                                    guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                        _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 &&
                                        _hoveredNode == nodeId);

                                    GUI.color = guiColor;

                                    var myRect4 = new Rect(
                                        offsetScreenPos.x - lightWidth / 2 -
                                        (_timedPanelAdd || _timedEditStep >= 0
                                             ? lightWidth * 2
                                             : lightWidth) - pedestrianWidth + 5f * zoom,
                                        offsetScreenPos.y - lightHeight / 2,
                                        lightWidth,
                                        lightHeight);

                                    var lightType = 0;

                                    hasOtherLight = false;
                                    if (hasOutgoingForwardSegment && hasOutgoingLeftSegment) {
                                        hasOtherLight = true;
                                        DrawForwardLeftLightTexture(
                                            liveSegmentLight.LightMain,
                                            myRect4);
                                        lightType = 1;
                                    } else if (hasOutgoingForwardSegment) {
                                        hasOtherLight = true;
                                        if (!hasOutgoingRightSegment) {
                                            myRect4 = new Rect(
                                                offsetScreenPos.x - lightWidth / 2 -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? lightWidth
                                                     : 0f) - pedestrianWidth + 5f * zoom,
                                                offsetScreenPos.y - lightHeight / 2,
                                                lightWidth,
                                                lightHeight);
                                        }

                                        DrawStraightLightTexture(liveSegmentLight.LightMain, myRect4);
                                    } else if (hasOutgoingLeftSegment) {
                                        hasOtherLight = true;
                                        if (!hasOutgoingRightSegment) {
                                            myRect4 = new Rect(
                                                offsetScreenPos.x - lightWidth / 2 -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? lightWidth
                                                     : 0f) - pedestrianWidth + 5f * zoom,
                                                offsetScreenPos.y - lightHeight / 2,
                                                lightWidth,
                                                lightHeight);
                                        }

                                        DrawLeftLightTexture(liveSegmentLight.LightMain, myRect4);
                                    }

                                    if (hasOtherLight &&
                                        myRect4.Contains(Event.current.mousePosition) &&
                                        !IsCursorInPanel()) {
                                        _hoveredButton[0] = srcSegmentId;
                                        _hoveredButton[1] = 3;
                                        _hoveredNode = nodeId;
                                        hoveredSegment = true;

                                        if (MainTool.CheckClicked() && !timedActive &&
                                            (_timedPanelAdd || _timedEditStep >= 0)) {
                                            liveSegmentLight.ChangeMainLight();
                                        }
                                    }

                                    // COUNTER
                                    if (timedActive && _timedShowNumbers) {
                                        float counterSize = 20f * zoom;

                                        long counter = timedNode.CheckNextChange(
                                            srcSegmentId,
                                            startNode,
                                            vehicleType,
                                            lightType);

                                        float numOffset;

                                        if (liveSegmentLight.LightMain ==
                                            RoadBaseAI.TrafficLightState.Red) {
                                            numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                        } else {
                                            numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                        }

                                        var myRectCounterNum =
                                            new Rect(
                                                offsetScreenPos.x - counterSize + 15f * zoom +
                                                (counter >= 10
                                                     ? counter >= 100 ? -10 * zoom : -5 * zoom
                                                     : 0f) - pedestrianWidth + 5f * zoom -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? hasOutgoingRightSegment
                                                            ? lightWidth * 2
                                                            : lightWidth
                                                     : hasOutgoingRightSegment ? lightWidth : 0f),
                                                offsetScreenPos.y - numOffset,
                                                counterSize,
                                                counterSize);

                                        _counterStyle.fontSize = (int)(18f * zoom);
                                        _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                        GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

                                        if (myRectCounterNum.Contains(Event.current.mousePosition) &&
                                            !IsCursorInPanel()) {
                                            _hoveredButton[0] = srcSegmentId;
                                            _hoveredButton[1] = 3;
                                            _hoveredNode = nodeId;
                                            hoveredSegment = true;
                                        }
                                    }

                                    // right arrow light
                                    if (hasOutgoingRightSegment) {
                                        guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                            _hoveredButton[0] == srcSegmentId &&
                                            _hoveredButton[1] == 4 &&
                                            _hoveredNode == nodeId);

                                        GUI.color = guiColor;

                                        var rect5 =
                                            new Rect(
                                                offsetScreenPos.x - lightWidth / 2 -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? lightWidth
                                                     : 0f) - pedestrianWidth + 5f * zoom,
                                                offsetScreenPos.y - lightHeight / 2,
                                                lightWidth,
                                                lightHeight);

                                        DrawRightLightTexture(liveSegmentLight.LightRight, rect5);

                                        if (rect5.Contains(Event.current.mousePosition) &&
                                            !IsCursorInPanel()) {
                                            _hoveredButton[0] = srcSegmentId;
                                            _hoveredButton[1] = 4;
                                            _hoveredNode = nodeId;
                                            hoveredSegment = true;

                                            if (MainTool.CheckClicked() && !timedActive &&
                                                (_timedPanelAdd || _timedEditStep >= 0)) {
                                                liveSegmentLight.ChangeRightLight();
                                            }
                                        }

                                        // COUNTER
                                        if (timedActive && _timedShowNumbers) {
                                            float counterSize = 20f * zoom;

                                            long counter = timedNode.CheckNextChange(
                                                srcSegmentId,
                                                startNode,
                                                vehicleType,
                                                2);

                                            float numOffset;

                                            if (liveSegmentLight.LightRight ==
                                                RoadBaseAI.TrafficLightState.Red) {
                                                numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                            } else {
                                                numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                            }

                                            var myRectCounterNum =
                                                new Rect(
                                                    offsetScreenPos.x - counterSize + 15f * zoom +
                                                    (counter >= 10
                                                         ? counter >= 100 ? -10 * zoom : -5 * zoom
                                                         : 0f) -
                                                    pedestrianWidth + 5f * zoom -
                                                    (_timedPanelAdd || _timedEditStep >= 0
                                                         ? lightWidth
                                                         : 0f),
                                                    offsetScreenPos.y - numOffset,
                                                    counterSize,
                                                    counterSize);

                                            _counterStyle.fontSize = (int)(18f * zoom);
                                            _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                            GUI.Label(
                                                myRectCounterNum,
                                                counter.ToString(),
                                                _counterStyle);

                                            if (myRectCounterNum.Contains(
                                                    Event.current.mousePosition) &&
                                                !IsCursorInPanel()) {
                                                _hoveredButton[0] = srcSegmentId;
                                                _hoveredButton[1] = 4;
                                                _hoveredNode = nodeId;
                                                hoveredSegment = true;
                                            }
                                        }
                                    }
                                    break;
                                }

                            default: {
                                    // left arrow light
                                    if (hasOutgoingLeftSegment) {
                                        guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                            _hoveredButton[0] == srcSegmentId &&
                                            _hoveredButton[1] == 3 && _hoveredNode == nodeId);

                                        GUI.color = guiColor;

                                        float offsetLight = lightWidth;

                                        if (hasOutgoingRightSegment) {
                                            offsetLight += lightWidth;
                                        }

                                        if (hasOutgoingForwardSegment) {
                                            offsetLight += lightWidth;
                                        }

                                        var myRect4 =
                                            new Rect(
                                                offsetScreenPos.x - lightWidth / 2 -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? offsetLight
                                                     : offsetLight - lightWidth) - pedestrianWidth +
                                                5f * zoom,
                                                offsetScreenPos.y - lightHeight / 2,
                                                lightWidth,
                                                lightHeight);

                                        DrawLeftLightTexture(liveSegmentLight.LightLeft, myRect4);

                                        if (myRect4.Contains(Event.current.mousePosition) &&
                                            !IsCursorInPanel()) {
                                            _hoveredButton[0] = srcSegmentId;
                                            _hoveredButton[1] = 3;
                                            _hoveredNode = nodeId;
                                            hoveredSegment = true;

                                            if (MainTool.CheckClicked() && !timedActive &&
                                                (_timedPanelAdd || _timedEditStep >= 0)) {
                                                liveSegmentLight.ChangeLeftLight();
                                            }
                                        }

                                        // COUNTER
                                        if (timedActive && _timedShowNumbers) {
                                            float counterSize = 20f * zoom;

                                            long counter = timedNode.CheckNextChange(
                                                srcSegmentId,
                                                startNode,
                                                vehicleType,
                                                1);

                                            float numOffset;

                                            if (liveSegmentLight.LightLeft ==
                                                RoadBaseAI.TrafficLightState.Red) {
                                                numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                            } else {
                                                numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                            }

                                            var myRectCounterNum =
                                                new Rect(
                                                    offsetScreenPos.x - counterSize + 15f * zoom +
                                                    (counter >= 10
                                                         ? counter >= 100 ? -10 * zoom : -5 * zoom
                                                         : 0f) -
                                                    pedestrianWidth + 5f * zoom -
                                                    (_timedPanelAdd || _timedEditStep >= 0
                                                         ? offsetLight
                                                         : offsetLight - lightWidth),
                                                    offsetScreenPos.y - numOffset,
                                                    counterSize,
                                                    counterSize);

                                            _counterStyle.fontSize = (int)(18f * zoom);
                                            _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                            GUI.Label(
                                                myRectCounterNum,
                                                counter.ToString(),
                                                _counterStyle);

                                            if (myRectCounterNum.Contains(
                                                    Event.current.mousePosition) &&
                                                !IsCursorInPanel()) {
                                                _hoveredButton[0] = srcSegmentId;
                                                _hoveredButton[1] = 3;
                                                _hoveredNode = nodeId;
                                                hoveredSegment = true;
                                            }
                                        }
                                    }

                                    // forward arrow light
                                    if (hasOutgoingForwardSegment) {
                                        guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                            _hoveredButton[0] == srcSegmentId &&
                                            _hoveredButton[1] == 4 && _hoveredNode == nodeId);

                                        GUI.color = guiColor;

                                        float offsetLight = lightWidth;

                                        if (hasOutgoingRightSegment) {
                                            offsetLight += lightWidth;
                                        }

                                        var myRect6 =
                                            new Rect(
                                                offsetScreenPos.x - lightWidth / 2 -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? offsetLight
                                                     : offsetLight - lightWidth) - pedestrianWidth +
                                                5f * zoom,
                                                offsetScreenPos.y - lightHeight / 2,
                                                lightWidth,
                                                lightHeight);

                                        DrawStraightLightTexture(liveSegmentLight.LightMain, myRect6);

                                        if (myRect6.Contains(Event.current.mousePosition) &&
                                            !IsCursorInPanel()) {
                                            _hoveredButton[0] = srcSegmentId;
                                            _hoveredButton[1] = 4;
                                            _hoveredNode = nodeId;
                                            hoveredSegment = true;

                                            if (MainTool.CheckClicked() && !timedActive &&
                                                (_timedPanelAdd || _timedEditStep >= 0)) {
                                                liveSegmentLight.ChangeMainLight();
                                            }
                                        }

                                        // COUNTER
                                        if (timedActive && _timedShowNumbers) {
                                            float counterSize = 20f * zoom;

                                            long counter = timedNode.CheckNextChange(
                                                srcSegmentId,
                                                startNode,
                                                vehicleType,
                                                0);

                                            float numOffset;

                                            if (liveSegmentLight.LightMain ==
                                                RoadBaseAI.TrafficLightState.Red) {
                                                numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                            } else {
                                                numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                            }

                                            var myRectCounterNum =
                                                new Rect(
                                                    offsetScreenPos.x - counterSize + 15f * zoom +
                                                    (counter >= 10
                                                         ? counter >= 100 ? -10 * zoom : -5 * zoom
                                                         : 0f) -
                                                    pedestrianWidth + 5f * zoom -
                                                    (_timedPanelAdd || _timedEditStep >= 0
                                                         ? offsetLight
                                                         : offsetLight - lightWidth),
                                                    offsetScreenPos.y - numOffset,
                                                    counterSize,
                                                    counterSize);

                                            _counterStyle.fontSize = (int)(18f * zoom);
                                            _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                            GUI.Label(
                                                myRectCounterNum,
                                                counter.ToString(),
                                                _counterStyle);

                                            if (myRectCounterNum.Contains(
                                                    Event.current.mousePosition) &&
                                                !IsCursorInPanel()) {
                                                _hoveredButton[0] = srcSegmentId;
                                                _hoveredButton[1] = 4;
                                                _hoveredNode = nodeId;
                                                hoveredSegment = true;
                                            }
                                        }
                                    }

                                    // right arrow light
                                    if (hasOutgoingRightSegment) {
                                        guiColor.a = TrafficManagerTool.GetHandleAlpha(
                                            _hoveredButton[0] == srcSegmentId &&
                                            _hoveredButton[1] == 5 && _hoveredNode == nodeId);

                                        GUI.color = guiColor;

                                        var rect6 =
                                            new Rect(
                                                offsetScreenPos.x - lightWidth / 2 -
                                                (_timedPanelAdd || _timedEditStep >= 0
                                                     ? lightWidth
                                                     : 0f) - pedestrianWidth + 5f * zoom,
                                                offsetScreenPos.y - lightHeight / 2,
                                                lightWidth,
                                                lightHeight);

                                        DrawRightLightTexture(liveSegmentLight.LightRight, rect6);

                                        if (rect6.Contains(Event.current.mousePosition) &&
                                            !IsCursorInPanel()) {
                                            _hoveredButton[0] = srcSegmentId;
                                            _hoveredButton[1] = 5;
                                            _hoveredNode = nodeId;
                                            hoveredSegment = true;

                                            if (MainTool.CheckClicked() && !timedActive &&
                                                (_timedPanelAdd || _timedEditStep >= 0)) {
                                                liveSegmentLight.ChangeRightLight();
                                            }
                                        }

                                        // COUNTER
                                        if (timedActive && _timedShowNumbers) {
                                            float counterSize = 20f * zoom;

                                            long counter = timedNode.CheckNextChange(
                                                srcSegmentId,
                                                startNode,
                                                vehicleType,
                                                2);

                                            float numOffset;

                                            if (liveSegmentLight.LightRight ==
                                                RoadBaseAI.TrafficLightState.Red) {
                                                numOffset = counterSize + 96f * zoom - modeHeight * 2;
                                            } else {
                                                numOffset = counterSize + 40f * zoom - modeHeight * 2;
                                            }

                                            var myRectCounterNum =
                                                new Rect(
                                                    offsetScreenPos.x - counterSize + 15f * zoom +
                                                    (counter >= 10
                                                         ? counter >= 100 ? -10 * zoom : -5 * zoom
                                                         : 0f) -
                                                    pedestrianWidth + 5f * zoom -
                                                    (_timedPanelAdd || _timedEditStep >= 0
                                                         ? lightWidth
                                                         : 0f),
                                                    offsetScreenPos.y - numOffset,
                                                    counterSize,
                                                    counterSize);

                                            _counterStyle.fontSize = (int)(18f * zoom);
                                            _counterStyle.normal.textColor = new Color(1f, 1f, 1f);

                                            GUI.Label(
                                                myRectCounterNum,
                                                counter.ToString(),
                                                _counterStyle);

                                            if (myRectCounterNum.Contains(
                                                    Event.current.mousePosition) &&
                                                !IsCursorInPanel()) {
                                                _hoveredButton[0] = srcSegmentId;
                                                _hoveredButton[1] = 5;
                                                _hoveredNode = nodeId;
                                                hoveredSegment = true;
                                            }
                                        }
                                    }

                                    break;
                                }
                        } // end switch liveSegmentLight.CurrentMode
                    } // end foreach light
                } // end foreach segment
            } // end foreach node

            if (!hoveredSegment) {
                _hoveredButton[0] = 0;
                _hoveredButton[1] = 0;
            } else {
                UIInput.MouseUsed();
            }
        }

        private static string T(string text) => Translation.TrafficLights.Get(text);

        public void UpdateOnscreenDisplayPanel() {
            var items = new List<OsdItem>();
            items.Add(
                new HardcodedMouseShortcut(
                    button: UIMouseButton.Left,
                    shift: false,
                    ctrl: true,
                    alt: false,
                    localizedText: T("TimedTL.CtrlClick:Quick setup")));
            items.Add(
            new HardcodedMouseShortcut(
                    button: UIMouseButton.Left,
                    shift: false,
                    ctrl: true,
                    alt: true,
                    localizedText: T("TimedTL.AltCtrlClick:Alternate Quick setup"))); // dedicated far turn cycle. only works on + junctions.
            OnscreenDisplay.Display(items: items);
        }
    } // end class
}
