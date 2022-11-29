// #define QUEUEDSTATS

namespace TrafficManager.UI.MainMenu {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using global::TrafficManager.API.Manager;
    using global::TrafficManager.Custom.PathFinding;
    using global::TrafficManager.State;
    using JetBrains.Annotations;
    using UnityEngine;
    using TrafficManager.Util;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;
    using TrafficManager.U;

#if DEBUG // whole class coverage
    using TrafficManager.UI.DebugSwitches;

    public class DebugMenuPanel : UIPanel
    {
        // private static UIState _uiState = UIState.None;

#if QUEUEDSTATS
        private static bool showPathFindStats;
#endif

#if DEBUG
        private static UITextField _goToField;
        private static UIButton _goToSegmentButton;
        private static UIButton _goToNodeButton;
        private static UIButton _goToVehicleButton;
        private static UIButton _goToParkedVehicleButton;
        private static UIButton _goToBuildingButton;
        private static UIButton _goToCitizenInstanceButton;
        private static UIButton _goToPosButton;
        private static UIButton _printDebugInfoButton;
        private static UIButton _reloadConfigButton;
        private static UIButton _debugSwitchesButton;
        private static UIButton _recalcLinesButton;
        private static UIButton _checkDetoursButton;
        private static UIButton _noneToVehicleButton = null;
        private static UIButton _vehicleToNoneButton = null;
        private static UIButton _printFlagsDebugInfoButton;
#endif

#if QUEUEDSTATS
        private static UIButton _togglePathFindStatsButton;
#endif

        private static UILabel _title;

        public override void Start() {
            isVisible = false;
            atlas = TextureUtil.Ingame;

            backgroundSprite = "GenericPanel";
            color = new Color32(75, 75, 135, 255);
            width = Translation.GetMenuWidth();
            height = 30;

            //height = LoadingExtension.IsPathManagerCompatible ? 430 : 230;
            Vector2 resolution = UIView.GetAView().GetScreenResolution();
            relativePosition = new Vector3(resolution.x - Translation.GetMenuWidth() - 30f, 65f);

            _title = AddUIComponent<UILabel>();
            _title.text = "Version " + VersionUtil.VersionString;
            _title.relativePosition = new Vector3(50.0f, 5.0f);

            int y = 30;
#if DEBUG
            _goToField = CreateTextField("", y);
            y += 40;
            height += 40;
            _goToPosButton = CreateButton("Goto position", y, ClickGoToPos);
            y += 40;
            height += 40;
            _goToSegmentButton = CreateButton("Goto segment", y, ClickGoToSegment);
            y += 40;
            height += 40;
            _goToNodeButton = CreateButton("Goto node", y, ClickGoToNode);
            y += 40;
            height += 40;
            _goToVehicleButton = CreateButton("Goto vehicle", y, ClickGoToVehicle);
            y += 40;
            height += 40;
            _goToParkedVehicleButton = CreateButton("Goto parked vehicle", y, ClickGoToParkedVehicle);
            y += 40;
            height += 40;
            _goToBuildingButton = CreateButton("Goto building", y, ClickGoToBuilding);
            y += 40;
            height += 40;
            _goToCitizenInstanceButton = CreateButton("Goto citizen inst.", y, ClickGoToCitizenInstance);
            y += 40;
            height += 40;
            _printDebugInfoButton = CreateButton("Print debug info", y, ClickPrintDebugInfo);
            y += 40;
            height += 40;
            _reloadConfigButton = CreateButton("Reload configuration", y, ClickReloadConfig);
            y += 40;
            height += 40;
            _debugSwitchesButton = CreateButton("Debug switches", y, ClickDebugSwitches);
            y += 40;
            height += 40;
            _recalcLinesButton = CreateButton("Recalculate transport lines", y, ClickRecalcLines);
            y += 40;
            height += 40;
            _checkDetoursButton = CreateButton("Check transport lines", y, clickCheckDetours);
            y += 40;
            height += 40;
#endif
#if QUEUEDSTATS
            _togglePathFindStatsButton = CreateButton("Toggle PathFind stats", y, ClickTogglePathFindStats);
            y += 40;
            height += 40;
#endif
#if DEBUG
            _printFlagsDebugInfoButton = CreateButton("Print flags debug info", y, ClickPrintFlagsDebugInfo);
            y += 40;
            height += 40;
#endif
        }

