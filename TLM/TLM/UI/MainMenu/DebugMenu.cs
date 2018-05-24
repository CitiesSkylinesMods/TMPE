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
using TrafficManager.Manager.Impl;
using TrafficManager.Util;
using CSUtil.Commons.Benchmark;

namespace TrafficManager.UI {
#if DEBUG
	public class DebugMenuPanel : UIPanel {
		//private static UIState _uiState = UIState.None;

#if QUEUEDSTATS
		private static bool showPathFindStats = false;
#endif

		/*private static UIButton _buttonSwitchTraffic;
		private static UIButton _buttonPrioritySigns;
		private static UIButton _buttonManualControl;
		private static UIButton _buttonTimedMain;
		private static UIButton _buttonLaneChange;
		private static UIButton _buttonLaneConnector;
		private static UIButton _buttonVehicleRestrictions;
		private static UIButton _buttonJunctionRestrictions;
		private static UIButton _buttonSpeedLimits;
		private static UIButton _buttonClearTraffic;
		private static UIButton _buttonToggleDespawn;*/
#if DEBUG
		private static UITextField _goToField = null;
		private static UIButton _goToSegmentButton = null;
		private static UIButton _goToNodeButton = null;
		private static UIButton _goToVehicleButton = null;
		private static UIButton _goToParkedVehicleButton = null;
		private static UIButton _goToBuildingButton = null;
		private static UIButton _goToCitizenInstanceButton = null;
		private static UIButton _goToPosButton = null;
		private static UIButton _printDebugInfoButton = null;
		private static UIButton _reloadConfigButton = null;
		private static UIButton _recalcLinesButton = null;
		private static UIButton _checkDetoursButton = null;
		private static UIButton _noneToVehicleButton = null;
		private static UIButton _vehicleToNoneButton = null;
		private static UIButton _printFlagsDebugInfoButton = null;
		private static UIButton _printBenchmarkReportButton = null;
		private static UIButton _resetBenchmarksButton = null;
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
			Vector2 resolution = UIView.GetAView().GetScreenResolution();
			relativePosition = new Vector3(resolution.x - Translation.getMenuWidth() - 30f, 65f);

			title = AddUIComponent<UILabel>();
			title.text = "Version " + TrafficManagerMod.Version;
			title.relativePosition = new Vector3(50.0f, 5.0f);

			int y = 30;

			/*_buttonSwitchTraffic = _createButton(Translation.GetString("Switch_traffic_lights"), y, clickSwitchTraffic);
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
			height += 40;*/

#if DEBUG
			_goToField = CreateTextField("", y);
			y += 40;
			height += 40;
			_goToPosButton = _createButton("Goto position", y, clickGoToPos);
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
			_goToParkedVehicleButton = _createButton("Goto parked vehicle", y, clickGoToParkedVehicle);
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
			_reloadConfigButton = _createButton("Reload configuration", y, clickReloadConfig);
			y += 40;
			height += 40;
			_recalcLinesButton = _createButton("Recalculate transport lines", y, clickRecalcLines);
			y += 40;
			height += 40;
			_checkDetoursButton = _createButton("Remove all parked vehicles", y, clickCheckDetours);
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
			_printFlagsDebugInfoButton = _createButton("Print flags debug info", y, clickPrintFlagsDebugInfo);
			y += 40;
			height += 40;
			_printBenchmarkReportButton = _createButton("Print benchmark report", y, clickPrintBenchmarkReport);
			y += 40;
			height += 40;
			_resetBenchmarksButton = _createButton("Reset benchmarks", y, clickResetBenchmarks);
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

			CSUtil.CameraControl.CameraController.Instance.GoToPos(new Vector3(float.Parse(vectorElms[0]), Camera.main.transform.position.y, float.Parse(vectorElms[1])));
		}

