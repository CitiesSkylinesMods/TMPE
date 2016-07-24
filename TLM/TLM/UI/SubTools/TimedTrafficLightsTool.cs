using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

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

		public TimedTrafficLightsTool(TrafficManagerTool mainTool) : base(mainTool) {
			
		}

		public override bool IsCursorInPanel() {
			return _cursorInSecondaryPanel;
		}

		public override void OnActivate() {
			nodeSelectionLocked = false;
			foreach (var selectedNodeIndex in SelectedNodeIndexes) {
				TrafficLightSimulation lightSim = TrafficLightSimulation.GetNodeSimulation(selectedNodeIndex);
				if (lightSim != null) {
					lightSim.housekeeping();
				}
				//TrafficPriority.nodeHousekeeping(selectedNodeIndex);
			}
		}

		public override void OnPrimaryClickOverlay() {
			if (HoveredNodeId <= 0 || nodeSelectionLocked)
				return;

			switch (TrafficManagerTool.GetToolMode()) {
				case ToolMode.TimedLightsSelectNode:
				case ToolMode.TimedLightsShowLights:
					if (TrafficManagerTool.GetToolMode() == ToolMode.TimedLightsShowLights) {
						TrafficManagerTool.SetToolMode(ToolMode.TimedLightsSelectNode);
						ClearSelectedNodes();
					}

					TrafficLightSimulation timedSim = TrafficLightSimulation.GetNodeSimulation(HoveredNodeId);
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
						var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(nodeId);
						if (nodeSimulation == null || !nodeSimulation.IsTimedLight())
							continue;
						TimedTrafficLights timedNode = nodeSimulation.TimedLight;
						if (timedNode == null)
							continue;

						//mayEnterBlocked = timedNode.vehiclesMayEnterBlockedJunctions;
						existingTimedLight = timedNode;
					}

					var timedSim2 = TrafficLightSimulation.GetNodeSimulation(HoveredNodeId);
					/*if (timedSim2 != null)
						timedSim2.housekeeping();*/
					TimedTrafficLights timedLight2 = null;
					if (timedSim2 == null || !timedSim2.IsTimedLight()) {
						var nodeGroup = new List<ushort>();
						nodeGroup.Add(HoveredNodeId);
						timedSim2 = TrafficLightSimulation.AddNodeToSimulation(HoveredNodeId);
						timedSim2.SetupTimedTrafficLight(nodeGroup);
						timedLight2 = timedSim2.TimedLight;
						//timedLight.vehiclesMayEnterBlockedJunctions = mayEnterBlocked;
					} else {
						timedLight2 = timedSim2.TimedLight;
					}

					timedLight2.Join(existingTimedLight);
					ClearSelectedNodes();
					foreach (ushort nodeId in timedLight2.NodeGroup)
						AddSelectedNode(nodeId);
					TrafficManagerTool.SetToolMode(ToolMode.TimedLightsShowLights);
					break;
				case ToolMode.TimedLightsRemoveNode:
					if (SelectedNodeIndexes.Count <= 0) {
						TrafficManagerTool.SetToolMode(ToolMode.TimedLightsSelectNode);
						return;
					}

					if (SelectedNodeIndexes.Contains(HoveredNodeId)) {
						TrafficLightSimulation.RemoveNodeFromSimulation(HoveredNodeId, false, false);
					}
					RemoveSelectedNode(HoveredNodeId);
					TrafficManagerTool.SetToolMode(ToolMode.TimedLightsShowLights);
					break;
			}
		}

		public override void OnToolGUI(Event e) {
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
				var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_segment0];

				//if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
				Bezier3 bezier;
				bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;
				bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;

				var color = MainTool.GetToolColor(false, false);

				NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
					segment.m_endDirection, false, false, out bezier.b, out bezier.c);
				MainTool.DrawOverlayBezier(cameraInfo, bezier, color);
				//}
			}

			if (SelectedNodeIndexes.Count <= 0) return;

			foreach (var index in SelectedNodeIndexes) {
				var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_segment0];

				Bezier3 bezier;

				bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_position;
				bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[index].m_position;

				var color = MainTool.GetToolColor(true, false);

				NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
					segment.m_endDirection, false, false, out bezier.b, out bezier.c);
				MainTool.DrawOverlayBezier(cameraInfo, bezier, color);
			}
		}

		private void _guiTimedControlPanel(int num) {
			//Log._Debug("guiTimedControlPanel");
			var layout = new GUIStyle { normal = { textColor = new Color(1f, 1f, 1f) } };
			var layoutRed = new GUIStyle { normal = { textColor = new Color(1f, 0f, 0f) } };
			var layoutGreen = new GUIStyle { normal = { textColor = new Color(0f, 1f, 0f) } };
			var layoutYellow = new GUIStyle { normal = { textColor = new Color(1f, 1f, 0f) } };

			if (TrafficManagerTool.GetToolMode() == ToolMode.TimedLightsAddNode || TrafficManagerTool.GetToolMode() == ToolMode.TimedLightsRemoveNode) {
				GUILayout.Label(Translation.GetString("Select_junction"));
				if (GUILayout.Button(Translation.GetString("Cancel"))) {
					TrafficManagerTool.SetToolMode(ToolMode.TimedLightsShowLights);
				} else
					return;
			}

			var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(SelectedNodeIndexes[0]);
			var timedNodeMain = nodeSimulation.TimedLight;

			if (nodeSimulation == null || timedNodeMain == null) {
				TrafficManagerTool.SetToolMode(ToolMode.TimedLightsSelectNode);
				//Log._Debug("nodesim or timednodemain is null");
				return;
			}

			for (var i = 0; i < timedNodeMain.NumSteps(); i++) {
				GUILayout.BeginHorizontal();

				if (_timedEditStep != i) {
					if (nodeSimulation.IsTimedLightActive()) {
						if (i == timedNodeMain.CurrentStep) {
							GUILayout.BeginVertical();
							GUILayout.Space(5);
							String labelStr = Translation.GetString("State") + " " + (i + 1) + ": (" + Translation.GetString("min/max") + ")" + timedNodeMain.GetStep(i).MinTimeRemaining() + "/" + timedNodeMain.GetStep(i).MaxTimeRemaining();
							float flow = Single.NaN;
							float wait = Single.NaN;
							if (timedNodeMain.IsInTestMode()) {
								try {
									timedNodeMain.GetStep(timedNodeMain.CurrentStep).calcWaitFlow(out wait, out flow);
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
							if (timedNodeMain.IsInTestMode() && !Single.IsNaN(wait) && !Single.IsNaN(flow)) {
								if (wait > 0 && flow < wait)
									labelLayout = layoutRed;
								else
									labelLayout = layoutGreen;
							} else {
								labelLayout = timedNodeMain.GetStep(i).isInEndTransition() ? layoutYellow : layoutGreen;
							}
							GUILayout.Label(labelStr, labelLayout);
							GUILayout.Space(5);
							GUILayout.EndVertical();
							if (GUILayout.Button(Translation.GetString("Skip"), GUILayout.Width(80))) {
								foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
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
									foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
										sim.TimedLight.MoveStep(i, i - 1);
										_timedViewedStep = i - 1;
									}
								}
							} else {
								GUILayout.Space(50);
							}

							if (i < timedNodeMain.NumSteps() - 1) {
								if (GUILayout.Button(Translation.GetString("down"), GUILayout.Width(48))) {
									foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
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

								foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
									sim.TimedLight.GetStep(i).SetLights(true);
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

								foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
									sim.TimedLight.GetStep(i).SetLights(true);
								}
							}

							if (GUILayout.Button(Translation.GetString("Delete"), GUILayout.Width(70))) {
								_timedPanelAdd = false;
								_timedViewedStep = -1;

								foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
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
						foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {

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
			}

			GUILayout.BeginHorizontal();

			if (_timedEditStep < 0 && !nodeSimulation.IsTimedLightActive()) {
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
						foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
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

			if (timedNodeMain.NumSteps() > 1 && _timedEditStep < 0) {
				if (nodeSimulation.IsTimedLightActive()) {
					if (GUILayout.Button(_timedShowNumbers ? Translation.GetString("Hide_counters") : Translation.GetString("Show_counters"))) {
						_timedShowNumbers = !_timedShowNumbers;
					}

					if (GUILayout.Button(Translation.GetString("Stop"))) {
						foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
							sim.TimedLight.Stop();
						}
					}

					bool isInTestMode = false;
					foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
						if (sim.TimedLight.IsInTestMode()) {
							isInTestMode = true;
							break;
						}
					}

					var curStep = timedNodeMain.CurrentStep;
					_waitFlowBalance = timedNodeMain.GetStep(curStep).waitFlowBalance;
					makeFlowPolicyDisplay(isInTestMode);
					foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
						sim.TimedLight.GetStep(curStep).waitFlowBalance = _waitFlowBalance;
					}

					//var mayEnterIfBlocked = GUILayout.Toggle(timedNodeMain.vehiclesMayEnterBlockedJunctions, Translation.GetString("Vehicles_may_enter_blocked_junctions"), new GUILayoutOption[] { });
					var testMode = GUILayout.Toggle(timedNodeMain.IsInTestMode(), Translation.GetString("Enable_test_mode_(stay_in_current_step)"), new GUILayoutOption[] { });
					foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
						sim.TimedLight.SetTestMode(testMode);
						//sim.TimedLight.vehiclesMayEnterBlockedJunctions = mayEnterIfBlocked;
					}
				} else {
					if (_timedEditStep < 0 && !_timedPanelAdd) {
						if (GUILayout.Button(Translation.GetString("Start"))) {
							_timedPanelAdd = false;
							nodeSelectionLocked = false;

							foreach (var sim in SelectedNodeIndexes.Select(TrafficLightSimulation.GetNodeSimulation)) {
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
				return;
			}

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

		public override void Cleanup() {
			ClearSelectedNodes();
			_timedShowNumbers = false;
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

			GUILayout.Window(252, _windowRect2, _guiTimedTrafficLightsNodeWindow, Translation.GetString("Select_nodes_windowTitle"));

			_cursorInSecondaryPanel = _windowRect2.Contains(Event.current.mousePosition);
		}

		private void _guiTimedTrafficLights() {
			_cursorInSecondaryPanel = false;

			GUILayout.Window(253, _windowRect, _guiTimedControlPanel, Translation.GetString("Timed_traffic_lights_manager"));

			_cursorInSecondaryPanel = _windowRect.Contains(Event.current.mousePosition);

			var hoveredSegment = false;

			foreach (var nodeId in SelectedNodeIndexes) {
				var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(nodeId);
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

				for (var i = 0; i < 8; i++) {
					ushort srcSegmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(i); // source segment

					if (srcSegmentId == 0)
						continue;

					if (nodeSimulation == null || !CustomTrafficLights.IsSegmentLight(nodeId, srcSegmentId)) {
						Log._Debug($"segment {srcSegmentId} @ {nodeId} is not a custom segment light. {nodeSimulation==null}");
						continue;
					}

					CustomSegmentLights liveSegmentLights = CustomTrafficLights.GetSegmentLights(nodeId, srcSegmentId);
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

							if (myRect2.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
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

						if (myRect3.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
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

							if (myRect1.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
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
							var prioSeg = TrafficPriority.GetPrioritySegment(nodeId, srcSegmentId);

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

								var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 3);

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

								if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
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

						var hasLeftSegment = geometry.HasLeftSegment(startNode);
						var hasForwardSegment = geometry.HasStraightSegment(startNode);
						var hasRightSegment = geometry.HasRightSegment(startNode);

						switch (liveSegmentLight.CurrentMode) {
							case CustomSegmentLight.Mode.Simple: {
									// no arrow light
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawMainLightTexture(liveSegmentLight.LightMain, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeLightMain();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 0);

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

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
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
								if (hasLeftSegment) {
									// left arrow light
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth * 2 : lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawLeftLightTexture(liveSegmentLight.LightLeft, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeLightLeft();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 1);

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

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
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

								if (hasForwardSegment && hasRightSegment) {
									drawForwardRightLightTexture(liveSegmentLight.LightMain, myRect5);
								} else if (!hasRightSegment) {
									drawStraightLightTexture(liveSegmentLight.LightMain, myRect5);
								} else {
									drawRightLightTexture(liveSegmentLight.LightMain, myRect5);
								}

								if (myRect5.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
									_hoveredButton[0] = srcSegmentId;
									_hoveredButton[1] = 4;
									_hoveredNode = nodeId;
									hoveredSegment = true;

									if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
										liveSegmentLight.ChangeLightMain();
									}
								}

								// COUNTER
								if (timedActive && _timedShowNumbers) {
									var counterSize = 20f * zoom;

									var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 0);

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

									if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
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

									if (hasForwardSegment && hasLeftSegment) {
										drawForwardLeftLightTexture(liveSegmentLight.LightMain, myRect4);

										lightType = 1;
									} else if (!hasLeftSegment) {
										if (!hasRightSegment) {
											myRect4 = new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);
										}

										drawStraightLightTexture(liveSegmentLight.LightMain, myRect4);
									} else {
										if (!hasRightSegment) {
											myRect4 = new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);
										}

										drawMainLightTexture(liveSegmentLight.LightMain, myRect4);
									}


									if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeLightMain();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, lightType);

										float numOffset;

										if (liveSegmentLight.LightMain == RoadBaseAI.TrafficLightState.Red) {
											numOffset = counterSize + 96f * zoom - modeHeight * 2;
										} else {
											numOffset = counterSize + 40f * zoom - modeHeight * 2;
										}

										var myRectCounterNum =
											new Rect(offsetScreenPos.x - counterSize + 15f * zoom + (counter >= 10 ? (counter >= 100 ? -10 * zoom : -5 * zoom) : 0f) - pedestrianWidth + 5f * zoom - (_timedPanelAdd || _timedEditStep >= 0 ? (hasRightSegment ? lightWidth * 2 : lightWidth) : (hasRightSegment ? lightWidth : 0f)),
												offsetScreenPos.y - numOffset, counterSize, counterSize);

										_counterStyle.fontSize = (int)(18f * zoom);
										_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

										GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

										if (myRectCounterNum.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}

									// right arrow light
									if (hasRightSegment) {
										guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 &&
													 _hoveredNode == nodeId
											? 0.92f
											: 0.6f;

										GUI.color = guiColor;

										var rect5 =
											new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
												offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

										drawRightLightTexture(liveSegmentLight.LightRight, rect5);

										if (rect5.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 4;
											_hoveredNode = nodeId;
											hoveredSegment = true;

											if (MainTool.CheckClicked() && !timedActive &&
												(_timedPanelAdd || _timedEditStep >= 0)) {
												liveSegmentLight.ChangeLightRight();
											}
										}

										// COUNTER
										if (timedActive && _timedShowNumbers) {
											var counterSize = 20f * zoom;

											var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 2);

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
												!_cursorInSecondaryPanel) {
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
								if (hasLeftSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 3 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var offsetLight = lightWidth;

									if (hasRightSegment)
										offsetLight += lightWidth;

									if (hasForwardSegment)
										offsetLight += lightWidth;

									var myRect4 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawLeftLightTexture(liveSegmentLight.LightLeft, myRect4);

									if (myRect4.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 3;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeLightLeft();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 1);

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
											!_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 3;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}
								}

								// forward arrow light
								if (hasForwardSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 4 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var offsetLight = lightWidth;

									if (hasRightSegment)
										offsetLight += lightWidth;

									var myRect6 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? offsetLight : offsetLight - lightWidth) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawStraightLightTexture(liveSegmentLight.LightMain, myRect6);

									if (myRect6.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 4;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeLightMain();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 0);

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
											!_cursorInSecondaryPanel) {
											_hoveredButton[0] = srcSegmentId;
											_hoveredButton[1] = 4;
											_hoveredNode = nodeId;
											hoveredSegment = true;
										}
									}
								}

								// right arrow light
								if (hasRightSegment) {
									guiColor.a = _hoveredButton[0] == srcSegmentId && _hoveredButton[1] == 5 && _hoveredNode == nodeId ? 0.92f : 0.6f;

									GUI.color = guiColor;

									var rect6 =
										new Rect(offsetScreenPos.x - lightWidth / 2 - (_timedPanelAdd || _timedEditStep >= 0 ? lightWidth : 0f) - pedestrianWidth + 5f * zoom,
											offsetScreenPos.y - lightHeight / 2, lightWidth, lightHeight);

									drawRightLightTexture(liveSegmentLight.LightRight, rect6);

									if (rect6.Contains(Event.current.mousePosition) && !_cursorInSecondaryPanel) {
										_hoveredButton[0] = srcSegmentId;
										_hoveredButton[1] = 5;
										_hoveredNode = nodeId;
										hoveredSegment = true;

										if (MainTool.CheckClicked() && !timedActive && (_timedPanelAdd || _timedEditStep >= 0)) {
											liveSegmentLight.ChangeLightRight();
										}
									}

									// COUNTER
									if (timedActive && _timedShowNumbers) {
										var counterSize = 20f * zoom;

										var counter = timedNode.CheckNextChange(srcSegmentId, vehicleType, 2);

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
											!_cursorInSecondaryPanel) {
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

		private void _guiTimedTrafficLightsNodeWindow(int num) {
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
					TrafficLightSimulation.AddNodeToSimulation(selectedNodeIndex);
					var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(selectedNodeIndex);
					nodeSimulation.SetupTimedTrafficLight(SelectedNodeIndexes);

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

			foreach (var selectedNodeIndex in SelectedNodeIndexes) {
				TrafficLightSimulation.RemoveNodeFromSimulation(selectedNodeIndex, true, false);
			}
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

		public override void ShowGUIOverlay() {
			if (!Options.timedLightsOverlay &&
				TrafficManagerTool.GetToolMode() != ToolMode.TimedLightsAddNode &&
				TrafficManagerTool.GetToolMode() != ToolMode.TimedLightsRemoveNode &&
				TrafficManagerTool.GetToolMode() != ToolMode.TimedLightsSelectNode &&
				TrafficManagerTool.GetToolMode() != ToolMode.TimedLightsShowLights)
				return;

			for (ushort nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
#if DEBUG
				bool debug = nodeId == 21361;
#endif
				if (SelectedNodeIndexes.Contains(nodeId)) {
#if DEBUG
					if (debug)
						Log._Debug($"TimedTrafficLightsTool.ShowGUIOverlay: Node {nodeId} is selected");
#endif
					continue;
				}

				TrafficLightSimulation lightSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
				if (lightSim != null && lightSim.IsTimedLight()) {
					TimedTrafficLights timedNode = lightSim.TimedLight;
					if (timedNode == null) {
#if DEBUG
						if (debug)
							Log._Debug($"TimedTrafficLightsTool.ShowGUIOverlay: Node {nodeId} does not have an instance of TimedTrafficLights. Removing node from simulation");
#endif
						TrafficLightSimulation.RemoveNodeFromSimulation(nodeId, true, false);
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
					guiColor.a = 0.25f;
					GUI.color = guiColor;
					var nodeDrawingBox = new Rect(nodeScreenPosition.x - size / 2, nodeScreenPosition.y - size / 2, size, size);
					//Log._Debug($"GUI Color: {guiColor} {GUI.color}");
					GUI.DrawTexture(nodeDrawingBox, lightSim.IsTimedLightActive() ? (timedNode.IsInTestMode() ? TrafficLightToolTextureResources.ClockTestTexture2D : TrafficLightToolTextureResources.ClockPlayTexture2D) : TrafficLightToolTextureResources.ClockPauseTexture2D, ScaleMode.ScaleToFit, true);
				}
			}
		}
	}
}