        private UITextField CreateTextField(string str, int y) {
            UITextField textfield = AddUIComponent<UITextField>();
            textfield.relativePosition = new Vector3(15f, y);
            textfield.horizontalAlignment = UIHorizontalAlignment.Left;
            textfield.text = str;
            textfield.textScale = 0.8f;
            textfield.color = Color.black;
            textfield.cursorBlinkTime = 0.45f;
            textfield.cursorWidth = 1;
            textfield.selectionBackgroundColor = new Color(233, 201, 148, 255);
            textfield.atlas = TextureUtil.Ingame;
            textfield.selectionSprite = "EmptySprite";
            textfield.verticalAlignment = UIVerticalAlignment.Middle;
            textfield.padding = new RectOffset(5, 0, 5, 0);
            textfield.foregroundSpriteMode = UIForegroundSpriteMode.Fill;
            textfield.normalBgSprite = "TextFieldPanel";
            textfield.hoveredBgSprite = "TextFieldPanelHovered";
            textfield.focusedBgSprite = "TextFieldPanel";
            textfield.size = new Vector3(190, 30);
            textfield.isInteractive = true;
            textfield.enabled = true;
            textfield.readOnly = false;
            textfield.builtinKeyNavigation = true;
            textfield.width = Translation.GetMenuWidth() - 30;
            return textfield;
        }

        private UIButton CreateButton(string text, int y, MouseEventHandler eventClick) {
            var button = AddUIComponent<UIButton>();
            button.textScale = 0.8f;
            button.width = Translation.GetMenuWidth() - 30;
            button.height = 30;
            button.atlas = TextureUtil.Ingame;
            button.normalBgSprite = "ButtonMenu";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.focusedBgSprite = "ButtonMenu";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textColor = new Color32(255, 255, 255, 255);
            button.playAudioEvents = true;
            button.text = text;
            button.relativePosition = new Vector3(15f, y);
            button.eventClick += (component, eventParam) => {
                eventClick(component, eventParam);
                button.Invalidate();
            };

            return button;
        }

#if DEBUG
        private void ClickGoToPos(UIComponent component, UIMouseEventParameter eventParam) {
            string[] vectorElms = _goToField.text.Split(',');

            if (vectorElms.Length < 2) {
                return;
            }

            CSUtil.CameraControl.CameraController.Instance.GoToPos(
                new Vector3(
                    float.Parse(vectorElms[0]),
                    InGameUtil.Instance.CachedCameraTransform.position.y,
                    float.Parse(vectorElms[1])));
        }

        private void ClickGoToSegment(UIComponent component, UIMouseEventParameter eventParam) {
            ushort segmentId = Convert.ToUInt16(_goToField.text);
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                CSUtil.CameraControl.CameraController.Instance.GoToSegment(segmentId);
            }
        }

        private void ClickGoToNode(UIComponent component, UIMouseEventParameter eventParam) {
            ushort nodeId = Convert.ToUInt16(_goToField.text);

            if ((nodeId.ToNode().m_flags & NetNode.Flags.Created) != NetNode.Flags.None) {
                CSUtil.CameraControl.CameraController.Instance.GoToNode(nodeId);
            }
        }

        private void ClickPrintDebugInfo(UIComponent component, UIMouseEventParameter eventParam) {
            Singleton<SimulationManager>.instance.AddAction(
                () => {
                    foreach (ICustomManager customManager in TMPELifecycle.Instance
                        .RegisteredManagers) {
                        customManager.PrintDebugInfo();
                    }
                });
        }

        private void ClickReloadConfig(UIComponent component, UIMouseEventParameter eventParam) {
            GlobalConfig.Reload();
        }

        private void ClickDebugSwitches(UIComponent component, UIMouseEventParameter eventParam) {
            DebugSwitchPanel.OpenModal();
        }

