using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.Manager;
using TrafficManager.Traffic;

namespace TrafficManager.UI.SubTools {
	public class TimedTrafficLightsTool : SubTool {
		private readonly GUIStyle _counterStyle = new GUIStyle();
		private readonly int[] _hoveredButton = new int[2];
		private bool nodeSelectionLocked = false;
		private List<ushort> SelectedNodeIndexes = new List<ushort>();
		private bool _cursorInSecondaryPanel;
		private Rect _windowRect = TrafficManagerTool.MoveGUI(new Rect(0, 0, 480, 350));
		private Rect _windowRect2 = TrafficManagerTool.MoveGUI(new Rect(0, 0, 300, 150));
		private bool _timedPanelAdd = false;
		private int _timedEditStep = -1;
		private ushort _hoveredNode = 0;
		private bool _timedShowNumbers = false;
		private int _timedViewedStep = -1;
		private int _stepMinValue = 1;
		private int _stepMaxValue = 1;
		private float _waitFlowBalance = 0.8f;
		private string _stepMinValueStr = "1";
		private string _stepMaxValueStr = "1";
		private bool timedLightActive = false;
		private int currentStep = -1;
		private int numSteps = 0;
		private bool inTestMode = false;
		private HashSet<ushort> currentTimedNodeIds;

		private GUIStyle layout = new GUIStyle { normal = { textColor = new Color(1f, 1f, 1f) } };
		private GUIStyle layoutRed = new GUIStyle { normal = { textColor = new Color(1f, 0f, 0f) } };
		private GUIStyle layoutGreen = new GUIStyle { normal = { textColor = new Color(0f, 1f, 0f) } };
		private GUIStyle layoutYellow = new GUIStyle { normal = { textColor = new Color(1f, 1f, 0f) } };

		public TimedTrafficLightsTool(TrafficManagerTool mainTool) : base(mainTool) {
			currentTimedNodeIds = new HashSet<ushort>();
		}

		public override bool IsCursorInPanel() {
			return base.IsCursorInPanel() || _cursorInSecondaryPanel;
		}

		private void RefreshCurrentTimedNodeIds() {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			currentTimedNodeIds.Clear();
			for (uint nodeId = 1; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
					continue;

				TrafficLightSimulation lightSim = tlsMan.GetNodeSimulation((ushort)nodeId);
				if (lightSim != null && lightSim.IsTimedLight()) {
					currentTimedNodeIds.Add((ushort)nodeId);
				}
			}
		}

		public override void OnActivate() {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			RefreshCurrentTimedNodeIds();

			nodeSelectionLocked = false;
			foreach (ushort nodeId in currentTimedNodeIds) {
				TrafficLightSimulation lightSim = tlsMan.GetNodeSimulation(nodeId);
				if (lightSim != null) {
					lightSim.housekeeping();
				}
			}
		}

		public override void OnSecondaryClickOverlay() {
			if (!IsCursorInPanel()) {
				Cleanup();
				TrafficManagerTool.SetToolMode(ToolMode.TimedLightsSelectNode);
			}
		}

		public override void OnPrimaryClickOverlay() {
			if (HoveredNodeId <= 0 || nodeSelectionLocked)
				return;

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			switch (TrafficManagerTool.GetToolMode()) {
				case ToolMode.TimedLightsSelectNode:
				case ToolMode.TimedLightsShowLights:
					if (TrafficManagerTool.GetToolMode() == ToolMode.TimedLightsShowLights) {
						TrafficManagerTool.SetToolMode(ToolMode.TimedLightsSelectNode);
						ClearSelectedNodes();
					}

					TrafficLightSimulation timedSim = tlsMan.GetNodeSimulation(HoveredNodeId);
					if (timedSim == null || !timedSim.IsTimedLight()) {
						if (IsNodeSelected(HoveredNodeId)) {
							RemoveSelectedNode(HoveredNodeId);
						} else {
							AddSelectedNode(HoveredNodeId);
						}
					} else {
						if (SelectedNodeIndexes.Count == 0) {
							//timedSim.housekeeping();
							var timedLight = timedSim.TimedLight;

							if (timedLight != null) {
								SelectedNodeIndexes = new List<ushort>(timedLight.NodeGroup);
								TrafficManagerTool.SetToolMode(ToolMode.TimedLightsShowLights);
							}
						} else {
							MainTool.ShowTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"), Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position);
						}
					}
					break;
				case ToolMode.TimedLightsAddNode:
					if (SelectedNodeIndexes.Count <= 0) {
						TrafficManagerTool.SetToolMode(ToolMode.TimedLightsSelectNode);
						return;
					}

					if (SelectedNodeIndexes.Contains(HoveredNodeId))
						return;

					//bool mayEnterBlocked = Options.mayEnterBlockedJunctions;
					TimedTrafficLights existingTimedLight = null;
					foreach (var nodeId in SelectedNodeIndexes) {
						var nodeSimulation = tlsMan.GetNodeSimulation(nodeId);
						if (nodeSimulation == null || !nodeSimulation.IsTimedLight())
							continue;
						TimedTrafficLights timedNode = nodeSimulation.TimedLight;
						if (timedNode == null)
							continue;

						//mayEnterBlocked = timedNode.vehiclesMayEnterBlockedJunctions;
						existingTimedLight = timedNode;
					}

					var timedSim2 = tlsMan.GetNodeSimulation(HoveredNodeId);
					/*if (timedSim2 != null)
						timedSim2.housekeeping();*/
					TimedTrafficLights timedLight2 = null;
					if (timedSim2 == null || !timedSim2.IsTimedLight()) {
						var nodeGroup = new List<ushort>();
						nodeGroup.Add(HoveredNodeId);
						timedSim2 = tlsMan.AddNodeToSimulation(HoveredNodeId);
						timedSim2.SetupTimedTrafficLight(nodeGroup);
						//timedLight.vehiclesMayEnterBlockedJunctions = mayEnterBlocked;
					}
					timedLight2 = timedSim2.TimedLight;

					timedLight2.Join(existingTimedLight);
					ClearSelectedNodes();
					foreach (ushort nodeId in timedLight2.NodeGroup)
						AddSelectedNode(nodeId);
					TrafficManagerTool.SetToolMode(ToolMode.TimedLightsShowLights);
					RefreshCurrentTimedNodeIds();
					break;
				case ToolMode.TimedLightsRemoveNode:
					if (SelectedNodeIndexes.Count <= 0) {
						TrafficManagerTool.SetToolMode(ToolMode.TimedLightsSelectNode);
						return;
					}

					if (SelectedNodeIndexes.Contains(HoveredNodeId)) {
						tlsMan.RemoveNodeFromSimulation(HoveredNodeId, false, false);
						RefreshCurrentTimedNodeIds();
					}
					RemoveSelectedNode(HoveredNodeId);
					TrafficManagerTool.SetToolMode(ToolMode.TimedLightsShowLights);
					break;
			}
		}

