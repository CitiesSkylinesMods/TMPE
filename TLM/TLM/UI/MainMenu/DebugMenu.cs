#define QUEUEDSTATSx
#define EXTRAPFx

using System;
using System.Linq;
using ColossalFramework;
using ColossalFramework.UI;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Custom.PathFinding;
using System.Collections.Generic;
using TrafficManager.Manager;
using CSUtil.Commons;

namespace TrafficManager.UI {
#if DEBUG
	public class DebugMenuPanel : UIPanel {
		//private static UIState _uiState = UIState.None;

#if QUEUEDSTATS
		private static bool showPathFindStats = false;
#endif

		private static UIButton _buttonSwitchTraffic;
		private static UIButton _buttonPrioritySigns;
		private static UIButton _buttonManualControl;
		private static UIButton _buttonTimedMain;
		private static UIButton _buttonLaneChange;
		private static UIButton _buttonLaneConnector;
		private static UIButton _buttonVehicleRestrictions;
		private static UIButton _buttonJunctionRestrictions;
		private static UIButton _buttonSpeedLimits;
		private static UIButton _buttonClearTraffic;
		private static UIButton _buttonToggleDespawn;
#if DEBUG
		private static UITextField _goToField = null;
		private static UIButton _goToSegmentButton = null;
		private static UIButton _goToNodeButton = null;
		private static UIButton _goToVehicleButton = null;
		private static UIButton _goToBuildingButton = null;
		private static UIButton _goToCitizenInstanceButton = null;
		private static UIButton _goToPosButton = null;
		private static UIButton _printDebugInfoButton = null;
		private static UIButton _noneToVehicleButton = null;
		private static UIButton _vehicleToNoneButton = null;
		private static UIButton _removeStuckEntitiesButton = null;
#endif

#if QUEUEDSTATS
		private static UIButton _togglePathFindStatsButton = null;
#endif

		public static UILabel title;

		public override void Start() {
			isVisible = false;

			backgroundSprite = "GenericPanel";
			color = new Color32(75, 75, 135, 255);
			width = Translation.getMenuWidth();
			height = 30;

			//height = LoadingExtension.IsPathManagerCompatible ? 430 : 230;
			relativePosition = new Vector3(1450f, 65f);

			title = AddUIComponent<UILabel>();
			title.text = "Version " + TrafficManagerMod.Version;
			title.relativePosition = new Vector3(50.0f, 5.0f);

			int y = 30;

			_buttonSwitchTraffic = _createButton(Translation.GetString("Switch_traffic_lights"), y, clickSwitchTraffic);
			y += 40;
			height += 40;

			if (Options.prioritySignsEnabled) {
				_buttonPrioritySigns = _createButton(Translation.GetString("Add_priority_signs"), y, clickAddPrioritySigns);
				y += 40;
				height += 40;
			}

			_buttonManualControl = _createButton(Translation.GetString("Manual_traffic_lights"), y, clickManualControl);
			y += 40;
			height += 40;

			if (Options.timedLightsEnabled) {
				_buttonTimedMain = _createButton(Translation.GetString("Timed_traffic_lights"), y, clickTimedAdd);
				y += 40;
				height += 40;
			}

			
			_buttonLaneChange = _createButton(Translation.GetString("Change_lane_arrows"), y, clickChangeLanes);
			y += 40;
			height += 40;

			if (Options.laneConnectorEnabled) {
				_buttonLaneConnector = _createButton(Translation.GetString("Lane_connector"), y, clickLaneConnector);
				y += 40;
				height += 40;
			}

			if (Options.customSpeedLimitsEnabled) {
				_buttonSpeedLimits = _createButton(Translation.GetString("Speed_limits"), y, clickSpeedLimits);
				y += 40;
				height += 40;
			}

			if (Options.vehicleRestrictionsEnabled) {
				_buttonVehicleRestrictions = _createButton(Translation.GetString("Vehicle_restrictions"), y, clickVehicleRestrictions);
				y += 40;
				height += 40;
			}

			if (Options.junctionRestrictionsEnabled) {
				_buttonJunctionRestrictions = _createButton(Translation.GetString("Junction_restrictions"), y, clickJunctionRestrictions);
				y += 40;
				height += 40;
			}

			_buttonClearTraffic = _createButton(Translation.GetString("Clear_Traffic"), y, clickClearTraffic);
			y += 40;
			height += 40;

			
			_buttonToggleDespawn = _createButton(Options.enableDespawning ? Translation.GetString("Disable_despawning") : Translation.GetString("Enable_despawning"), y, ClickToggleDespawn);
			y += 40;
			height += 40;

#if DEBUG
			_goToField = CreateTextField("", y);
			y += 40;
			height += 40;
			_goToPosButton = _createButton("Goto position", y, clickGoToPos);
			y += 40;
			height += 40;
			_goToPosButton = _createButton("Clear position", y, clickClearPos);
			y += 40;
			height += 40;
			_goToSegmentButton = _createButton("Goto segment", y, clickGoToSegment);
			y += 40;
			height += 40;
			_goToNodeButton = _createButton("Goto node", y, clickGoToNode);
			y += 40;
			height += 40;
			_goToVehicleButton = _createButton("Goto vehicle", y, clickGoToVehicle);
			y += 40;
			height += 40;
			_goToBuildingButton = _createButton("Goto building", y, clickGoToBuilding);
			y += 40;
			height += 40;
			_goToCitizenInstanceButton = _createButton("Goto citizen inst.", y, clickGoToCitizenInstance);
			y += 40;
			height += 40;
			_printDebugInfoButton = _createButton("Print debug info", y, clickPrintDebugInfo);
			y += 40;
			height += 40;
			/*_noneToVehicleButton = _createButton("None -> Vehicle", y, clickNoneToVehicle);
			y += 40;
			height += 40;
			_vehicleToNoneButton = _createButton("Vehicle -> None", y, clickVehicleToNone);
			y += 40;
			height += 40;*/
#endif
#if QUEUEDSTATS
			_togglePathFindStatsButton = _createButton("Toggle PathFind stats", y, clickTogglePathFindStats);
			y += 40;
			height += 40;
#endif
#if DEBUG
			_removeStuckEntitiesButton = _createButton("Remove stuck entities", y, clickRemoveStuckEntities);
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
			textfield.width = Translation.getMenuWidth() - 30;
			return textfield;
		}