		private void clickGoToSegment(UIComponent component, UIMouseEventParameter eventParam) {
			ushort segmentId = Convert.ToUInt16(_goToField.text);
			if ((Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None) {
				CSUtil.CameraControl.CameraController.Instance.GoToSegment(segmentId);
			}
		}

		private void clickGoToNode(UIComponent component, UIMouseEventParameter eventParam) {
			ushort nodeId = Convert.ToUInt16(_goToField.text);
			if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) != NetNode.Flags.None) {
				CSUtil.CameraControl.CameraController.Instance.GoToNode(nodeId);
			}
		}

		private void clickPrintDebugInfo(UIComponent component, UIMouseEventParameter eventParam) {
			Constants.ServiceFactory.SimulationService.AddAction(() => {
				UtilityManager.Instance.PrintDebugInfo();
			});
		}

		private void clickReloadConfig(UIComponent component, UIMouseEventParameter eventParam) {
			GlobalConfig.Reload();
		}

		private void clickRecalcLines(UIComponent component, UIMouseEventParameter eventParam) {
			SimulationManager.instance.AddAction(() => {
				for (int i = 0; i < TransportManager.MAX_LINE_COUNT; ++i) {
					if (TransportManager.instance.m_lines.m_buffer[i].m_flags == TransportLine.Flags.None) {
						continue;
						//Log.Message("\tTransport line is not created.");
					}
					Log.Info($"Recalculating transport line {i} now.");
					if (TransportManager.instance.m_lines.m_buffer[i].UpdatePaths((ushort)i) &&
						TransportManager.instance.m_lines.m_buffer[i].UpdateMeshData((ushort)i)
					) {
						Log.Info($"Transport line {i} recalculated.");
					}
				}
			});
		}

		private void clickCheckDetours(UIComponent component, UIMouseEventParameter eventParam) {
			SimulationManager.instance.AddAction(() => {
				SimulationManager.instance.ForcedSimulationPaused = true;
				for (uint i = 0; i < VehicleManager.instance.m_parkedVehicles.m_buffer.Length; ++i) {
					VehicleManager.instance.ReleaseParkedVehicle((ushort)i);
				}
				SimulationManager.instance.ForcedSimulationPaused = false;
			});

			/*Log.Info($"Screen.width: {Screen.width} Screen.height: {Screen.height}");
			Log.Info($"Screen.currentResolution.width: {Screen.currentResolution.width} Screen.currentResolution.height: {Screen.currentResolution.height}");
			Vector2 resolution = UIView.GetAView().GetScreenResolution();
			Log.Info($"UIView.screenResolution.width: {resolution.x} UIView.screenResolution.height: {resolution.y}");
			*/

			/*SimulationManager.instance.AddAction(() => {
				PrintTransportStats();
			});*/
		}

		public static void PrintTransportStats() {
			for (int i = 0; i < TransportManager.MAX_LINE_COUNT; ++i) {
				Log.Info("Transport line " + i + ":");
				if ((TransportManager.instance.m_lines.m_buffer[i].m_flags & TransportLine.Flags.Created) == TransportLine.Flags.None) {
					Log.Info("\tTransport line is not created.");
					continue;
				}
				Log.Info("\tFlags: " + TransportManager.instance.m_lines.m_buffer[i].m_flags + ", cat: " + TransportManager.instance.m_lines.m_buffer[i].Info.category + ", type: " + TransportManager.instance.m_lines.m_buffer[i].Info.m_transportType + ", name: " + TransportManager.instance.GetLineName((ushort)i));
				ushort firstStopNodeId = TransportManager.instance.m_lines.m_buffer[i].m_stops;
				ushort stopNodeId = firstStopNodeId;
				Vector3 lastNodePos = Vector3.zero;
				int index = 1;
				while (stopNodeId != 0) {
					Vector3 pos = NetManager.instance.m_nodes.m_buffer[stopNodeId].m_position;
					Log.Info("\tStop node #" + index + " -- " + stopNodeId + ": Flags: " + NetManager.instance.m_nodes.m_buffer[stopNodeId].m_flags + ", Transport line: " + NetManager.instance.m_nodes.m_buffer[stopNodeId].m_transportLine + ", Problems: " + NetManager.instance.m_nodes.m_buffer[stopNodeId].m_problems + " Pos: " + pos + ", Dist. to lat pos: " + (lastNodePos - pos).magnitude);
					if (NetManager.instance.m_nodes.m_buffer[stopNodeId].m_problems != Notification.Problem.None) {
						Log.Warning("\t*** PROBLEMS DETECTED ***");
					}
					lastNodePos = pos;

					ushort nextSegment = TransportLine.GetNextSegment(stopNodeId);
					if (nextSegment != 0) {
						stopNodeId = NetManager.instance.m_segments.m_buffer[(int)nextSegment].m_endNode;
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
			Update();
			showPathFindStats = !showPathFindStats;
		}
#endif

#if DEBUG

		private void clickPrintFlagsDebugInfo(UIComponent component, UIMouseEventParameter eventParam) {
			Flags.PrintDebugInfo();
		}

		private void clickPrintBenchmarkReport(UIComponent component, UIMouseEventParameter eventParam) {
			Constants.ServiceFactory.SimulationService.AddAction(() => {
				Log.Info(BenchmarkProfileProvider.Instance.CreateReport());
			});
		}

		private void clickResetBenchmarks(UIComponent component, UIMouseEventParameter eventParam) {
			Constants.ServiceFactory.SimulationService.AddAction(() => {
				BenchmarkProfileProvider.Instance.ClearProfiles();
			});
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
				CSUtil.CameraControl.CameraController.Instance.GoToVehicle(vehicleId);
			}
		}

		private void clickGoToParkedVehicle(UIComponent component, UIMouseEventParameter eventParam) {
			ushort parkedVehicleId = Convert.ToUInt16(_goToField.text);
			VehicleParked parkedVehicle = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId];
			if ((parkedVehicle.m_flags & (ushort)VehicleParked.Flags.Created) != 0) {
				CSUtil.CameraControl.CameraController.Instance.GoToParkedVehicle(parkedVehicleId);
			}
		}

		private void clickGoToBuilding(UIComponent component, UIMouseEventParameter eventParam) {
			ushort buildingId = Convert.ToUInt16(_goToField.text);
			Building building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingId];
			if ((building.m_flags & Building.Flags.Created) != 0) {
				CSUtil.CameraControl.CameraController.Instance.GoToBuilding(buildingId);

				/*for (int index = 0; index < BuildingManager.BUILDINGGRID_RESOLUTION * BuildingManager.BUILDINGGRID_RESOLUTION; ++index) {
					ushort bid = Singleton<BuildingManager>.instance.m_buildingGrid[index];
					while (bid != 0) {
						if (bid == buildingId) {
							int i = index / BuildingManager.BUILDINGGRID_RESOLUTION;
							int j = index % BuildingManager.BUILDINGGRID_RESOLUTION;
							Log._Debug($"Found building {buildingId} in building grid @ {index}. i={i}, j={j}");
						}
						bid = Singleton<BuildingManager>.instance.m_buildings.m_buffer[bid].m_nextGridBuilding;
					}
				}*/
			}
		}

		private void clickGoToCitizenInstance(UIComponent component, UIMouseEventParameter eventParam) {
			ushort citizenInstanceId = Convert.ToUInt16(_goToField.text);
			CitizenInstance citizenInstance = Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId];
			if ((citizenInstance.m_flags & CitizenInstance.Flags.Created) != 0) {
				CSUtil.CameraControl.CameraController.Instance.GoToCitizenInstance(citizenInstanceId);
			}
		}
#endif

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
