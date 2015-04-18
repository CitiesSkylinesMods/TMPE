using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;

namespace TrafficManager
{
    public class UITrafficManager : UIPanel
    {
        public enum UIState
        {
            None,
            SwitchTrafficLight,
            AddStopSign,
            ManualSwitch,
            TimedControlNodes,
            TimedControlLights,
            LaneChange,
            LaneRestrictions,
            Crosswalk
        }

        private static UIState _uistate = UIState.None;

        public static UIState uistate
        {
            set
            {
                if (value == UIState.None)
                {
                    buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";
                    buttonPrioritySigns.focusedBgSprite = "ButtonMenu";
                    buttonManualControl.focusedBgSprite = "ButtonMenu";
                    buttonTimedMain.focusedBgSprite = "ButtonMenu";
                    buttonLaneChange.focusedBgSprite = "ButtonMenu";
                    //buttonLaneRestrictions.focusedBgSprite = "ButtonMenu";
                    buttonCrosswalk.focusedBgSprite = "ButtonMenu";
                    buttonClearTraffic.focusedBgSprite = "ButtonMenu";
                    buttonToggleDespawn.focusedBgSprite = "ButtonMenu";
                }

                _uistate = value;
            }
            get { return _uistate; }
        }

        private static UIButton buttonSwitchTraffic;
        private static UIButton buttonPrioritySigns;
        private static UIButton buttonManualControl;
        private static UIButton buttonTimedMain;
        private static UIButton buttonLaneChange;
        private static UIButton buttonLaneRestrictions;
        private static UIButton buttonCrosswalk;
        private static UIButton buttonClearTraffic;
        private static UIButton buttonToggleDespawn;

        public static TrafficLightTool trafficLightTool;

        public override void Start()
        {
            trafficLightTool = LoadingExtension.Instance.TrafficLightTool;

            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(75, 75, 135, 255);
            this.width = 250;
            this.height = 350;
            this.relativePosition = new Vector3(10.48f, 80f);

            UILabel title = this.AddUIComponent<UILabel>();
            title.text = "Traffic Manager";
            title.relativePosition = new Vector3(65.0f, 5.0f);

            buttonSwitchTraffic = _createButton("Switch traffic lights", new Vector3(35f, 30f), clickSwitchTraffic);
            buttonPrioritySigns = _createButton("Add priority signs", new Vector3(35f, 70f), clickAddPrioritySigns);
            buttonManualControl = _createButton("Manual Control", new Vector3(35f, 110f), clickManualControl);
            buttonTimedMain = _createButton("Timed traffic lights", new Vector3(35f, 150f), clickTimedAdd);
            buttonLaneChange = _createButton("Change lanes", new Vector3(35f, 190f), clickChangeLanes);
            //buttonLaneRestrictions = _createButton("Road Restrictions", new Vector3(35f, 230f), clickLaneRestrictions);
            buttonCrosswalk = _createButton("Add/Remove Crosswalk", new Vector3(35f, 230f), clickCrosswalk);
            buttonClearTraffic = _createButton("Clear Traffic", new Vector3(35f, 270f), clickClearTraffic);
            buttonToggleDespawn = _createButton(LoadingExtension.Instance.despawnEnabled ? "Disable despawning" : "Enable despawning", new Vector3(35f, 310f), clickToggleDespawn);
        }

        private UIButton _createButton(string text, Vector3 pos, MouseEventHandler eventClick)
        {
            var button = this.AddUIComponent<UIButton>();
            button.width = 180;
            button.height = 30;
            button.normalBgSprite = "ButtonMenu";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.focusedBgSprite = "ButtonMenu";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textColor = new Color32(255, 255, 255, 255);
            button.playAudioEvents = true;
            button.text = text;
            button.relativePosition = pos;
            button.eventClick += eventClick;

            return button;
        }

        private void clickSwitchTraffic(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.SwitchTrafficLight)
            {
                _uistate = UIState.SwitchTrafficLight;

                buttonSwitchTraffic.focusedBgSprite = "ButtonMenuFocused";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.SwitchTrafficLight);
            }
            else
            {
                _uistate = UIState.None;

                buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickAddPrioritySigns(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.AddStopSign)
            {
                _uistate = UIState.AddStopSign;

                buttonPrioritySigns.focusedBgSprite = "ButtonMenuFocused";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.AddPrioritySigns);
            }
            else
            {
                _uistate = UIState.None;

                buttonPrioritySigns.focusedBgSprite = "ButtonMenu";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickManualControl(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.ManualSwitch)
            {
                _uistate = UIState.ManualSwitch;

                buttonManualControl.focusedBgSprite = "ButtonMenuFocused";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.ManualSwitch);
            }
            else
            {
                _uistate = UIState.None;

                buttonManualControl.focusedBgSprite = "ButtonMenu";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickTimedAdd(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.TimedControlNodes)
            {
                _uistate = UIState.TimedControlNodes;

                buttonTimedMain.focusedBgSprite = "ButtonMenuFocused";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.TimedLightsSelectNode);
            }
            else
            {
                _uistate = UIState.None;

                buttonTimedMain.focusedBgSprite = "ButtonMenu";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickClearTraffic(UIComponent component, UIMouseEventParameter eventParam)
        {
            List<ushort> vehicleList = new List<ushort>();

            foreach (var vehicleID in TrafficPriority.vehicleList.Keys)
            {
                vehicleList.Add(vehicleID);
            }

            lock (Singleton<VehicleManager>.instance)
            {
                for (var i = 0; i < vehicleList.Count; i++)
                {
                    Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleList[i]);
                }
            }
        }