		private UIButton _createButton(string text, int y, MouseEventHandler eventClick) {
			var button = AddUIComponent<UIButton>();
			button.textScale = 0.8f;
			button.width = Translation.getMenuWidth()-30;
			button.height = 30;
			button.normalBgSprite = "ButtonMenu";
			button.disabledBgSprite = "ButtonMenuDisabled";
			button.hoveredBgSprite = "ButtonMenuHovered";
			button.focusedBgSprite = "ButtonMenu";
			button.pressedBgSprite = "ButtonMenuPressed";
			button.textColor = new Color32(255, 255, 255, 255);
			button.playAudioEvents = true;
			button.text = text;
			button.relativePosition = new Vector3(15f, y);
			button.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
				deactivateButtons();
				eventClick(component, eventParam);
				button.Invalidate();
			};

			return button;
		}

#if DEBUG
		private void clickGoToPos(UIComponent component, UIMouseEventParameter eventParam) {
			string[] vectorElms = _goToField.text.Split(',');
			if (vectorElms.Length < 2)
				return;

			ushort segmentId = Convert.ToUInt16(_goToField.text);
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None) {
				CameraCtrl.GoToPos(new Vector3(float.Parse(vectorElms[0]), Camera.main.transform.position.y, float.Parse(vectorElms[1])));
			}
		}

		private void clickClearPos(UIComponent component, UIMouseEventParameter eventParam) {
			CameraCtrl.ClearPos();
		}

		private void clickGoToSegment(UIComponent component, UIMouseEventParameter eventParam) {
			ushort segmentId = Convert.ToUInt16(_goToField.text);
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None) {
				CameraCtrl.GoToSegment(segmentId, new Vector3(Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_bounds.center.x, Camera.main.transform.position.y, Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_bounds.center.z));
			}
		}

		private void clickGoToNode(UIComponent component, UIMouseEventParameter eventParam) {
			ushort nodeId = Convert.ToUInt16(_goToField.text);
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) != NetNode.Flags.None) {
				CameraCtrl.GoToNode(nodeId, new Vector3(Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position.x, Camera.main.transform.position.y, Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position.z));
			}
		}

		private void clickPrintDebugInfo(UIComponent component, UIMouseEventParameter eventParam) {
			UtilityManager.Instance.RequestPrintDebugInfo();
		}

		private static Dictionary<string, List<byte>> customEmergencyLanes = new Dictionary<string, List<byte>>();

		private void clickNoneToVehicle(UIComponent component, UIMouseEventParameter eventParam) {
			Dictionary<NetInfo, ushort> ret = new Dictionary<NetInfo, ushort>();
			int numLoaded = PrefabCollection<NetInfo>.LoadedCount();
			for (uint i = 0; i < numLoaded; ++i) {
				NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);
				if (!(info.m_netAI is RoadBaseAI))
					continue;
				RoadBaseAI ai = (RoadBaseAI)info.m_netAI;
				if (!ai.m_highwayRules)
					continue;
				NetInfo.Lane[] laneInfos = info.m_lanes;

				for (byte k = 0; k < Math.Min(2, laneInfos.Length); ++k) {
					NetInfo.Lane laneInfo = laneInfos[k];
					if (laneInfo.m_vehicleType == VehicleInfo.VehicleType.None) {
						laneInfo.m_vehicleType = VehicleInfo.VehicleType.Car;
						laneInfo.m_laneType = NetInfo.LaneType.Vehicle;
						Log._Debug($"Changing vehicle type of lane {k} @ {info.name} from None to Car, lane type from None to Vehicle");

						if (!customEmergencyLanes.ContainsKey(info.name))
							customEmergencyLanes.Add(info.name, new List<byte>());
						customEmergencyLanes[info.name].Add(k);
					}
				}
			}
		}