        private void ClickRecalcLines(UIComponent component, UIMouseEventParameter eventParam) {
            SimulationManager.instance.AddAction(
                () => {
                    for (int i = 0; i < TransportManager.MAX_LINE_COUNT; ++i) {
                        if (TransportManager.instance.m_lines.m_buffer[i].m_flags == TransportLine.Flags.None) {
                            continue;
                            // Log.Message("\tTransport line is not created.");
                        }

                        Log.Info($"Recalculating transport line {i} now.");
                        if (TransportManager.instance.m_lines.m_buffer[i].UpdatePaths((ushort)i)
                            && TransportManager.instance.m_lines.m_buffer[i].UpdateMeshData((ushort)i)) {
                            Log.Info($"Transport line {i} recalculated.");
                        }
                    }
                });
        }

        private void clickCheckDetours(UIComponent component, UIMouseEventParameter eventParam) {
            SimulationManager.instance.AddAction(PrintTransportStats);
        }

        private static void PrintTransportStats() {
            TransportLine[] linesBuffer = TransportManager.instance.m_lines.m_buffer;

            for (int i = 0; i < TransportManager.MAX_LINE_COUNT; ++i) {
                Log.Info("Transport line " + i + ":");

                if ((linesBuffer[i].m_flags &
                     TransportLine.Flags.Created) == TransportLine.Flags.None) {
                    Log.Info("\tTransport line is not created.");
                    continue;
                }

                Log.InfoFormat(
                    "\tFlags: {0}, cat: {1}, type: {2}, name: {3}",
                    linesBuffer[i].m_flags,
                    linesBuffer[i].Info.category,
                    linesBuffer[i].Info.m_transportType,
                    TransportManager.instance.GetLineName((ushort)i));

                ushort firstStopNodeId = linesBuffer[i].m_stops;
                ushort stopNodeId = firstStopNodeId;
                Vector3 lastNodePos = Vector3.zero;
                int index = 1;

                while (stopNodeId != 0) {
                    ref NetNode stopNode = ref stopNodeId.ToNode();

                    Vector3 pos = stopNode.m_position;

                    Log.InfoFormat(
                        "\tStop node #{0} -- {1}: Flags: {2}, Transport line: {3}, Problems: {4} " +
                        "Pos: {5}, Dist. to lat pos: {6}",
                        index,
                        stopNodeId,
                        stopNode.m_flags,
                        stopNode.m_transportLine,
                        stopNode.m_problems,
                        pos,
                        (lastNodePos - pos).magnitude);

                    if (stopNode.m_problems.IsNotNone) {
                        Log.Warning("\t*** PROBLEMS DETECTED ***");
                    }

                    lastNodePos = pos;

                    ushort nextSegmentId = TransportLine.GetNextSegment(stopNodeId);
                    if (nextSegmentId != 0) {
                        stopNodeId = nextSegmentId.ToSegment().m_endNode;
                    } else {
                        break;
                    }

                    ++index;

                    if (stopNodeId == firstStopNodeId) {
                        break;
                    }

                    if (index > 10000) {
                        Log.Error("Too many iterations!");
                        break;
                    }
                }
            }
        }

        private static Dictionary<string, List<byte>> _customEmergencyLanes
            = new Dictionary<string, List<byte>>();

        [UsedImplicitly]
        private void ClickNoneToVehicle(UIComponent component, UIMouseEventParameter eventParam) {
            Dictionary<NetInfo, ushort> ret = new Dictionary<NetInfo, ushort>();
            int numLoaded = PrefabCollection<NetInfo>.LoadedCount();

            for (uint i = 0; i < numLoaded; ++i) {
                NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);

                if (!(info.m_netAI is RoadBaseAI)) {
                    continue;
                }

                RoadBaseAI ai = (RoadBaseAI)info.m_netAI;

                if (!ai.m_highwayRules) {
                    continue;
                }

                NetInfo.Lane[] laneInfos = info.m_lanes;

                for (byte k = 0; k < Math.Min(2, laneInfos.Length); ++k) {
                    NetInfo.Lane laneInfo = laneInfos[k];

                    if (laneInfo.m_vehicleType == VehicleInfo.VehicleType.None) {
                        laneInfo.m_vehicleType = VehicleInfo.VehicleType.Car;
                        laneInfo.m_laneType = NetInfo.LaneType.Vehicle;
                        Log._Debug(
                            $"Changing vehicle type of lane {k} @ {info.name} from None to Car, " +
                            $"lane type from None to Vehicle");

                        if (!_customEmergencyLanes.ContainsKey(info.name)) {
                            _customEmergencyLanes.Add(info.name, new List<byte>());
                        }

                        _customEmergencyLanes[info.name].Add(k);
                    }
                }
            }
        }
