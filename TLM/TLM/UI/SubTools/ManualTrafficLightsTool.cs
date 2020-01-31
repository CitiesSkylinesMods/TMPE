namespace TrafficManager.UI.SubTools {
    using ColossalFramework;
    using JetBrains.Annotations;
    using TrafficLight;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.TrafficLight;
    using TrafficManager.Manager.Impl;
    using TrafficManager.UI.Textures;
    using UnityEngine;

    public class ManualTrafficLightsTool : SubTool {
        private readonly int[] hoveredButton = new int[2];
        private readonly GUIStyle counterStyle = new GUIStyle();

        public ManualTrafficLightsTool(TrafficManagerTool mainTool)
            : base(mainTool) { }

        public override void OnSecondaryClickOverlay() {
            if (IsCursorInPanel()) {
                return;
            }

            Cleanup();
            SelectedNodeId = 0;
        }

        public override void OnPrimaryClickOverlay() {
            if (IsCursorInPanel()) {
                return;
            }

            if (SelectedNodeId != 0) return;

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

            if (!tlsMan.TrafficLightSimulations[HoveredNodeId].IsTimedLight()) {
                if ((Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags &
                     NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
                    prioMan.RemovePrioritySignsFromNode(HoveredNodeId);
                    TrafficLightManager.Instance.AddTrafficLight(
                        HoveredNodeId,
                        ref Singleton<NetManager>.instance.m_nodes.m_buffer[
                            HoveredNodeId]);
                }

                if (tlsMan.SetUpManualTrafficLight(HoveredNodeId)) {
                    SelectedNodeId = HoveredNodeId;
                }

                // for (var s = 0; s < 8; s++) {
                //        var segment = Singleton<NetManager>
                //                      .instance.m_nodes.m_buffer[SelectedNodeId].GetSegment(s);
                //        if (segment != 0 &&
                //            !TrafficPriority.IsPrioritySegment(SelectedNodeId, segment)) {
                //            TrafficPriority.AddPrioritySegment(
                //                SelectedNodeId,
                //                segment,
                //                SegmentEnd.PriorityType.None);
                //        }
                //    }
            } else {
                MainTool.ShowError(
                    Translation.TrafficLights.Get("Dialog.Text:Node has timed TL script"));
            }
        }

        public override void OnToolGUI(Event e) {
            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            var hoveredSegment = false;

            if (SelectedNodeId != 0) {
                CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
                TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
                JunctionRestrictionsManager junctionRestrictionsManager = JunctionRestrictionsManager.Instance;

                if (!tlsMan.HasManualSimulation(SelectedNodeId)) {
                    return;
                }

                tlsMan.TrafficLightSimulations[SelectedNodeId].Housekeeping();

                // TODO check
                // if (Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].CountSegments() == 2) {
                //     _guiManualTrafficLightsCrosswalk(
                //         ref Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode]);
                //     return;
                // }
                NetNode[] nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
                NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

                for (int i = 0; i < 8; ++i) {
                    ushort segmentId = nodesBuffer[SelectedNodeId].GetSegment(i);
                    if (segmentId == 0) {
                        continue;
                    }

                    bool startNode =
                        (bool)Constants.ServiceFactory.NetService.IsStartNode(
                            segmentId,
                            SelectedNodeId);
                    Vector3 position = CalculateNodePositionForSegment(
                        nodesBuffer[SelectedNodeId],
                        ref segmentsBuffer[segmentId]);
                    ICustomSegmentLights segmentLights =
                        customTrafficLightsManager.GetSegmentLights(segmentId, startNode, false);

                    if (segmentLights == null) {
                        continue;
                    }

                    bool showPedLight = segmentLights.PedestrianLightState != null &&
                                        junctionRestrictionsManager.IsPedestrianCrossingAllowed(
                                            segmentLights.SegmentId,
                                            segmentLights.StartNode);
                    bool visible = MainTool.WorldToScreenPoint(position, out Vector3 screenPos);

                    if (!visible) {
                        continue;
                    }

                    Vector3 diff = position - Camera.main.transform.position;
                    float zoom = 1.0f / diff.magnitude * 100f;

                    // original / 2.5
                    float lightWidth = 41f * zoom;
                    float lightHeight = 97f * zoom;

                    float pedestrianWidth = 36f * zoom;
                    float pedestrianHeight = 61f * zoom;

                    // SWITCH MODE BUTTON
                    float modeWidth = 41f * zoom;
                    float modeHeight = 38f * zoom;

                    Color guiColor = GUI.color;

                    if (showPedLight) {
                        // pedestrian light

                        // SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
                        hoveredSegment = RenderManualPedestrianLightSwitch(
                            zoom,
                            segmentId,
                            screenPos,
                            lightWidth,
                            segmentLights,
                            hoveredSegment);

                        // SWITCH PEDESTRIAN LIGHT
                        guiColor.a = TrafficManagerTool.GetHandleAlpha(
                            hoveredButton[0] == segmentId && hoveredButton[1] == 2 &&
                            segmentLights.ManualPedestrianMode);
                        GUI.color = guiColor;

                        var myRect3 = new Rect(
                            screenPos.x - pedestrianWidth / 2 - lightWidth + 5f * zoom,
                            screenPos.y - pedestrianHeight / 2 + 22f * zoom,
                            pedestrianWidth,
                            pedestrianHeight);

                        switch (segmentLights.PedestrianLightState) {
                            case RoadBaseAI.TrafficLightState.Green: {
                                GUI.DrawTexture(
                                    myRect3,
                                    TrafficLightTextures.PedestrianGreenLight);
                                break;
                            }

                            // also: case RoadBaseAI.TrafficLightState.Red:
                            default: {
                                GUI.DrawTexture(
                                    myRect3,
                                    TrafficLightTextures.PedestrianRedLight);
                                break;
                            }
                        }

                        hoveredSegment = IsPedestrianLightHovered(
                            myRect3,
                            segmentId,
                            hoveredSegment,
                            segmentLights);
                    }

                    int lightOffset = -1;

                    foreach (ExtVehicleType vehicleType in segmentLights.VehicleTypes) {
                        ++lightOffset;
                        ICustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);

                        Vector3 offsetScreenPos = screenPos;
                        offsetScreenPos.y -= (lightHeight + 10f * zoom) * lightOffset;

                        SetAlpha(segmentId, -1);

                        var myRect1 = new Rect(
                            offsetScreenPos.x - modeWidth / 2,
                            offsetScreenPos.y - modeHeight / 2 + modeHeight - 7f * zoom,
                            modeWidth,
                            modeHeight);

                        GUI.DrawTexture(myRect1, TrafficLightTextures.LightMode);

                        hoveredSegment = GetHoveredSegment(
                            myRect1,
                            segmentId,
                            hoveredSegment,
                            segmentLight);

                        // COUNTER
                        hoveredSegment = RenderCounter(
                            segmentId,
                            offsetScreenPos,
                            modeWidth,
                            modeHeight,
                            zoom,
                            segmentLights,
                            hoveredSegment);

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
                                    (7f * zoom * (float)(numInfos + 1)) + (infoWidth * (float)numInfos),
                                    offsetScreenPos.y - (infoHeight / 2f),
                                    infoWidth,
                                    infoHeight);
                                guiColor.a = TrafficManagerTool.GetHandleAlpha(false);

                                GUI.DrawTexture(
                                    infoRect,
                                    RoadUITextures.VehicleInfoSignTextures[TrafficManagerTool.InfoSignsToDisplay[k]]);

                                ++numInfos;
                            }
                        }

                        ExtSegment seg = segMan.ExtSegments[segmentId];
                        ExtSegmentEnd segEnd =
                            segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];
                        if (seg.oneWay && segEnd.outgoing) {
                            continue;
                        }

                        segEndMan.CalculateOutgoingLeftStraightRightSegments(
                            ref segEnd,
                            ref nodesBuffer[SelectedNodeId],
                            out bool hasLeftSegment,
                            out bool hasForwardSegment,
                            out bool hasRightSegment);

                        switch (segmentLight.CurrentMode) {
                            case LightMode.Simple: {
                                hoveredSegment = SimpleManualSegmentLightMode(
                                    segmentId,
                                    offsetScreenPos,
                                    lightWidth,
                                    pedestrianWidth,
                                    zoom,
                                    lightHeight,
                                    segmentLight,
                                    hoveredSegment);
                                break;
                            }

                            case LightMode.SingleLeft: {
                                hoveredSegment = LeftForwardRManualSegmentLightMode(
                                    hasLeftSegment,
                                    segmentId,
                                    offsetScreenPos,
                                    lightWidth,
                                    pedestrianWidth,
                                    zoom,
                                    lightHeight,
                                    segmentLight,
                                    hoveredSegment,
                                    hasForwardSegment,
                                    hasRightSegment);
                                break;
                            }

                            case LightMode.SingleRight: {
                                hoveredSegment = RightForwardLSegmentLightMode(
                                    segmentId,
                                    offsetScreenPos,
                                    lightWidth,
                                    pedestrianWidth,
                                    zoom,
                                    lightHeight,
                                    hasForwardSegment,
                                    hasLeftSegment,
                                    segmentLight,
                                    hasRightSegment,
                                    hoveredSegment);
                                break;
                            }

                            default: {
                                // left arrow light
                                if (hasLeftSegment) {
                                    hoveredSegment = LeftArrowLightMode(
                                        segmentId,
                                        lightWidth,
                                        hasRightSegment,
                                        hasForwardSegment,
                                        offsetScreenPos,
                                        pedestrianWidth,
                                        zoom,
                                        lightHeight,
                                        segmentLight,
                                        hoveredSegment);
                                }

                                // forward arrow light
                                if (hasForwardSegment) {
                                    hoveredSegment = ForwardArrowLightMode(
                                        segmentId,
                                        lightWidth,
                                        hasRightSegment,
                                        offsetScreenPos,
                                        pedestrianWidth,
                                        zoom,
                                        lightHeight,
                                        segmentLight,
                                        hoveredSegment);
                                }

                                // right arrow light
                                if (hasRightSegment) {
                                    hoveredSegment = RightArrowLightMode(
                                        segmentId,
                                        offsetScreenPos,
                                        lightWidth,
                                        pedestrianWidth,
                                        zoom,
                                        lightHeight,
                                        segmentLight,
                                        hoveredSegment);
                                }

                                break;
                            }
                        } // end switch
                    } // end foreach all vehicle type
                } // end for all 8 segments
            } // end if a node is selected

            if (hoveredSegment) {
                return;
            }

            hoveredButton[0] = 0;
            hoveredButton[1] = 0;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
            if (SelectedNodeId != 0) {
                RenderManualNodeOverlays(cameraInfo);
            } else {
                RenderManualSelectionOverlay(cameraInfo);
            }
        }

        private bool RenderManualPedestrianLightSwitch(float zoom,
                                                       int segmentId,
                                                       Vector3 screenPos,
                                                       float lightWidth,
                                                       ICustomSegmentLights segmentLights,
                                                       bool hoveredSegment)
        {
            if (segmentLights.PedestrianLightState == null) {
                return false;
            }

            Color guiColor = GUI.color;
            float manualPedestrianWidth = 36f * zoom;
            float manualPedestrianHeight = 35f * zoom;

            guiColor.a = TrafficManagerTool.GetHandleAlpha(
                hoveredButton[0] == segmentId
                && (hoveredButton[1] == 1
                    || hoveredButton[1] == 2));

            GUI.color = guiColor;

            var myRect2 = new Rect(
                screenPos.x - (manualPedestrianWidth / 2) - lightWidth + (5f * zoom),
                screenPos.y - (manualPedestrianHeight / 2) - (9f * zoom),
                manualPedestrianWidth,
                manualPedestrianHeight);

            GUI.DrawTexture(
                myRect2,
                segmentLights.ManualPedestrianMode
                    ? TrafficLightTextures.PedestrianModeManual
                    : TrafficLightTextures.PedestrianModeAutomatic);

            if (!myRect2.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 1;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            segmentLights.ManualPedestrianMode = !segmentLights.ManualPedestrianMode;
            return true;
        }

        private bool IsPedestrianLightHovered(Rect myRect3,
                                              int segmentId,
                                              bool hoveredSegment,
                                              ICustomSegmentLights segmentLights)
        {
            if (!myRect3.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            if (segmentLights.PedestrianLightState == null) {
                return false;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 2;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            if (!segmentLights.ManualPedestrianMode) {
                segmentLights.ManualPedestrianMode = true;
            } else {
                segmentLights.ChangeLightPedestrian();
            }

            return true;
        }

        private bool GetHoveredSegment(Rect myRect1,
                                       int segmentId,
                                       bool hoveredSegment,
                                       ICustomSegmentLight segmentDict)
        {
            if (!myRect1.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            // Log.Message("mouse in myRect1");
            hoveredButton[0] = segmentId;
            hoveredButton[1] = -1;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            segmentDict.ToggleMode();
            return true;
        }

        private bool RenderCounter(int segmentId,
                                   Vector3 screenPos,
                                   float modeWidth,
                                   float modeHeight,
                                   float zoom,
                                   ICustomSegmentLights segmentLights,
                                   bool hoveredSegment)
        {
            SetAlpha(segmentId, 0);

            var myRectCounter = new Rect(
                screenPos.x - (modeWidth / 2),
                screenPos.y - (modeHeight / 2) - (6f * zoom),
                modeWidth,
                modeHeight);

            GUI.DrawTexture(myRectCounter, TrafficLightTextures.LightCounter);

            float counterSize = 20f * zoom;

            uint counter = segmentLights.LastChange();

            var myRectCounterNum = new Rect(
                screenPos.x - counterSize + (15f * zoom) + (counter >= 10 ? -5 * zoom : 0f),
                screenPos.y - counterSize + (11f * zoom),
                counterSize,
                counterSize);

            counterStyle.fontSize = (int)(18f * zoom);
            counterStyle.normal.textColor = new Color(1f, 1f, 1f);

            GUI.Label(myRectCounterNum, counter.ToString(), counterStyle);

            if (!myRectCounter.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 0;
            return true;
        }

        private bool SimpleManualSegmentLightMode(int segmentId,
                                                  Vector3 screenPos,
                                                  float lightWidth,
                                                  float pedestrianWidth,
                                                  float zoom,
                                                  float lightHeight,
                                                  ICustomSegmentLight segmentDict,
                                                  bool hoveredSegment)
        {
            SetAlpha(segmentId, 3);

            var myRect4 =
                new Rect(
                    screenPos.x - (lightWidth / 2) - lightWidth - pedestrianWidth + (5f * zoom),
                    screenPos.y - (lightHeight / 2),
                    lightWidth,
                    lightHeight);

            switch (segmentDict.LightMain) {
                case RoadBaseAI.TrafficLightState.Green: {
                    GUI.DrawTexture(myRect4, TrafficLightTextures.GreenLight);
                    break;
                }

                case RoadBaseAI.TrafficLightState.Red: {
                    GUI.DrawTexture(myRect4, TrafficLightTextures.RedLight);
                    break;
                }
            }

            if (!myRect4.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 3;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            segmentDict.ChangeMainLight();
            return true;
        }

        private bool LeftForwardRManualSegmentLightMode(bool hasLeftSegment,
                                                        int segmentId,
                                                        Vector3 screenPos,
                                                        float lightWidth,
                                                        float pedestrianWidth,
                                                        float zoom,
                                                        float lightHeight,
                                                        ICustomSegmentLight segmentDict,
                                                        bool hoveredSegment,
                                                        bool hasForwardSegment,
                                                        bool hasRightSegment)
        {
            if (hasLeftSegment) {
                // left arrow light
                SetAlpha(segmentId, 3);

                var myRect4 =
                    new Rect(
                        screenPos.x - (lightWidth / 2) - (lightWidth * 2) - pedestrianWidth + (5f * zoom),
                        screenPos.y - (lightHeight / 2),
                        lightWidth,
                        lightHeight);

                switch (segmentDict.LightLeft) {
                    case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(myRect4, TrafficLightTextures.GreenLightLeft);
                        break;
                    }

                    case RoadBaseAI.TrafficLightState.Red: {
                        GUI.DrawTexture(myRect4, TrafficLightTextures.RedLightLeft);
                        break;
                    }
                }

                if (myRect4.Contains(Event.current.mousePosition)) {
                    hoveredButton[0] = segmentId;
                    hoveredButton[1] = 3;
                    hoveredSegment = true;

                    if (MainTool.CheckClicked()) {
                        segmentDict.ChangeLeftLight();
                    }
                }
            }

            // forward-right arrow light
            SetAlpha(segmentId, 4);

            var myRect5 =
                new Rect(
                    screenPos.x - (lightWidth / 2) - lightWidth - pedestrianWidth + (5f * zoom),
                    screenPos.y - (lightHeight / 2),
                    lightWidth,
                    lightHeight);

            if (hasForwardSegment && hasRightSegment) {
                switch (segmentDict.LightMain) {
                    case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(myRect5, TrafficLightTextures.GreenLightForwardRight);
                        break;
                    }

                    case RoadBaseAI.TrafficLightState.Red: {
                        GUI.DrawTexture(myRect5, TrafficLightTextures.RedLightForwardRight);
                        break;
                    }
                }
            } else if (!hasRightSegment) {
                switch (segmentDict.LightMain) {
                    case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(myRect5, TrafficLightTextures.GreenLightStraight);
                        break;
                    }

                    case RoadBaseAI.TrafficLightState.Red: {
                        GUI.DrawTexture(myRect5, TrafficLightTextures.RedLightStraight);
                        break;
                    }
                }
            } else {
                switch (segmentDict.LightMain) {
                    case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(myRect5, TrafficLightTextures.GreenLightRight);
                        break;
                    }

                    case RoadBaseAI.TrafficLightState.Red: {
                        GUI.DrawTexture(myRect5, TrafficLightTextures.RedLightRight);
                        break;
                    }
                }
            }

            if (!myRect5.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 4;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            segmentDict.ChangeMainLight();
            return true;
        }

        private bool RightForwardLSegmentLightMode(int segmentId,
                                                   Vector3 screenPos,
                                                   float lightWidth,
                                                   float pedestrianWidth,
                                                   float zoom,
                                                   float lightHeight,
                                                   bool hasForwardSegment,
                                                   bool hasLeftSegment,
                                                   ICustomSegmentLight segmentDict,
                                                   bool hasRightSegment,
                                                   bool hoveredSegment)
        {
            SetAlpha(segmentId, 3);

            var myRect4 = new Rect(
                screenPos.x - (lightWidth / 2) - (lightWidth * 2) - pedestrianWidth + (5f * zoom),
                screenPos.y - (lightHeight / 2),
                lightWidth,
                lightHeight);

            if (hasForwardSegment && hasLeftSegment) {
                switch (segmentDict.LightLeft) {
                    case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(myRect4, TrafficLightTextures.GreenLightForwardLeft);
                        break;
                    }

                    case RoadBaseAI.TrafficLightState.Red: {
                        GUI.DrawTexture(myRect4, TrafficLightTextures.RedLightForwardLeft);
                        break;
                    }
                }
            } else if (!hasLeftSegment) {
                if (!hasRightSegment) {
                    myRect4 = new Rect(
                        screenPos.x - (lightWidth / 2) - lightWidth - pedestrianWidth + (5f * zoom),
                        screenPos.y - (lightHeight / 2),
                        lightWidth,
                        lightHeight);
                }

                switch (segmentDict.LightMain) {
                    case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(myRect4, TrafficLightTextures.GreenLightStraight);
                        break;
                    }

                    case RoadBaseAI.TrafficLightState.Red: {
                        GUI.DrawTexture(myRect4, TrafficLightTextures.RedLightStraight);
                        break;
                    }
                }
            } else {
                if (!hasRightSegment) {
                    myRect4 = new Rect(
                        screenPos.x - (lightWidth / 2) - lightWidth - pedestrianWidth + (5f * zoom),
                        screenPos.y - (lightHeight / 2),
                        lightWidth,
                        lightHeight);
                }

                switch (segmentDict.LightMain) {
                    case RoadBaseAI.TrafficLightState.Green: {
                        GUI.DrawTexture(myRect4, TrafficLightTextures.GreenLightLeft);
                        break;
                    }

                    case RoadBaseAI.TrafficLightState.Red: {
                        GUI.DrawTexture(myRect4, TrafficLightTextures.RedLightLeft);
                        break;
                    }
                }
            }

            if (myRect4.Contains(Event.current.mousePosition)) {
                hoveredButton[0] = segmentId;
                hoveredButton[1] = 3;
                hoveredSegment = true;

                if (MainTool.CheckClicked()) {
                    segmentDict.ChangeMainLight();
                }
            }

            Color guiColor = GUI.color;

            // right arrow light
            if (hasRightSegment) {
                guiColor.a = TrafficManagerTool.GetHandleAlpha(
                    hoveredButton[0] == segmentId && hoveredButton[1] == 4);
            }

            GUI.color = guiColor;

            var myRect5 =
                new Rect(
                    screenPos.x - (lightWidth / 2) - lightWidth - pedestrianWidth + (5f * zoom),
                    screenPos.y - (lightHeight / 2),
                    lightWidth,
                    lightHeight);

            switch (segmentDict.LightRight) {
                case RoadBaseAI.TrafficLightState.Green: {
                    GUI.DrawTexture(myRect5, TrafficLightTextures.GreenLightRight);
                    break;
                }

                case RoadBaseAI.TrafficLightState.Red: {
                    GUI.DrawTexture(myRect5, TrafficLightTextures.RedLightRight);
                    break;
                }
            }

            if (!myRect5.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 4;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            segmentDict.ChangeRightLight();
            return true;
        }

        private bool LeftArrowLightMode(int segmentId,
                                        float lightWidth,
                                        bool hasRightSegment,
                                        bool hasForwardSegment,
                                        Vector3 screenPos,
                                        float pedestrianWidth,
                                        float zoom,
                                        float lightHeight,
                                        ICustomSegmentLight segmentDict,
                                        bool hoveredSegment)
        {
            SetAlpha(segmentId, 3);

            float offsetLight = lightWidth;

            if (hasRightSegment) {
                offsetLight += lightWidth;
            }

            if (hasForwardSegment) {
                offsetLight += lightWidth;
            }

            var myRect4 =
                new Rect(
                    screenPos.x - (lightWidth / 2) - offsetLight - pedestrianWidth + (5f * zoom),
                    screenPos.y - (lightHeight / 2),
                    lightWidth,
                    lightHeight);

            switch (segmentDict.LightLeft) {
                case RoadBaseAI.TrafficLightState.Green:
                    GUI.DrawTexture(myRect4, TrafficLightTextures.GreenLightLeft);
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    GUI.DrawTexture(myRect4, TrafficLightTextures.RedLightLeft);
                    break;
            }

            if (!myRect4.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 3;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            segmentDict.ChangeLeftLight();

            if (!hasForwardSegment) {
                segmentDict.ChangeMainLight();
            }

            return true;
        }

        private bool ForwardArrowLightMode(int segmentId,
                                           float lightWidth,
                                           bool hasRightSegment,
                                           Vector3 screenPos,
                                           float pedestrianWidth,
                                           float zoom,
                                           float lightHeight,
                                           ICustomSegmentLight segmentDict,
                                           bool hoveredSegment)
        {
            SetAlpha(segmentId, 4);

            float offsetLight = lightWidth;

            if (hasRightSegment) {
                offsetLight += lightWidth;
            }

            var myRect6 =
                new Rect(
                    screenPos.x - (lightWidth / 2) - offsetLight - pedestrianWidth + (5f * zoom),
                    screenPos.y - (lightHeight / 2),
                    lightWidth,
                    lightHeight);

            switch (segmentDict.LightMain) {
                case RoadBaseAI.TrafficLightState.Green: {
                    GUI.DrawTexture(myRect6, TrafficLightTextures.GreenLightStraight);
                    break;
                }

                case RoadBaseAI.TrafficLightState.Red: {
                    GUI.DrawTexture(myRect6, TrafficLightTextures.RedLightStraight);
                    break;
                }
            }

            if (!myRect6.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 4;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            segmentDict.ChangeMainLight();
            return true;
        }

        private bool RightArrowLightMode(int segmentId,
                                         Vector3 screenPos,
                                         float lightWidth,
                                         float pedestrianWidth,
                                         float zoom,
                                         float lightHeight,
                                         ICustomSegmentLight segmentDict,
                                         bool hoveredSegment)
        {
            SetAlpha(segmentId, 5);

            var myRect5 =
                new Rect(
                    screenPos.x - (lightWidth / 2) - lightWidth - pedestrianWidth + (5f * zoom),
                    screenPos.y - (lightHeight / 2),
                    lightWidth,
                    lightHeight);

            switch (segmentDict.LightRight) {
                case RoadBaseAI.TrafficLightState.Green: {
                    GUI.DrawTexture(myRect5, TrafficLightTextures.GreenLightRight);
                    break;
                }

                case RoadBaseAI.TrafficLightState.Red: {
                    GUI.DrawTexture(myRect5, TrafficLightTextures.RedLightRight);
                    break;
                }
            }

            if (!myRect5.Contains(Event.current.mousePosition)) {
                return hoveredSegment;
            }

            hoveredButton[0] = segmentId;
            hoveredButton[1] = 5;

            if (!MainTool.CheckClicked()) {
                return true;
            }

            segmentDict.ChangeRightLight();
            return true;
        }

        // Not used
        [UsedImplicitly]
        private Vector3 CalculateNodePositionForSegment(NetNode node, int segmentId) {
            Vector3 position = node.m_position;

            NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            if (segment.m_startNode == SelectedNodeId) {
                position.x += segment.m_startDirection.x * 10f;
                position.y += segment.m_startDirection.y * 10f;
                position.z += segment.m_startDirection.z * 10f;
            } else {
                position.x += segment.m_endDirection.x * 10f;
                position.y += segment.m_endDirection.y * 10f;
                position.z += segment.m_endDirection.z * 10f;
            }

            return position;
        }

        private Vector3 CalculateNodePositionForSegment(NetNode node, ref NetSegment segment) {
            Vector3 position = node.m_position;

            const float offset = 25f;

            if (segment.m_startNode == SelectedNodeId) {
                position.x += segment.m_startDirection.x * offset;
                position.y += segment.m_startDirection.y * offset;
                position.z += segment.m_startDirection.z * offset;
            } else {
                position.x += segment.m_endDirection.x * offset;
                position.y += segment.m_endDirection.y * offset;
                position.z += segment.m_endDirection.z * offset;
            }

            return position;
        }

        private void SetAlpha(int segmentId, int buttonId) {
            Color guiColor = GUI.color;

            guiColor.a = TrafficManagerTool.GetHandleAlpha(
                hoveredButton[0] == segmentId
                && hoveredButton[1] == buttonId);

            GUI.color = guiColor;
        }

        private void RenderManualSelectionOverlay(RenderManager.CameraInfo cameraInfo) {
            if (HoveredNodeId == 0) {
                return;
            }

            MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, false, false);

            // var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>
            //  .instance.m_nodes.m_buffer[HoveredNodeId].m_segment0];
            //
            // //if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) return;
            // Bezier3 bezier;
            // bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;
            // bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;
            //
            // var color = MainTool.GetToolColor(false, false);
            //
            // NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
            //         segment.m_endDirection, false, false, out bezier.b, out bezier.c);
            // MainTool.DrawOverlayBezier(cameraInfo, bezier, color);
        }

        private void RenderManualNodeOverlays(RenderManager.CameraInfo cameraInfo) {
            if (!TrafficLightSimulationManager.Instance.HasManualSimulation(SelectedNodeId)) {
                return;
            }

            MainTool.DrawNodeCircle(cameraInfo, SelectedNodeId, true, false);
        }

        public override void Cleanup() {
            if (SelectedNodeId == 0) return;
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            if (!tlsMan.HasManualSimulation(SelectedNodeId)) {
                return;
            }

            tlsMan.RemoveNodeFromSimulation(SelectedNodeId, true, false);
        }
    }
}