		public override void OnToolGUI(Event e) {
			base.OnToolGUI(e);

			switch (TrafficManagerTool.GetToolMode()) {
				case ToolMode.TimedLightsSelectNode:
					_guiTimedTrafficLightsNode();
					break;
				case ToolMode.TimedLightsShowLights:
				case ToolMode.TimedLightsAddNode:
				case ToolMode.TimedLightsRemoveNode:
					_guiTimedTrafficLights();
					break;
			}
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			bool onlySelected = TrafficManagerTool.GetToolMode() == ToolMode.TimedLightsRemoveNode;
			//Log._Debug($"nodeSelLocked={nodeSelectionLocked} HoveredNodeId={HoveredNodeId} IsNodeSelected={IsNodeSelected(HoveredNodeId)} onlySelected={onlySelected} isinsideui={MainTool.GetToolController().IsInsideUI} cursorVis={Cursor.visible}");
            if (!nodeSelectionLocked &&
				HoveredNodeId != 0 &&
				(!IsNodeSelected(HoveredNodeId) ^ onlySelected) &&
				!MainTool.GetToolController().IsInsideUI &&
				Cursor.visible &&
				Flags.mayHaveTrafficLight(HoveredNodeId)) {
				//var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_segment0];

				//if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
				MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, false, false);

				/*Bezier3 bezier;
				bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;
				bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;

				var color = MainTool.GetToolColor(false, false);

				NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
					segment.m_endDirection, false, false, out bezier.b, out bezier.c);
				MainTool.DrawOverlayBezier(cameraInfo, bezier, color);*/
				//}
			}

			if (SelectedNodeIndexes.Count <= 0) return;

			foreach (var index in SelectedNodeIndexes) {
				//var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_segment0];

				MainTool.DrawNodeCircle(cameraInfo, index, true, false);

				/*Bezier3 bezier;

				bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_position;
				bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_position;

				var color = MainTool.GetToolColor(true, false);

				NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
					segment.m_endDirection, false, false, out bezier.b, out bezier.c);
				MainTool.DrawOverlayBezier(cameraInfo, bezier, color);*/
			}
		}

		private void _guiTimedControlPanel(int num) {
			//Log._Debug("guiTimedControlPanel");

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			if (TrafficManagerTool.GetToolMode() == ToolMode.TimedLightsAddNode || TrafficManagerTool.GetToolMode() == ToolMode.TimedLightsRemoveNode) {
				GUILayout.Label(Translation.GetString("Select_junction"));
				if (GUILayout.Button(Translation.GetString("Cancel"))) {
					TrafficManagerTool.SetToolMode(ToolMode.TimedLightsShowLights);
				} else {
					DragWindow(ref _windowRect);
					return;
				}
			}

			var nodeSimulation = tlsMan.GetNodeSimulation(SelectedNodeIndexes[0]);
			var timedNodeMain = nodeSimulation?.TimedLight;

			if (nodeSimulation == null || timedNodeMain == null) {
				TrafficManagerTool.SetToolMode(ToolMode.TimedLightsSelectNode);
				//Log._Debug("nodesim or timednodemain is null");
				DragWindow(ref _windowRect);
				return;
			}

			if (Event.current.type == EventType.Layout) {
				timedLightActive = nodeSimulation.IsTimedLightActive();
				currentStep = timedNodeMain.CurrentStep;
				inTestMode = timedNodeMain.IsInTestMode();
				numSteps = timedNodeMain.NumSteps();
			}

			for (var i = 0; i < timedNodeMain.NumSteps(); i++) {
				GUILayout.BeginHorizontal();

				if (_timedEditStep != i) {
					if (timedLightActive) {
						if (i == currentStep) {
							GUILayout.BeginVertical();
							GUILayout.Space(5);
							String labelStr = Translation.GetString("State") + " " + (i + 1) + ": (" + Translation.GetString("min/max") + ")" + timedNodeMain.GetStep(i).MinTimeRemaining() + "/" + timedNodeMain.GetStep(i).MaxTimeRemaining();
							float flow = Single.NaN;
							float wait = Single.NaN;
							if (inTestMode) {
								try {
									timedNodeMain.GetStep(timedNodeMain.CurrentStep).calcWaitFlow(true, timedNodeMain.CurrentStep, out wait, out flow);
								} catch (Exception e) {
									Log.Warning("calcWaitFlow in UI: This is not thread-safe: " + e.ToString());
								}
							} else {
								wait = timedNodeMain.GetStep(i).maxWait;
								flow = timedNodeMain.GetStep(i).minFlow;
							}
							if (!Single.IsNaN(flow) && !Single.IsNaN(wait))
								labelStr += " " + Translation.GetString("avg._flow") + ": " + String.Format("{0:0.##}", flow) + " " + Translation.GetString("avg._wait") + ": " + String.Format("{0:0.##}", wait);
							GUIStyle labelLayout = layout;
							if (inTestMode && !Single.IsNaN(wait) && !Single.IsNaN(flow)) {
								if (wait > 0 && flow < wait)
									labelLayout = layoutRed;
								else
									labelLayout = layoutGreen;
							} else {
								bool inEndTransition = false;
								try {
									inEndTransition = timedNodeMain.GetStep(i).IsInEndTransition();
								} catch (Exception e) {
									Log.Error("Error while determining if timed traffic light is in end transition: " + e.ToString());
								}
								labelLayout = inEndTransition ? layoutYellow : layoutGreen;
							}
							GUILayout.Label(labelStr, labelLayout);
							GUILayout.Space(5);
							GUILayout.EndVertical();
							if (GUILayout.Button(Translation.GetString("Skip"), GUILayout.Width(80))) {
								foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
									sim.TimedLight.SkipStep();
								}
							}
						} else {
							GUILayout.Label(Translation.GetString("State") + " " + (i + 1) + ": " + timedNodeMain.GetStep(i).minTime + " - " + timedNodeMain.GetStep(i).maxTime, layout);
						}
					} else {
						GUIStyle labelLayout = layout;
						if (_timedViewedStep == i) {
							labelLayout = layoutGreen;
						}
						GUILayout.Label(Translation.GetString("State") + " " + (i + 1) + ": " + timedNodeMain.GetStep(i).minTime + " - " + timedNodeMain.GetStep(i).maxTime, labelLayout);

						if (_timedEditStep < 0) {
							GUILayout.BeginHorizontal(GUILayout.Width(100));

							if (i > 0) {
								if (GUILayout.Button(Translation.GetString("up"), GUILayout.Width(48))) {
									foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
										sim.TimedLight.MoveStep(i, i - 1);
										_timedViewedStep = i - 1;
									}
								}
							} else {
								GUILayout.Space(50);
							}
							
							if (i < numSteps - 1) {
								if (GUILayout.Button(Translation.GetString("down"), GUILayout.Width(48))) {
									foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
										sim.TimedLight.MoveStep(i, i + 1);
										_timedViewedStep = i + 1;
									}
								}
							} else {
								GUILayout.Space(50);
							}

							GUILayout.EndHorizontal();

							if (GUILayout.Button(Translation.GetString("View"), GUILayout.Width(70))) {
								_timedPanelAdd = false;
								_timedViewedStep = i;

								foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
									sim.TimedLight.GetStep(i).UpdateLiveLights(true);
								}
							}

							if (GUILayout.Button(Translation.GetString("Edit"), GUILayout.Width(65))) {
								_timedPanelAdd = false;
								_timedEditStep = i;
								_timedViewedStep = -1;
								_stepMinValue = timedNodeMain.GetStep(i).minTime;
								_stepMaxValue = timedNodeMain.GetStep(i).maxTime;
								_waitFlowBalance = timedNodeMain.GetStep(i).waitFlowBalance;
								_stepMinValueStr = _stepMinValue.ToString();
								_stepMaxValueStr = _stepMaxValue.ToString();
								nodeSelectionLocked = true;

								foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
									sim.TimedLight.GetStep(i).UpdateLiveLights(true);
								}
							}

