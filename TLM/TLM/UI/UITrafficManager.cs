using System;
using System.Linq;
using ColossalFramework;
using ColossalFramework.UI;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.UI
{
    public class UITrafficManager : UIPanel
    {
        private static UIState _uiState = UIState.None;

        private static bool _inited;

        public static UIState UIState
        {
            set
            {
                if (value == UIState.None && _inited)
                {
                    _buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";
                    _buttonPrioritySigns.focusedBgSprite = "ButtonMenu";
                    _buttonManualControl.focusedBgSprite = "ButtonMenu";
                    _buttonTimedMain.focusedBgSprite = "ButtonMenu";


                    //buttonLaneRestrictions.focusedBgSprite = "ButtonMenu";
                    _buttonCrosswalk.focusedBgSprite = "ButtonMenu";
                    _buttonClearTraffic.focusedBgSprite = "ButtonMenu";
                    if (LoadingExtension.IsPathManagerCompatibile)
                    {
                        _buttonLaneChange.focusedBgSprite = "ButtonMenu";
                        _buttonToggleDespawn.focusedBgSprite = "ButtonMenu";
                    }
                }

                _uiState = value;
            }
            get { return _uiState; }
        }

        private static UIButton _buttonSwitchTraffic;
        private static UIButton _buttonPrioritySigns;
        private static UIButton _buttonManualControl;
        private static UIButton _buttonTimedMain;
        private static UIButton _buttonLaneChange;
        private static UIButton _buttonLaneRestrictions;
        private static UIButton _buttonCrosswalk;
        private static UIButton _buttonClearTraffic;
        private static UIButton _buttonToggleDespawn;

        public static TrafficLightTool TrafficLightTool;

        public override void Start()
        {
            _inited = true;

            TrafficLightTool = LoadingExtension.Instance.TrafficLightTool;

            backgroundSprite = "GenericPanel";
            color = new Color32(75, 75, 135, 255);
            width = 250;
            height = LoadingExtension.IsPathManagerCompatibile ? 350 : 270;
            relativePosition = new Vector3(10.48f, 80f);

            UILabel title = AddUIComponent<UILabel>();
            title.text = "Traffic Manager";
            title.relativePosition = new Vector3(65.0f, 5.0f);

            if (LoadingExtension.IsPathManagerCompatibile)
            {
                _buttonSwitchTraffic = _createButton("Switch traffic lights", new Vector3(35f, 30f), clickSwitchTraffic);
                _buttonPrioritySigns = _createButton("Add priority signs", new Vector3(35f, 70f), clickAddPrioritySigns);
                _buttonManualControl = _createButton("Manual traffic lights", new Vector3(35f, 110f), clickManualControl);
                _buttonTimedMain = _createButton("Timed traffic lights", new Vector3(35f, 150f), clickTimedAdd);
                _buttonLaneChange = _createButton("Change lanes", new Vector3(35f, 190f), clickChangeLanes);
                //buttonLaneRestrictions = _createButton("Road Restrictions", new Vector3(35f, 230f), clickLaneRestrictions);
                _buttonCrosswalk = _createButton("Add/Remove Crosswalk", new Vector3(35f, 230f), clickCrosswalk);
                _buttonClearTraffic = _createButton("Clear Traffic", new Vector3(35f, 270f), clickClearTraffic);
                _buttonToggleDespawn = _createButton(LoadingExtension.Instance.DespawnEnabled ? "Disable despawning" : "Enable despawning", new Vector3(35f, 310f), ClickToggleDespawn);

            }
            else
            {
                _buttonSwitchTraffic = _createButton("Switch traffic lights", new Vector3(35f, 30f), clickSwitchTraffic);
                _buttonPrioritySigns = _createButton("Add priority signs", new Vector3(35f, 70f), clickAddPrioritySigns);
                _buttonManualControl = _createButton("Manual traffic lights", new Vector3(35f, 110f), clickManualControl);
                _buttonTimedMain = _createButton("Timed traffic lights", new Vector3(35f, 150f), clickTimedAdd);
                _buttonCrosswalk = _createButton("Add/Remove Crosswalk", new Vector3(35f, 190f), clickCrosswalk);
                _buttonClearTraffic = _createButton("Clear Traffic", new Vector3(35f, 230f), clickClearTraffic);
            }
        }

        private UIButton _createButton(string text, Vector3 pos, MouseEventHandler eventClick)
        {
            var button = AddUIComponent<UIButton>();
            button.width = 190;
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
            if (_uiState != UIState.SwitchTrafficLight)
            {
                _uiState = UIState.SwitchTrafficLight;

                _buttonSwitchTraffic.focusedBgSprite = "ButtonMenuFocused";

                TrafficLightTool.SetToolMode(ToolMode.SwitchTrafficLight);
            }
            else
            {
                _uiState = UIState.None;

                _buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.SetToolMode(ToolMode.None);
            }
        }

        private void clickAddPrioritySigns(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uiState != UIState.AddStopSign)
            {
                _uiState = UIState.AddStopSign;

                _buttonPrioritySigns.focusedBgSprite = "ButtonMenuFocused";

                TrafficLightTool.SetToolMode(ToolMode.AddPrioritySigns);
            }
            else
            {
                _uiState = UIState.None;

                _buttonPrioritySigns.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.SetToolMode(ToolMode.None);
            }
        }

        private void clickManualControl(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uiState != UIState.ManualSwitch)
            {
                _uiState = UIState.ManualSwitch;

                _buttonManualControl.focusedBgSprite = "ButtonMenuFocused";

                TrafficLightTool.SetToolMode(ToolMode.ManualSwitch);
            }
            else
            {
                _uiState = UIState.None;

                _buttonManualControl.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.SetToolMode(ToolMode.None);
            }
        }

        private void clickTimedAdd(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uiState != UIState.TimedControlNodes)
            {
                _uiState = UIState.TimedControlNodes;

                _buttonTimedMain.focusedBgSprite = "ButtonMenuFocused";

                TrafficLightTool.SetToolMode(ToolMode.TimedLightsSelectNode);
            }
            else
            {
                _uiState = UIState.None;

                _buttonTimedMain.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.SetToolMode(ToolMode.None);
            }
        }

        private void clickClearTraffic(UIComponent component, UIMouseEventParameter eventParam)
        {
            var vehicleList = TrafficPriority.VehicleList.Keys.ToList();

            lock (Singleton<VehicleManager>.instance)
            {
                foreach (var vehicle in
                    from vehicle in vehicleList
                    let vehicleData = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicle]
                    where vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car
                    select vehicle)
                {
                    Singleton<VehicleManager>.instance.ReleaseVehicle(vehicle);
                }
            }
        }

        private static void ClickToggleDespawn(UIComponent component, UIMouseEventParameter eventParam)
        {
            LoadingExtension.Instance.DespawnEnabled = !LoadingExtension.Instance.DespawnEnabled;

            if (LoadingExtension.IsPathManagerCompatibile)
            {
                _buttonToggleDespawn.text = LoadingExtension.Instance.DespawnEnabled
                    ? "Disable despawning"
                    : "Enable despawning";
            }
        }

        private void clickChangeLanes(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uiState != UIState.LaneChange)
            {
                _uiState = UIState.LaneChange;

                if (LoadingExtension.IsPathManagerCompatibile)
                {
                    _buttonLaneChange.focusedBgSprite = "ButtonMenuFocused";
                }

                TrafficLightTool.SetToolMode(ToolMode.LaneChange);
            }
            else
            {
                _uiState = UIState.None;

                if (LoadingExtension.IsPathManagerCompatibile)
                {
                    _buttonLaneChange.focusedBgSprite = "ButtonMenu";
                }

                TrafficLightTool.SetToolMode(ToolMode.None);
            }
        }

        protected virtual void ClickLaneRestrictions(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uiState != UIState.LaneRestrictions)
            {
                _uiState = UIState.LaneRestrictions;

                _buttonLaneRestrictions.focusedBgSprite = "ButtonMenuFocused";

                TrafficLightTool.SetToolMode(ToolMode.LaneRestrictions);
            }
            else
            {
                _uiState = UIState.None;

                _buttonLaneRestrictions.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.SetToolMode(ToolMode.None);
            }
        }

        private void clickCrosswalk(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uiState != UIState.Crosswalk)
            {
                _uiState = UIState.Crosswalk;

                _buttonCrosswalk.focusedBgSprite = "ButtonMenuFocused";

                TrafficLightTool.SetToolMode(ToolMode.Crosswalk);
            }
            else
            {
                _uiState = UIState.None;

                _buttonCrosswalk.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.SetToolMode(ToolMode.None);
            }
        }

        public override void Update()
        {
            switch (_uiState)
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
                var instance = Singleton<NetManager>.instance;

                var segment = instance.m_segments.m_buffer[TrafficLightTool.SelectedSegment];

                var info = segment.Info;

                var num2 = segment.m_lanes;
                var num3 = 0;

                var dir = NetInfo.Direction.Forward;
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
                    }

                    num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                    num3++;
                }
            }
        }

        public void SwitchLane(uint laneId)
        {
            var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags;

            if ((flags & NetLane.Flags.LeftForwardRight) == NetLane.Flags.LeftForwardRight)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.LeftForwardRight) | NetLane.Flags.Forward);
            }
            else if ((flags & NetLane.Flags.ForwardRight) == NetLane.Flags.ForwardRight)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.ForwardRight) | NetLane.Flags.LeftForwardRight);
            }
            else if ((flags & NetLane.Flags.LeftRight) == NetLane.Flags.LeftRight)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.LeftRight) | NetLane.Flags.ForwardRight);
            }
            else if ((flags & NetLane.Flags.LeftForward) == NetLane.Flags.LeftForward)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.LeftForward) | NetLane.Flags.LeftRight);
            }
            else if ((flags & NetLane.Flags.Right) == NetLane.Flags.Right)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.Right) | NetLane.Flags.LeftForward);
            }
            else if ((flags & NetLane.Flags.Left) == NetLane.Flags.Left)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.Left) | NetLane.Flags.Right);
            }
            else if ((flags & NetLane.Flags.Forward) == NetLane.Flags.Forward)
            {
                Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags =
                    (ushort) ((flags & ~NetLane.Flags.Forward) | NetLane.Flags.Left);
            }
        }

        private void _crosswalkPanel()
        {
        }
    }
}