#endif

#if QUEUEDSTATS
		private void clickTogglePathFindStats(UIComponent component, UIMouseEventParameter eventParam) {
			showPathFindStats = !showPathFindStats;
		}
#endif

#if DEBUG

		private void clickRemoveStuckEntities(UIComponent component, UIMouseEventParameter eventParam) {
			UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);

			UtilityManager.Instance.RequestResetStuckEntities();
		}

		private void clickVehicleToNone(UIComponent component, UIMouseEventParameter eventParam) {
			foreach (KeyValuePair<string, List<byte>> e in customEmergencyLanes) {
				NetInfo info = PrefabCollection<NetInfo>.FindLoaded(e.Key);
				if (info == null) {
					Log.Warning($"Could not find NetInfo by name {e.Key}");
					continue;
				}

				foreach (byte index in e.Value) {
					if (index < 0 || index >= info.m_lanes.Length) {
						Log.Warning($"Illegal lane index {index} for NetInfo {e.Key}");
						continue;
					}

					Log._Debug($"Resetting vehicle type of lane {index} @ {info.name}");

					info.m_lanes[index].m_vehicleType = VehicleInfo.VehicleType.None;
					info.m_lanes[index].m_laneType = NetInfo.LaneType.None;
				}
			}
			customEmergencyLanes.Clear();
		}

		private void clickGoToVehicle(UIComponent component, UIMouseEventParameter eventParam) {
			ushort vehicleId = Convert.ToUInt16(_goToField.text);
			Vehicle vehicle = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId];
			if ((vehicle.m_flags & Vehicle.Flags.Created) != 0) {
				CameraCtrl.GoToVehicle(vehicleId, new Vector3(vehicle.GetLastFramePosition().x, Camera.main.transform.position.y, vehicle.GetLastFramePosition().z));
			}
		}

		private void clickGoToBuilding(UIComponent component, UIMouseEventParameter eventParam) {
			ushort buildingId = Convert.ToUInt16(_goToField.text);
			Building building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingId];
			if ((building.m_flags & Building.Flags.Created) != 0) {
				CameraCtrl.GoToBuilding(buildingId, new Vector3(building.m_position.x, Camera.main.transform.position.y, building.m_position.z));

				for (int index = 0; index < BuildingManager.BUILDINGGRID_RESOLUTION * BuildingManager.BUILDINGGRID_RESOLUTION; ++index) {
					ushort bid = Singleton<BuildingManager>.instance.m_buildingGrid[index];
					while (bid != 0) {
						if (bid == buildingId) {
							int i = index / BuildingManager.BUILDINGGRID_RESOLUTION;
							int j = index % BuildingManager.BUILDINGGRID_RESOLUTION;
							Log._Debug($"Found building {buildingId} in building grid @ {index}. i={i}, j={j}");
						}
						bid = Singleton<BuildingManager>.instance.m_buildings.m_buffer[bid].m_nextGridBuilding;
					}
				}
			}
		}

		private void clickGoToCitizenInstance(UIComponent component, UIMouseEventParameter eventParam) {
			ushort citizenInstanceId = Convert.ToUInt16(_goToField.text);
			CitizenInstance citizenInstance = Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId];
			if ((citizenInstance.m_flags & CitizenInstance.Flags.Created) != 0) {
				CameraCtrl.GoToCitizenInstance(citizenInstanceId, new Vector3(citizenInstance.GetLastFramePosition().x, Camera.main.transform.position.y, citizenInstance.GetLastFramePosition().z));
			}
		}