							if (GUILayout.Button(Translation.GetString("Delete"), GUILayout.Width(70))) {
								_timedPanelAdd = false;
								_timedViewedStep = -1;

								foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
									sim.TimedLight.RemoveStep(i);
								}
							}
						}
					}
				} else {
					nodeSelectionLocked = true;
					int oldStepMinValue = _stepMinValue;
					int oldStepMaxValue = _stepMaxValue;

					// Editing step
					GUILayout.Label(Translation.GetString("Min._Time:"), GUILayout.Width(75));
					_stepMinValueStr = GUILayout.TextField(_stepMinValueStr, GUILayout.Height(20));
					if (!Int32.TryParse(_stepMinValueStr, out _stepMinValue))
						_stepMinValue = oldStepMinValue;

					GUILayout.Label(Translation.GetString("Max._Time:"), GUILayout.Width(75));
					_stepMaxValueStr = GUILayout.TextField(_stepMaxValueStr, GUILayout.Height(20));
					if (!Int32.TryParse(_stepMaxValueStr, out _stepMaxValue))
						_stepMaxValue = oldStepMaxValue;

					if (GUILayout.Button(Translation.GetString("Save"), GUILayout.Width(70))) {
						foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {

							if (_stepMinValue < 0)
								_stepMinValue = 0;
							if (_stepMaxValue <= 0)
								_stepMaxValue = 1;
							if (_stepMaxValue < _stepMinValue)
								_stepMaxValue = _stepMinValue;
							if (_waitFlowBalance <= 0)
								_waitFlowBalance = 0.8f;

							sim.TimedLight.GetStep(_timedEditStep).minTime = (int)_stepMinValue;
							sim.TimedLight.GetStep(_timedEditStep).maxTime = (int)_stepMaxValue;
							sim.TimedLight.GetStep(_timedEditStep).waitFlowBalance = _waitFlowBalance;
							sim.TimedLight.GetStep(_timedEditStep).UpdateLights();
						}

						_timedEditStep = -1;
						_timedViewedStep = -1;
						nodeSelectionLocked = false;
					}

					GUILayout.EndHorizontal();
					makeFlowPolicyDisplay(true);
					GUILayout.BeginHorizontal();
				}

				GUILayout.EndHorizontal();
			} // foreach step

			GUILayout.BeginHorizontal();

			if (_timedEditStep < 0 && !timedLightActive) {
				if (_timedPanelAdd) {
					nodeSelectionLocked = true;
					// new step
					int oldStepMinValue = _stepMinValue;
					int oldStepMaxValue = _stepMaxValue;

					GUILayout.Label(Translation.GetString("Min._Time:"), GUILayout.Width(65));
					_stepMinValueStr = GUILayout.TextField(_stepMinValueStr, GUILayout.Height(20));
					if (!Int32.TryParse(_stepMinValueStr, out _stepMinValue))
						_stepMinValue = oldStepMinValue;

					GUILayout.Label(Translation.GetString("Max._Time:"), GUILayout.Width(65));
					_stepMaxValueStr = GUILayout.TextField(_stepMaxValueStr, GUILayout.Height(20));
					if (!Int32.TryParse(_stepMaxValueStr, out _stepMaxValue))
						_stepMaxValue = oldStepMaxValue;

					if (GUILayout.Button(Translation.GetString("Add"), GUILayout.Width(70))) {
						foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
							if (_stepMinValue < 0)
								_stepMinValue = 0;
							if (_stepMaxValue <= 0)
								_stepMaxValue = 1;
							if (_stepMaxValue < _stepMinValue)
								_stepMaxValue = _stepMinValue;
							if (_waitFlowBalance <= 0)
								_waitFlowBalance = 1f;

							sim.TimedLight.AddStep(_stepMinValue, _stepMaxValue, _waitFlowBalance);
						}
						_timedPanelAdd = false;
					}
					if (GUILayout.Button("X", GUILayout.Width(22))) {
						_timedPanelAdd = false;
					}

					GUILayout.EndHorizontal();
					makeFlowPolicyDisplay(true);
					GUILayout.BeginHorizontal();

				} else {
					if (_timedEditStep < 0) {
						if (GUILayout.Button(Translation.GetString("Add_step"))) {
							_timedPanelAdd = true;
							nodeSelectionLocked = true;
							_timedViewedStep = -1;
							_timedEditStep = -1;
						}
					}
				}
			}

			GUILayout.EndHorizontal();

			GUILayout.Space(5);

			if (numSteps > 1 && _timedEditStep < 0) {
				if (timedLightActive) {
					if (GUILayout.Button(_timedShowNumbers ? Translation.GetString("Hide_counters") : Translation.GetString("Show_counters"))) {
						_timedShowNumbers = !_timedShowNumbers;
					}

					if (GUILayout.Button(Translation.GetString("Stop"))) {
						foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
							sim.TimedLight.Stop();
						}
					}

					/*bool isInTestMode = false;
					foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
						if (sim.TimedLight.IsInTestMode()) {
							isInTestMode = true;
							break;
						}
					}*/

					var curStep = timedNodeMain.CurrentStep;
					_waitFlowBalance = timedNodeMain.GetStep(curStep).waitFlowBalance;
					makeFlowPolicyDisplay(inTestMode);
					foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
						sim.TimedLight.GetStep(curStep).waitFlowBalance = _waitFlowBalance;
					}

					//var mayEnterIfBlocked = GUILayout.Toggle(timedNodeMain.vehiclesMayEnterBlockedJunctions, Translation.GetString("Vehicles_may_enter_blocked_junctions"), new GUILayoutOption[] { });
					var testMode = GUILayout.Toggle(inTestMode, Translation.GetString("Enable_test_mode_(stay_in_current_step)"), new GUILayoutOption[] { });
					foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
						sim.TimedLight.SetTestMode(testMode);
						//sim.TimedLight.vehiclesMayEnterBlockedJunctions = mayEnterIfBlocked;
					}
				} else {
					if (_timedEditStep < 0 && !_timedPanelAdd) {
						if (GUILayout.Button(Translation.GetString("Start"))) {
							_timedPanelAdd = false;
							nodeSelectionLocked = false;

							foreach (var sim in SelectedNodeIndexes.Select(tlsMan.GetNodeSimulation)) {
#if DEBUG
//								Log._Debug("Starting traffic light @ " + sim.TimedLight.NodeId);
#endif
								sim.TimedLight.Start();
							}
						}
					}
				}
			}

			if (_timedEditStep >= 0) {
				DragWindow(ref _windowRect);
				return;
			}

			if (!timedLightActive) {

				GUILayout.Space(30);

				if (GUILayout.Button(Translation.GetString("Add_junction_to_timed_light"))) {
					TrafficManagerTool.SetToolMode(ToolMode.TimedLightsAddNode);
				}

				if (SelectedNodeIndexes.Count > 1) {
					if (GUILayout.Button(Translation.GetString("Remove_junction_from_timed_light"))) {
						TrafficManagerTool.SetToolMode(ToolMode.TimedLightsRemoveNode);
					}
				}

				GUILayout.Space(30);

				if (GUILayout.Button(Translation.GetString("Remove_timed_traffic_light"))) {
					DisableTimed();
					ClearSelectedNodes();
					TrafficManagerTool.SetToolMode(ToolMode.None);
				}
			}

			DragWindow(ref _windowRect);
			return;
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
			RefreshCurrentTimedNodeIds();
		}

		public override void Initialize() {
			Cleanup();
		}

		private void makeFlowPolicyDisplay(bool editable) {
			string formatStr;
			if (_waitFlowBalance < 0.01f)
				formatStr = "{0:0.###}";
			else if (_waitFlowBalance < 0.1f)
				formatStr = "{0:0.##}";
			else
				formatStr = "{0:0.#}";

			GUILayout.BeginHorizontal();
			if (editable) {
				GUILayout.Label(Translation.GetString("Sensitivity") + " (" + String.Format(formatStr, _waitFlowBalance) + ", " + getWaitFlowBalanceInfo() + "):");
				if (_waitFlowBalance <= 0.01f) {
					if (_waitFlowBalance >= 0) {
						if (GUILayout.Button("-.001")) {
							_waitFlowBalance -= 0.001f;
						}
					}
					if (_waitFlowBalance < 0.01f) {
						if (GUILayout.Button("+.001")) {
							_waitFlowBalance += 0.001f;
						}
					}
				} else if (_waitFlowBalance <= 0.1f) {
					if (GUILayout.Button("-.01")) {
						_waitFlowBalance -= 0.01f;
					}
					if (_waitFlowBalance < 0.1f) {
						if (GUILayout.Button("+.01")) {
							_waitFlowBalance += 0.01f;
						}
					}
				}
				if (_waitFlowBalance < 0)
					_waitFlowBalance = 0;
				if (_waitFlowBalance > 5)
					_waitFlowBalance = 5;
				GUILayout.EndHorizontal();

				_waitFlowBalance = GUILayout.HorizontalSlider(_waitFlowBalance, 0.001f, 5f);
				GUILayout.BeginHorizontal();
				GUIStyle style = new GUIStyle();
				style.normal.textColor = Color.white;
				style.alignment = TextAnchor.LowerLeft;
				GUILayout.Label(Translation.GetString("Low"), style, new GUILayoutOption[] { GUILayout.Height(10) });
				style.alignment = TextAnchor.LowerRight;
				GUILayout.Label(Translation.GetString("High"), style, new GUILayoutOption[] { GUILayout.Height(10) });
			} else {
				GUILayout.Label(Translation.GetString("Sensitivity") + ": " + String.Format(formatStr, _waitFlowBalance) + " (" + getWaitFlowBalanceInfo() + ")");
			}
			GUILayout.EndHorizontal();

			GUILayout.Space(5);
		}

		private void _guiTimedTrafficLightsNode() {
			_cursorInSecondaryPanel = false;

			_windowRect2 = GUILayout.Window(252, _windowRect2, _guiTimedTrafficLightsNodeWindow, Translation.GetString("Select_nodes_windowTitle"));

			_cursorInSecondaryPanel = _windowRect2.Contains(Event.current.mousePosition);
		}

		private void _guiTimedTrafficLights() {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
			CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			_cursorInSecondaryPanel = false;

			_windowRect = GUILayout.Window(253, _windowRect, _guiTimedControlPanel, Translation.GetString("Timed_traffic_lights_manager"));

			_cursorInSecondaryPanel = _windowRect.Contains(Event.current.mousePosition);

			GUI.matrix = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(1, 1, 1)); // revert scaling
			ShowGUI();
		}

		private void _guiTimedTrafficLightsNodeWindow(int num) {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			if (SelectedNodeIndexes.Count < 1) {
				GUILayout.Label(Translation.GetString("Select_nodes"));
			} else {
				var txt = SelectedNodeIndexes.Aggregate("", (current, t) => current + (Translation.GetString("Node") + " " + t + "\n"));

				GUILayout.Label(txt);

				if (SelectedNodeIndexes.Count > 0 && GUILayout.Button(Translation.GetString("Deselect_all_nodes"))) {
					ClearSelectedNodes();
				}
				if (!GUILayout.Button(Translation.GetString("Setup_timed_traffic_light"))) return;

				_waitFlowBalance = 0.8f;
				foreach (var selectedNodeIndex in SelectedNodeIndexes) {
					tlsMan.AddNodeToSimulation(selectedNodeIndex);
					var nodeSimulation = tlsMan.GetNodeSimulation(selectedNodeIndex);
					nodeSimulation.SetupTimedTrafficLight(SelectedNodeIndexes);
					RefreshCurrentTimedNodeIds();

					/*for (var s = 0; s < 8; s++) {
						var segment = Singleton<NetManager>.instance.m_nodes.m_buffer[selectedNodeIndex].GetSegment(s);
						if (segment <= 0)
							continue;

						if (!TrafficPriority.IsPrioritySegment(selectedNodeIndex, segment)) {
							TrafficPriority.AddPrioritySegment(selectedNodeIndex, segment, SegmentEnd.PriorityType.None);
						} else {
							TrafficPriority.GetPrioritySegment(selectedNodeIndex, segment).Type = SegmentEnd.PriorityType.None;
						}
					}*/
				}

				TrafficManagerTool.SetToolMode(ToolMode.TimedLightsShowLights);
			}

			DragWindow(ref _windowRect2);
		}

		private string getWaitFlowBalanceInfo() {
			if (_waitFlowBalance < 0.1f) {
				return Translation.GetString("Extreme_long_green/red_phases");
			} else if (_waitFlowBalance < 0.5f) {
				return Translation.GetString("Very_long_green/red_phases");
			} else if (_waitFlowBalance < 0.75f) {
				return Translation.GetString("Long_green/red_phases");
			} else if (_waitFlowBalance < 1.25f) {
				return Translation.GetString("Moderate_green/red_phases");
			} else if (_waitFlowBalance < 1.5f) {
				return Translation.GetString("Short_green/red_phases");
			} else if (_waitFlowBalance < 2.5f) {
				return Translation.GetString("Very_short_green/red_phases");
			} else {
				return Translation.GetString("Extreme_short_green/red_phases");
			}
		}
		private void DisableTimed() {
			if (SelectedNodeIndexes.Count <= 0) return;

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			foreach (var selectedNodeIndex in SelectedNodeIndexes) {
				tlsMan.RemoveNodeFromSimulation(selectedNodeIndex, true, false);
			}
			RefreshCurrentTimedNodeIds();
		}

		private void AddSelectedNode(ushort node) {
			SelectedNodeIndexes.Add(node);
		}

		private bool IsNodeSelected(ushort node) {
			return SelectedNodeIndexes.Contains(node);
		}

		private void RemoveSelectedNode(ushort node) {
			SelectedNodeIndexes.Remove(node);
		}

		private void ClearSelectedNodes() {
			SelectedNodeIndexes.Clear();
		}

		private void drawStraightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightStraightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightStraightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightStraightTexture2D);
					break;
			}
		}

		private void drawForwardLeftLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightForwardLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightForwardLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightForwardLeftTexture2D);
					break;
			}
		}

		private void drawForwardRightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightForwardRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightForwardRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightForwardRightTexture2D);
					break;
			}
		}

		private void drawLeftLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightLeftTexture2D);
					break;
			}
		}

		private void drawRightLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightRightTexture2D);
					break;
			}
		}

		private void drawMainLightTexture(RoadBaseAI.TrafficLightState state, Rect rect) {
			switch (state) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.GreenLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
				default:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.RedLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					GUI.DrawTexture(rect, TrafficLightToolTextureResources.YellowRedLightTexture2D);
					break;
			}
		}

		public override void ShowGUIOverlay(bool viewOnly) {
			if (viewOnly && !Options.timedLightsOverlay /*&&
				TrafficManagerTool.GetToolMode() != ToolMode.TimedLightsAddNode &&
				TrafficManagerTool.GetToolMode() != ToolMode.TimedLightsRemoveNode &&
				TrafficManagerTool.GetToolMode() != ToolMode.TimedLightsSelectNode &&
				TrafficManagerTool.GetToolMode() != ToolMode.TimedLightsShowLights*/)
				return;

			/*if (TrafficManagerTool.GetToolMode() == ToolMode.TimedLightsShowLights) {
				ShowGUI();
			}*/

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
#if DEBUG
				bool debug = nodeId == 21361;
#endif
				if (SelectedNodeIndexes.Contains((ushort)nodeId)) {
#if DEBUG
					if (debug)
						Log._Debug($"TimedTrafficLightsTool.ShowGUIOverlay: Node {nodeId} is selected");
#endif
					continue;
				}

				TrafficLightSimulation lightSim = tlsMan.GetNodeSimulation((ushort)nodeId);
				if (lightSim != null && lightSim.IsTimedLight()) {
					TimedTrafficLights timedNode = lightSim.TimedLight;
					if (timedNode == null) {
#if DEBUG
						if (debug)
							Log._Debug($"TimedTrafficLightsTool.ShowGUIOverlay: Node {nodeId} does not have an instance of TimedTrafficLights. Removing node from simulation");
#endif
						tlsMan.RemoveNodeFromSimulation((ushort)nodeId, true, false);
						RefreshCurrentTimedNodeIds();
						break;
					}

					var nodePositionVector3 = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
					var camPos = Singleton<SimulationManager>.instance.m_simulationView.m_position;
					var diff = nodePositionVector3 - camPos;
					if (diff.magnitude > TrafficManagerTool.PriorityCloseLod)
						continue; // do not draw if too distant

					var nodeScreenPosition = Camera.main.WorldToScreenPoint(nodePositionVector3);
					nodeScreenPosition.y = Screen.height - nodeScreenPosition.y;
					if (nodeScreenPosition.z < 0)
						continue;
					var zoom = 1.0f / diff.magnitude * 100f * MainTool.GetBaseZoom();
					var size = 120f * zoom;
					var guiColor = GUI.color;
					guiColor.a = 0.5f;
					GUI.color = guiColor;
					var nodeDrawingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);
					//Log._Debug($"GUI Color: {guiColor} {GUI.color}");
					GUI.DrawTexture(nodeDrawingBox, lightSim.IsTimedLightActive() ? (timedNode.IsInTestMode() ? TrafficLightToolTextureResources.ClockTestTexture2D : TrafficLightToolTextureResources.ClockPlayTexture2D) : TrafficLightToolTextureResources.ClockPauseTexture2D, ScaleMode.ScaleToFit, true);
				}
			}
		}

		private void ShowGUI() {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
			CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

			var hoveredSegment = false;

			foreach (var nodeId in SelectedNodeIndexes) {
				var nodeSimulation = tlsMan.GetNodeSimulation(nodeId);
				//nodeSimulation.housekeeping();
				if (nodeSimulation == null || !nodeSimulation.IsTimedLight())
					continue;
				TimedTrafficLights timedNode = nodeSimulation.TimedLight;

				var nodePos = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;

				var nodeScreenPos = Camera.main.WorldToScreenPoint(nodePos);
				nodeScreenPos.y = Screen.height - nodeScreenPos.y;

				if (nodeScreenPos.z < 0)
					continue;

				var diff = nodePos - Camera.main.transform.position;
				var zoom = 1.0f / diff.magnitude * 100f * MainTool.GetBaseZoom();

				NodeGeometry nodeGeometry = NodeGeometry.Get(nodeId);
				foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
					if (end == null)
						continue;
					ushort srcSegmentId = end.SegmentId; // source segment

					CustomSegmentLights liveSegmentLights = customTrafficLightsManager.GetSegmentLights(srcSegmentId, end.StartNode);
					if (liveSegmentLights == null)
						continue;
					if (!nodeSimulation.IsTimedLightActive()) {
						liveSegmentLights.MakeRedOrGreen();
					}

					var offset = 17f;
					Vector3 segmentLightPos = nodePos;

					if (Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_startNode == nodeId) {
						segmentLightPos.x += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_startDirection.x * offset;
						segmentLightPos.y += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_startDirection.y;
						segmentLightPos.z += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_startDirection.z * offset;
					} else {
						segmentLightPos.x += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_endDirection.x * offset;
						segmentLightPos.y += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_endDirection.y;
						segmentLightPos.z += Singleton<NetManager>.instance.m_segments.m_buffer[srcSegmentId].m_endDirection.z * offset;
					}

					var screenPos = Camera.main.WorldToScreenPoint(segmentLightPos);
					screenPos.y = Screen.height - screenPos.y;

					var timedActive = nodeSimulation.IsTimedLightActive();
					var guiColor = GUI.color;

					var manualPedestrianWidth = 36f * zoom;
					var manualPedestrianHeight = 35f * zoom;

					var pedestrianWidth = 36f * zoom;
					var pedestrianHeight = 61f * zoom;

					// original / 2.5
					var lightWidth = 41f * zoom;
					var lightHeight = 97f * zoom;

					// SWITCH MODE BUTTON
					var modeWidth = 41f * zoom;
					var modeHeight = 38f * zoom;

					if (liveSegmentLights.PedestrianLightState != null) {
						// pedestrian light

						// SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
						if (!timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
							guiColor.a = _hoveredButton[0] == srcSegmentId &&
										 (_hoveredButton[1] == 1 || _hoveredButton[1] == 2) &&
										 _hoveredNode == nodeId
								? 0.92f
								: 0.6f;

							GUI.color = guiColor;

							var myRect2 = new Rect(screenPos.x - manualPedestrianWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom,
								screenPos.y - manualPedestrianHeight / 2 - 9f * zoom, manualPedestrianWidth,
								manualPedestrianHeight);

							GUI.DrawTexture(myRect2, liveSegmentLights.ManualPedestrianMode ? TrafficLightToolTextureResources.PedestrianModeManualTexture2D : TrafficLightToolTextureResources.PedestrianModeAutomaticTexture2D);

							if (myRect2.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
								_hoveredButton[0] = srcSegmentId;
								_hoveredButton[1] = 1;
								_hoveredNode = nodeId;
								hoveredSegment = true;

								if (MainTool.CheckClicked()) {
									liveSegmentLights.ManualPedestrianMode = !liveSegmentLights.ManualPedestrianMode;
								}
							}
						}

						// SWITCH PEDESTRIAN LIGHT
						guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 2 && _hoveredNode == nodeId ? 0.92f : 0.6f;

						GUI.color = guiColor;

						var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

						switch (liveSegmentLights.PedestrianLightState) {
							case RoadBaseAI.TrafficLightState.Green:
								GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianGreenLightTexture2D);
								break;
							case RoadBaseAI.TrafficLightState.Red:
							default:
								GUI.DrawTexture(myRect3, TrafficLightToolTextureResources.PedestrianRedLightTexture2D);
								break;
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
						foreach (KeyValuePair<byte, ExtVehicleType> e in liveSegmentLights.VehicleTypeByLaneIndex) {
							if (e.Value == vehicleType)
								laneIndices.Add(e.Key);
						}
						//Log._Debug($"Traffic light @ seg. {srcSegmentId} node {nodeId}. Lane indices for vehicleType {vehicleType}: {string.Join(",", laneIndices.Select(x => x.ToString()).ToArray())}");

						++lightOffset;
						CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);

						Vector3 offsetScreenPos = screenPos;
						offsetScreenPos.y -= (lightHeight + 10f * zoom) * lightOffset;

						if (!timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
							guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == -1 &&
										 _hoveredNode == nodeId
								? 0.92f
								: 0.6f;
							
							GUI.color = guiColor;

							var myRect1 = new Rect(offsetScreenPos.x - modeWidth / 2,
								offsetScreenPos.y - modeHeight / 2 + modeHeight - 7f * zoom, modeWidth, modeHeight);

							GUI.DrawTexture(myRect1, TrafficLightToolTextureResources.LightModeTexture2D);
							
							if (myRect1.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
								_hoveredButton[0] = srcSegmentId;
								_hoveredButton[1] = -1;
								_hoveredNode = nodeId;
								hoveredSegment = true;

								if (MainTool.CheckClicked()) {
									liveSegmentLight.ChangeMode();
									timedNode.ChangeLightMode(srcSegmentId, vehicleType, liveSegmentLight.CurrentMode);
								}
							}
						}

						if (lightOffset > 0) {
							// Info sign
							var infoWidth = 56.125f * zoom;
							var infoHeight = 51.375f * zoom;

							int numInfos = 0;
							for (int k = 0; k < TrafficManagerTool.InfoSignsToDisplay.Length; ++k) {
								if ((TrafficManagerTool.InfoSignsToDisplay[k] & vehicleType) == ExtVehicleType.None)
									continue;
								var infoRect = new Rect(offsetScreenPos.x + modeWidth / 2f + 7f * zoom * (float)(numInfos + 1) + infoWidth * (float)numInfos, offsetScreenPos.y - infoHeight / 2f, infoWidth, infoHeight);
								guiColor.a = 0.6f;
								GUI.DrawTexture(infoRect, TrafficLightToolTextureResources.VehicleInfoSignTextures[TrafficManagerTool.InfoSignsToDisplay[k]]);
								++numInfos;
							}
						}

#if DEBUG
						if (timedActive /*&& _timedShowNumbers*/) {
							var prioSeg = TrafficPriorityManager.Instance.GetPrioritySegment(nodeId, srcSegmentId);

							var counterSize = 20f * zoom;
							var yOffset = counterSize + 77f * zoom - modeHeight * 2;
							//var carNumRect = new Rect(offsetScreenPos.x, offsetScreenPos.y - yOffset, counterSize, counterSize);
							var segIdRect = new Rect(offsetScreenPos.x, offsetScreenPos.y - yOffset - counterSize - 2f, counterSize, counterSize);

							_counterStyle.fontSize = (int)(15f * zoom);
							_counterStyle.normal.textColor = new Color(1f, 0f, 0f);

							/*String labelStr = "n/a";
							if (prioSeg != null) {
								labelStr = prioSeg.GetRegisteredVehicleCount(laneIndices).ToString() + " " + Translation.GetString("incoming");
							}
							GUI.Label(carNumRect, labelStr, _counterStyle);*/

							_counterStyle.normal.textColor = new Color(1f, 0f, 0f);
							GUI.Label(segIdRect, Translation.GetString("Segment") + " " + srcSegmentId, _counterStyle);
						}
#endif

						if (lightOffset == 0 && liveSegmentLights.PedestrianLightState != null) {
							// PEDESTRIAN COUNTER
							if (timedActive && _timedShowNumbers) {
								var counterSize = 20f * zoom;

								var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, 3);

								float numOffset;

								if (liveSegmentLights.PedestrianLightState == RoadBaseAI.TrafficLightState.Red) { // TODO check this
									numOffset = counterSize + 53f * zoom - modeHeight * 2;
								} else {
									numOffset = counterSize + 29f * zoom - modeHeight * 2;
								}

								var myRectCounterNum =
									new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 1f) + 24f * zoom - pedestrianWidth / 2,
										offsetScreenPos.y - numOffset, counterSize, counterSize);

								_counterStyle.fontSize = (int)(15f * zoom);
								_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

								GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

								if (myRectCounterNum.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
									_hoveredButton[0] = srcSegmentId;
									_hoveredButton[1] = 2;
									_hoveredNode = nodeId;
									hoveredSegment = true;
								}
							}
						}

						SegmentGeometry geometry = SegmentGeometry.Get(srcSegmentId);
						bool startNode = geometry.StartNodeId() == nodeId;
						if (geometry.IsOutgoingOneWay(startNode)) continue;

						var hasOutgoingLeftSegment = geometry.HasOutgoingLeftSegment(startNode);
						var hasOutgoingForwardSegment = geometry.HasOutgoingStraightSegment(startNode);
						var hasOutgoingRightSegment = geometry.HasOutgoingRightSegment(startNode);

						/*var hasLeftSegment = geometry.HasLeftSegment(startNode);
						var hasForwardSegment = geometry.HasStraightSegment(startNode);
						var hasRightSegment = geometry.HasRightSegment(startNode);*/

						bool hasOtherLight = false;
						switch (liveSegmentLight.CurrentMode) {
							case CustomSegmentLight.Mode.Simple: {
									// no arrow light
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawMainLightTexture(liveSegmentLight.LightMain, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeMainLight();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, 0);

										float numOffset;

										if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}

									GUI.color = guiColor;
								}
								break;
							case CustomSegmentLight.Mode.SingleLeft:
								if (hasOutgoingLeftSegment) {
									// left arrow light
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawLeftLightTexture(liveSegmentLight.LightLeft, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeLeftLight();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, 1);

										float numOffset;

										if (liveSegmentLight.LightLeft == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}
								}

								// forward-right arrow light
								guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 && _hoveredNode == nodeId ? 0.92f : 0.6f;

								GUI.color = guiColor;

								var myRect5 =
									new Rect(offsetScreenPos.x - lightWidth / 2 - pedestrianWidth - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) + 5f * zoom,
										offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

								if (hasOutgoingForwardSegment && hasOutgoingRightSegment) {
									drawForwardRightLightTexture(liveSegmentLight.LightMain, myRect5);
									hasOtherLight = true;
								} else if (hasOutgoingForwardSegment) {
									drawStraightLightTexture(liveSegmentLight.LightMain, myRect5);
									hasOtherLight = true;
								} else if (hasOutgoingRightSegment) {
									drawRightLightTexture(liveSegmentLight.LightMain, myRect5);
									hasOtherLight = true;
								}

								if (hasOtherLight && myRect5.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
									_hoveredButton[0] = srcSegmentId;
									_hoveredButton[1] = 4;
									_hoveredNode = nodeId;
									hoveredSegment = true;

									if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
										liveSegmentLight.ChangeMainLight();
									}
								}

								// COUNTER
								if (timedActive && _timedShowNumbers) {
									var counterSize = 20f * zoom;

									var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, 0);

									float numOffset;

									if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
										numOffset = counterSize + 96f * zoom - modeHeight * 2;
									} else {
										numOffset = counterSize + 40f * zoom - modeHeight * 2;
									}

									var myRectCounterNum =
										new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
											offsetScreenPos.y - numOffset, counterSize, counterSize);

									_counterStyle.fontSize = (int)(18f * zoom);
									_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

									GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

									if (myRectCounterNum.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 4;
										_hoveredNode = nodeId;
										hoveredSegment = true;
									}
								}
								break;
							case CustomSegmentLight.Mode.SingleRight: {
									// forward-left light
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var myRect4 = new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth) - pedestrianWidth + 5f * zoom,
										offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									var lightType = 0;

									hasOtherLight = false;
									if (hasOutgoingForwardSegment && hasOutgoingLeftSegment) {
										hasOtherLight = true;
										drawForwardLeftLightTexture(liveSegmentLight.LightMain, myRect4);

										lightType = 1;
									} else if (hasOutgoingForwardSegment) {
										hasOtherLight = true;
										if (!hasOutgoingRightSegment) {
											myRect4 = new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);
										}

										drawStraightLightTexture(liveSegmentLight.LightMain, myRect4);
									} else if (hasOutgoingLeftSegment) {
										hasOtherLight = true;
										if (!hasOutgoingRightSegment) {
											myRect4 = new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);
										}

										drawLeftLightTexture(liveSegmentLight.LightMain, myRect4);
									}


									if (hasOtherLight && myRect4.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeMainLight();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, lightType);

										float numOffset;

										if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? (hasOutgoingRightSegment ? lightWidth * 2 : lightWidth) : (hasOutgoingRightSegment ? lightWidth : 0f)),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}

									// right arrow light
									if (hasOutgoingRightSegment) {
										guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 &&
													 _hoveredNode == nodeId
											? 0.92f
											: 0.6f;

										GUI.color = guiColor;

										var rect5 =
											new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

										drawRightLightTexture(liveSegmentLight.LightRight, rect5);

										if (rect5.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
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
											var counterSize = 20f * zoom;

											var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, 2);

											float numOffset;

											if (liveSegmentLight.LightRight == RoadBaseAI.TrafficLightState.Red) {
												numOffset = counterSize + 96f * zoom - modeHeight * 2;
											} else {
												numOffset = counterSize + 40f * zoom - modeHeight * 2;
											}

											var myRectCounterNum =
												new Rect(
													offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
													pedestrianWidth + 5f * zoom -
													(_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
													offsetScreenPos.y - numOffset, counterSize, counterSize);

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
									}
								}
								break;
							default:
								// left arrow light
								if (hasOutgoingLeftSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var offsetLight = lightWidth;

									if (hasOutgoingRightSegment)
										offsetLight += lightWidth;

									if (hasOutgoingForwardSegment)
										offsetLight += lightWidth;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawLeftLightTexture(liveSegmentLight.LightLeft, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeLeftLight();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, 1);

										float numOffset;

										if (liveSegmentLight.LightLeft == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(
												offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
												pedestrianWidth + 5f * zoom -
												(_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

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
								}

								// forward arrow light
								if (hasOutgoingForwardSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var offsetLight = lightWidth;

									if (hasOutgoingRightSegment)
										offsetLight += lightWidth;

									var myRect6 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawStraightLightTexture(liveSegmentLight.LightMain, myRect6);

									if (myRect6.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 4;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeMainLight();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, 0);

										float numOffset;

										if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(
												offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
												pedestrianWidth + 5f * zoom -
												(_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

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
								}

								// right arrow light
								if (hasOutgoingRightSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 5 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var rect6 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawRightLightTexture(liveSegmentLight.LightRight, rect6);

									if (rect6.Contains(Event.current.mousePosition) && !IsCursorInPanel()) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 5;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeRightLight();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, end.StartNode, vehicleType, 2);

										float numOffset;

										if (liveSegmentLight.LightRight == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(
												offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) -
												pedestrianWidth + 5f * zoom -
												(_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) &&
											!IsCursorInPanel()) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 5;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}
								}
								break;
						} // end switch liveSegmentLight.CurrentMode
					} // end foreach light
				} // end foreach segment
			} // end foreach node

			if (!hoveredSegment) {
				_hoveredButton[0] = 0;
				_hoveredButton[1] = 0;
			}
		}
	}
}