#endif

#if QUEUEDSTATS
        private void ClickTogglePathFindStats(UIComponent component,
                                              UIMouseEventParameter eventParam) {
            showPathFindStats = !showPathFindStats;
        }
#endif

#if DEBUG

        private void ClickPrintFlagsDebugInfo(UIComponent component,
                                              UIMouseEventParameter eventParam) {
            Flags.PrintDebugInfo();
        }

        [UsedImplicitly]
        private void ClickVehicleToNone(UIComponent component, UIMouseEventParameter eventParam) {
            foreach (KeyValuePair<string, List<byte>> e in _customEmergencyLanes) {
                NetInfo info = PrefabCollection<NetInfo>.FindLoaded(e.Key);
                if (info == null) {
                    Log.Warning($"Could not find NetInfo by name {e.Key}");
                    continue;
                }

                foreach (byte index in e.Value) {
                    if (index >= info.m_lanes.Length) {
                        Log.Warning($"Illegal lane index {index} for NetInfo {e.Key}");
                        continue;
                    }

                    Log._Debug($"Resetting vehicle type of lane {index} @ {info.name}");

                    info.m_lanes[index].m_vehicleType = VehicleInfo.VehicleType.None;
                    info.m_lanes[index].m_laneType = NetInfo.LaneType.None;
                }
            }

            _customEmergencyLanes.Clear();
        }

        private void ClickGoToVehicle(UIComponent component, UIMouseEventParameter eventParam) {
            ushort vehicleId = Convert.ToUInt16(_goToField.text);
            ref Vehicle vehicle = ref vehicleId.ToVehicle();
            if (vehicle.IsCreated()) {
                CSUtil.CameraControl.CameraController.Instance.GoToVehicle(vehicleId);
            }
        }

        private void ClickGoToParkedVehicle(UIComponent component, UIMouseEventParameter eventParam) {
            ushort parkedVehicleId = Convert.ToUInt16(_goToField.text);
            if (parkedVehicleId.ToParkedVehicle().IsCreated()) {
                CSUtil.CameraControl.CameraController.Instance.GoToParkedVehicle(parkedVehicleId);
            }
        }

        private void ClickGoToBuilding(UIComponent component, UIMouseEventParameter eventParam) {
            ushort buildingId = Convert.ToUInt16(_goToField.text);
            ref Building building = ref buildingId.ToBuilding();
            if ((building.m_flags & Building.Flags.Created) != 0) {
                CSUtil.CameraControl.CameraController.Instance.GoToBuilding(buildingId);

                //    for (int index = 0;
                //         index < BuildingManager.BUILDINGGRID_RESOLUTION *
                //         BuildingManager.BUILDINGGRID_RESOLUTION;
                //         ++index) {
                //        ushort bid = Singleton<BuildingManager>.instance.m_buildingGrid[index];
                //        while (bid != 0) {
                //            if (bid == buildingId) {
                //                int i = index / BuildingManager.BUILDINGGRID_RESOLUTION;
                //                int j = index % BuildingManager.BUILDINGGRID_RESOLUTION;
                //                Log._Debug(
                //                    $"Found building {buildingId} in building grid @ {index}. i={i}, j={j}");
                //            }
                //
                //            bid = Singleton<BuildingManager>
                //                  .instance.m_buildings.m_buffer[bid].m_nextGridBuilding;
                //        }
                //    }
            }
        }

        private void ClickGoToCitizenInstance(UIComponent component,
                                              UIMouseEventParameter eventParam) {
            ushort citizenInstanceId = Convert.ToUInt16(_goToField.text);
            ref CitizenInstance citizenInstance = ref CitizenManager.instance.m_instances.m_buffer[citizenInstanceId];
            if (citizenInstance.IsCreated()) {
                CSUtil.CameraControl.CameraController.Instance.GoToCitizenInstance(
                    citizenInstanceId);
            }
        }
#endif

        public override void Update() {
#if QUEUEDSTATS
            if (showPathFindStats && _title != null) {
                _title.text = CustomPathManager.TotalQueuedPathFinds.ToString();
            }
#endif
        }
    } // end class
#endif // whole class #if DEBUG
}