#endif

		private void clickSwitchTraffic(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.SwitchTrafficLight) {
				_buttonSwitchTraffic.normalBgSprite = _buttonSwitchTraffic.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.SwitchTrafficLight);
			} else {
				_buttonSwitchTraffic.normalBgSprite = _buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		private void clickAddPrioritySigns(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.AddPrioritySigns) {
				_buttonPrioritySigns.normalBgSprite = _buttonPrioritySigns.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.AddPrioritySigns);
			} else {
				_buttonPrioritySigns.normalBgSprite = _buttonPrioritySigns.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		private void clickManualControl(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.ManualSwitch) {
				_buttonManualControl.normalBgSprite = _buttonManualControl.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.ManualSwitch);
			} else {
				_buttonManualControl.normalBgSprite = _buttonManualControl.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		private void clickTimedAdd(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.TimedLightsSelectNode && UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.TimedLightsShowLights) {
				_buttonTimedMain.normalBgSprite = _buttonTimedMain.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.TimedLightsSelectNode);
			} else {
				_buttonTimedMain.normalBgSprite = _buttonTimedMain.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		private void clickSpeedLimits(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.SpeedLimits) {
				_buttonSpeedLimits.normalBgSprite = _buttonSpeedLimits.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.SpeedLimits);
			} else {
				_buttonSpeedLimits.normalBgSprite = _buttonSpeedLimits.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		private void clickVehicleRestrictions(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.VehicleRestrictions) {
				_buttonVehicleRestrictions.normalBgSprite = _buttonVehicleRestrictions.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.VehicleRestrictions);
			} else {
				_buttonVehicleRestrictions.normalBgSprite = _buttonVehicleRestrictions.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		private void clickJunctionRestrictions(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.JunctionRestrictions) {
				_buttonJunctionRestrictions.normalBgSprite = _buttonJunctionRestrictions.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.JunctionRestrictions);
			} else {
				_buttonJunctionRestrictions.normalBgSprite = _buttonJunctionRestrictions.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		private void clickChangeLanes(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.LaneChange) {
				_buttonLaneChange.normalBgSprite = _buttonLaneChange.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.LaneChange);
			} else {
				_buttonLaneChange.normalBgSprite = _buttonLaneChange.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		private void clickLaneConnector(UIComponent component, UIMouseEventParameter eventParam) {
			if (UIBase.GetTrafficManagerTool(true).GetToolMode() != ToolMode.LaneConnector) {
				_buttonLaneConnector.normalBgSprite = _buttonLaneConnector.focusedBgSprite = "ButtonMenuFocused";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.LaneConnector);
			} else {
				_buttonLaneConnector.normalBgSprite = _buttonLaneConnector.focusedBgSprite = "ButtonMenu";
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			}
		}

		/// <summary>
		/// Removes the focused sprite from all menu buttons
		/// </summary>
		public static void deactivateButtons() {
			if (_buttonSwitchTraffic != null)
				_buttonSwitchTraffic.normalBgSprite = _buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";
			if (_buttonPrioritySigns != null)
				_buttonPrioritySigns.normalBgSprite = _buttonPrioritySigns.focusedBgSprite = "ButtonMenu";
			if (_buttonManualControl != null)
				_buttonManualControl.normalBgSprite = _buttonManualControl.focusedBgSprite = "ButtonMenu";
			if (_buttonTimedMain != null)
				_buttonTimedMain.normalBgSprite = _buttonTimedMain.focusedBgSprite = "ButtonMenu";
			if (_buttonLaneChange != null)
				_buttonLaneChange.normalBgSprite = _buttonLaneChange.focusedBgSprite = "ButtonMenu";
			if (_buttonLaneConnector != null)
				_buttonLaneConnector.normalBgSprite = _buttonLaneConnector.focusedBgSprite = "ButtonMenu";
			if (_buttonSpeedLimits != null)
				_buttonSpeedLimits.normalBgSprite = _buttonSpeedLimits.focusedBgSprite = "ButtonMenu";
			if (_buttonVehicleRestrictions != null)
				_buttonVehicleRestrictions.normalBgSprite = _buttonVehicleRestrictions.focusedBgSprite = "ButtonMenu";
			if (_buttonJunctionRestrictions != null)
				_buttonJunctionRestrictions.normalBgSprite = _buttonJunctionRestrictions.focusedBgSprite = "ButtonMenu";
			if (_buttonClearTraffic != null)
				_buttonClearTraffic.normalBgSprite = _buttonClearTraffic.focusedBgSprite = "ButtonMenu";
			if (_buttonToggleDespawn != null)
				_buttonToggleDespawn.normalBgSprite = _buttonToggleDespawn.focusedBgSprite = "ButtonMenu";
		}

		private void clickClearTraffic(UIComponent component, UIMouseEventParameter eventParam) {
			UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);

			VehicleStateManager.Instance.RequestClearTraffic();
		}

		private static void ClickToggleDespawn(UIComponent component, UIMouseEventParameter eventParam) {
			UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);

			Options.setEnableDespawning(!Options.enableDespawning);

			_buttonToggleDespawn.text = Options.enableDespawning
				? Translation.GetString("Disable_despawning")
				: Translation.GetString("Enable_despawning");
		}

		public override void Update() {
#if QUEUEDSTATS
			if (showPathFindStats && title != null) {
				title.text = CustomPathManager.TotalQueuedPathFinds.ToString();
#if EXTRAPF
				title.text += "+" + CustomPathManager.ExtraQueuedPathFinds.ToString();
#endif
			}
#endif
		}
	}
#endif
}