        private void clickToggleDespawn(UIComponent component, UIMouseEventParameter eventParam)
        {
            LoadingExtension.Instance.despawnEnabled = !LoadingExtension.Instance.despawnEnabled;

            buttonToggleDespawn.text = LoadingExtension.Instance.despawnEnabled
                ? "Disable despawning"
                : "Enable despawning";
        }

        private void clickChangeLanes(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.LaneChange)
            {
                _uistate = UIState.LaneChange;

                buttonLaneChange.focusedBgSprite = "ButtonMenuFocused";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.LaneChange);
            }
            else
            {
                _uistate = UIState.None;

                buttonLaneChange.focusedBgSprite = "ButtonMenu";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickLaneRestrictions(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.LaneRestrictions)
            {
                _uistate = UIState.LaneRestrictions;

                buttonLaneRestrictions.focusedBgSprite = "ButtonMenuFocused";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.LaneRestrictions);
            }
            else
            {
                _uistate = UIState.None;

                buttonLaneRestrictions.focusedBgSprite = "ButtonMenu";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickCrosswalk(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.Crosswalk)
            {
                _uistate = UIState.Crosswalk;

                buttonCrosswalk.focusedBgSprite = "ButtonMenuFocused";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.Crosswalk);
            }
            else
            {
                _uistate = UIState.None;

                buttonCrosswalk.focusedBgSprite = "ButtonMenu";

                LoadingExtension.Instance.TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        public override void Update()
        {
            switch (_uistate)
            {
                case UIState.None: _basePanel(); break;
                case UIState.SwitchTrafficLight: _switchTrafficPanel(); break;
                case UIState.AddStopSign: _addStopSignPanel(); break;
                case UIState.ManualSwitch: _manualSwitchPanel(); break;
                case UIState.TimedControlNodes: _timedControlNodesPanel(); break;
                case UIState.TimedControlLights: _timedControlLightsPanel(); break;
                case UIState.LaneChange: _laneChangePanel(); break;
                case UIState.Crosswalk: _crosswalkPanel(); break;
            }
        }

        private void _basePanel()
        {

        }

        private void _switchTrafficPanel()
        {

        }

        private void _addStopSignPanel()
        {

        }

        private void _manualSwitchPanel()
        {

        }

        private void _timedControlNodesPanel()
        {
        }

        private void _timedControlLightsPanel()
        {
        }

        private void _laneChangePanel()
        {
            if (TrafficLightTool.SelectedSegment != 0)
            {
                NetManager instance = Singleton<NetManager>.instance;

                var segment = instance.m_segments.m_buffer[TrafficLightTool.SelectedSegment];

                var info = segment.Info;

                uint num2 = segment.m_lanes;
                int num3 = 0;

                int offsetIdx = 0;

                NetInfo.Direction dir = NetInfo.Direction.Forward;
                if (segment.m_startNode == TrafficLightTool.SelectedNode)
                    dir = NetInfo.Direction.Backward;
                var dir3 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);

                while (num3 < info.m_lanes.Length && num2 != 0u)
                {
                    if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian && info.m_lanes[num3].m_direction == dir3)
                    {
                        //segmentLights[num3].Show();
                        //segmentLights[num3].relativePosition = new Vector3(35f, (float)(xPos + (offsetIdx * 40f)));
                        //segmentLights[num3].text = ((NetLane.Flags)instance.m_lanes.m_buffer[num2].m_flags & ~NetLane.Flags.Created).ToString();

                        //if (segmentLights[num3].containsMouse)
                        //{
                        //    if (Input.GetMouseButton(0) && !segmentMouseDown)
                        //    {
                        //        switchLane(num2);
                        //        segmentMouseDown = true;

                        //        if (
                        //            !TrafficPriority.isPrioritySegment(TrafficLightTool.SelectedNode,
                        //                TrafficLightTool.SelectedSegment))
                        //        {
                        //            TrafficPriority.addPrioritySegment(TrafficLightTool.SelectedNode, TrafficLightTool.SelectedSegment, PrioritySegment.PriorityType.None);
                        //        }
                        //    }
                        //}

                        offsetIdx++;
                    }

                    num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                    num3++;
                }
            }
        }

        public void switchLane(uint laneID)
        {
            var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneID].m_flags;

            if ((flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.LeftForwardRight)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneID].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.LeftForwardRight) | NetLane.Flags.Forward);
            }
            else if ((flags & NetLane.Flags.ForwardRight) == NetLane.Flags.ForwardRight)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneID].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.ForwardRight) | NetLane.Flags.LeftForwardRight);
            }
            else if ((flags & NetLane.Flags.LeftRight) == NetLane.Flags.LeftRight)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneID].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.LeftRight) | NetLane.Flags.ForwardRight);
            }
            else if ((flags & NetLane.Flags.LeftForward) == NetLane.Flags.LeftForward)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneID].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.LeftForward) | NetLane.Flags.LeftRight);
            }
            else if ((flags & NetLane.Flags.Right) == NetLane.Flags.Right)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneID].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.Right) | NetLane.Flags.LeftForward);
            }
            else if ((flags & NetLane.Flags.Left) == NetLane.Flags.Left)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneID].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.Left) | NetLane.Flags.Right);
            }
            else if ((flags & NetLane.Flags.Forward) == NetLane.Flags.Forward)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneID].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.Forward) | NetLane.Flags.Left);
            }
        }

        private void _crosswalkPanel()
        {
        }
    }
}
