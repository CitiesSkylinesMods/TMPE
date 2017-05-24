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
	public class ManualTrafficLightsTool : SubTool {
		private readonly int[] _hoveredButton = new int[2];
		private readonly GUIStyle _counterStyle = new GUIStyle();

		public ManualTrafficLightsTool(TrafficManagerTool mainTool) : base(mainTool) {
			
		}

		public override void OnSecondaryClickOverlay() {
			if (IsCursorInPanel())
				return;
			Cleanup();
			SelectedNodeId = 0;
		}

		public override void OnPrimaryClickOverlay() {
			if (IsCursorInPanel())
				return;
			if (SelectedNodeId != 0) return;

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			TrafficLightSimulation sim = tlsMan.GetNodeSimulation(HoveredNodeId);
			if (sim == null || !sim.IsTimedLight()) {
				if ((Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
					prioMan.RemovePrioritySignsFromNode(HoveredNodeId);
					TrafficLightManager.Instance.AddTrafficLight(HoveredNodeId, ref Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId]);
				}

				SelectedNodeId = HoveredNodeId;

				sim = tlsMan.AddNodeToSimulation(SelectedNodeId);
				sim.SetupManualTrafficLight();

				/*for (var s = 0; s < 8; s++) {
					var segment = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].GetSegment(s);
					if (segment != 0 && !TrafficPriority.IsPrioritySegment(SelectedNodeId, segment)) {
						TrafficPriority.AddPrioritySegment(SelectedNodeId, segment, SegmentEnd.PriorityType.None);
					}
				}*/
			} else {
				MainTool.ShowTooltip(Translation.GetString("NODE_IS_TIMED_LIGHT"));
			}
		}

		public override void OnToolGUI(Event e) {
			var hoveredSegment = false;

			if (SelectedNodeId != 0) {
				CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
				TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

				var nodeSimulation = tlsMan.GetNodeSimulation(SelectedNodeId);
				if (nodeSimulation == null || !nodeSimulation.IsManualLight())
					return;
				nodeSimulation.housekeeping();

				/*if (Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].CountSegments() == 2) {
					_guiManualTrafficLightsCrosswalk(ref Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode]);
					return;
				}*/ // TODO check

				NodeGeometry nodeGeometry = NodeGeometry.Get(SelectedNodeId);
				foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
					if (end == null)
						continue;

					var position = CalculateNodePositionForSegment(Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId], ref Singleton<NetManager>.instance.m_segments.m_buffer[end.SegmentId]);
					var segmentLights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode, false);
					if (segmentLights == null)
						continue;

					var screenPos = Camera.main.WorldToScreenPoint(position);
					screenPos.y = Screen.height - screenPos.y;

					if (screenPos.z < 0)
						continue;

					var diff = position - Camera.main.transform.position;
					var zoom = 1.0f / diff.magnitude * 100f;

					// original / 2.5
					var lightWidth = 41f * zoom;
					var lightHeight = 97f * zoom;

					var pedestrianWidth = 36f * zoom;
					var pedestrianHeight = 61f * zoom;

					// SWITCH MODE BUTTON
					var modeWidth = 41f * zoom;
					var modeHeight = 38f * zoom;

					var guiColor = GUI.color;

					if (segmentLights.PedestrianLightState != null) {
						// pedestrian light

						// SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
						hoveredSegment = RenderManualPedestrianLightSwitch(zoom, end.SegmentId, screenPos, lightWidth, segmentLights, hoveredSegment);

						// SWITCH PEDESTRIAN LIGHT
						guiColor.a = _hoveredButton[0] == end.SegmentId && _hoveredButton[1] == 2 && segmentLights.ManualPedestrianMode ? 0.92f : 0.6f;
						GUI.color = guiColor;

						var myRect3 = new Rect(screenPos.x - pedestrianWidth / 2 - lightWidth + 5f * zoom, screenPos.y - pedestrianHeight / 2 + 22f * zoom, pedestrianWidth, pedestrianHeight);

						switch (segmentLights.PedestrianLightState) {
							case RoadBaseAI.TrafficLightState.Green:
								GUI.DrawTexture(myRect3, TextureResources.PedestrianGreenLightTexture2D);
								break;
							case RoadBaseAI.TrafficLightState.Red:
							default:
								GUI.DrawTexture(myRect3, TextureResources.PedestrianRedLightTexture2D);
								break;
						}

						hoveredSegment = IsPedestrianLightHovered(myRect3, end.SegmentId, hoveredSegment, segmentLights);
					}

					int lightOffset = -1;
					foreach (ExtVehicleType vehicleType in segmentLights.VehicleTypes) {
						++lightOffset;
						CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);

						Vector3 offsetScreenPos = screenPos;
						offsetScreenPos.y -= (lightHeight + 10f * zoom) * lightOffset;

						SetAlpha(end.SegmentId, -1);

						var myRect1 = new Rect(offsetScreenPos.x - modeWidth / 2, offsetScreenPos.y - modeHeight / 2 + modeHeight - 7f * zoom, modeWidth, modeHeight);

						GUI.DrawTexture(myRect1, TextureResources.LightModeTexture2D);

						hoveredSegment = GetHoveredSegment(myRect1, end.SegmentId, hoveredSegment, segmentLight);

						// COUNTER
						hoveredSegment = RenderCounter(end.SegmentId, offsetScreenPos, modeWidth, modeHeight, zoom, segmentLights, hoveredSegment);

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
								GUI.DrawTexture(infoRect, TextureResources.VehicleInfoSignTextures[TrafficManagerTool.InfoSignsToDisplay[k]]);
								++numInfos;
							}
						}

						if (end.OutgoingOneWay) continue;

						var hasLeftSegment = end.NumLeftSegments > 0;
						var hasForwardSegment = end.NumStraightSegments > 0;
						var hasRightSegment = end.NumRightSegments > 0;

						switch (segmentLight.CurrentMode) {
							case CustomSegmentLight.Mode.Simple:
								hoveredSegment = SimpleManualSegmentLightMode(end.SegmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);
								break;
							case CustomSegmentLight.Mode.SingleLeft:
								hoveredSegment = LeftForwardRManualSegmentLightMode(hasLeftSegment, end.SegmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment, hasForwardSegment, hasRightSegment);
								break;
							case CustomSegmentLight.Mode.SingleRight:
								hoveredSegment = RightForwardLSegmentLightMode(end.SegmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, hasForwardSegment, hasLeftSegment, segmentLight, hasRightSegment, hoveredSegment);
								break;
							default:
								// left arrow light
								if (hasLeftSegment)
									hoveredSegment = LeftArrowLightMode(end.SegmentId, lightWidth, hasRightSegment, hasForwardSegment, offsetScreenPos, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);

								// forward arrow light
								if (hasForwardSegment)
									hoveredSegment = ForwardArrowLightMode(end.SegmentId, lightWidth, hasRightSegment, offsetScreenPos, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);

								// right arrow light
								if (hasRightSegment)
									hoveredSegment = RightArrowLightMode(end.SegmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);
								break;
						}
					}
				}
			}

			if (hoveredSegment) return;
			_hoveredButton[0] = 0;
			_hoveredButton[1] = 0;
		}

		public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
			if (SelectedNodeId != 0) {
				RenderManualNodeOverlays(cameraInfo);
			} else {
				RenderManualSelectionOverlay(cameraInfo);
			}
		}

		private bool RenderManualPedestrianLightSwitch(float zoom, int segmentId, Vector3 screenPos, float lightWidth,
			CustomSegmentLights segmentLights, bool hoveredSegment) {
			if (segmentLights.PedestrianLightState == null)
				return false;

			var guiColor = GUI.color;
			var manualPedestrianWidth = 36f * zoom;
			var manualPedestrianHeight = 35f * zoom;

			guiColor.a = _hoveredButton[0] == segmentId && (_hoveredButton[1] == 1 || _hoveredButton[1] == 2) ? 0.92f : 0.6f;

			GUI.color = guiColor;

			var myRect2 = new Rect(screenPos.x - manualPedestrianWidth / 2 - lightWidth + 5f * zoom,
				screenPos.y - manualPedestrianHeight / 2 - 9f * zoom, manualPedestrianWidth, manualPedestrianHeight);

			GUI.DrawTexture(myRect2, segmentLights.ManualPedestrianMode ? TextureResources.PedestrianModeManualTexture2D : TextureResources.PedestrianModeAutomaticTexture2D);

			if (!myRect2.Contains(Event.current.mousePosition))
				return hoveredSegment;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 1;

			if (!MainTool.CheckClicked())
				return true;

			segmentLights.ManualPedestrianMode = !segmentLights.ManualPedestrianMode;
			return true;
		}

		private bool IsPedestrianLightHovered(Rect myRect3, int segmentId, bool hoveredSegment, CustomSegmentLights segmentLights) {
			if (!myRect3.Contains(Event.current.mousePosition))
				return hoveredSegment;
			if (segmentLights.PedestrianLightState == null)
				return false;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 2;

			if (!MainTool.CheckClicked())
				return true;

			if (!segmentLights.ManualPedestrianMode) {
				segmentLights.ManualPedestrianMode = true;
			} else {
				segmentLights.ChangeLightPedestrian();
			}
			return true;
		}

		private bool GetHoveredSegment(Rect myRect1, int segmentId, bool hoveredSegment, CustomSegmentLight segmentDict) {
			if (!myRect1.Contains(Event.current.mousePosition))
				return hoveredSegment;

			//Log.Message("mouse in myRect1");
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = -1;

			if (!MainTool.CheckClicked())
				return true;
			segmentDict.ToggleMode();
			return true;
		}

		private bool RenderCounter(int segmentId, Vector3 screenPos, float modeWidth, float modeHeight, float zoom,
			CustomSegmentLights segmentLights, bool hoveredSegment) {
			SetAlpha(segmentId, 0);

			var myRectCounter = new Rect(screenPos.x - modeWidth / 2, screenPos.y - modeHeight / 2 - 6f * zoom, modeWidth, modeHeight);

			GUI.DrawTexture(myRectCounter, TextureResources.LightCounterTexture2D);

			var counterSize = 20f * zoom;

			var counter = segmentLights.LastChange();

			var myRectCounterNum = new Rect(screenPos.x - counterSize + 15f * zoom + (counter >= 10 ? -5 * zoom : 0f),
				screenPos.y - counterSize + 11f * zoom, counterSize, counterSize);

			_counterStyle.fontSize = (int)(18f * zoom);
			_counterStyle.normal.textColor = new Color(1f, 1f, 1f);

			GUI.Label(myRectCounterNum, counter.ToString(), _counterStyle);

			if (!myRectCounter.Contains(Event.current.mousePosition))
				return hoveredSegment;
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 0;
			return true;
		}

		private bool SimpleManualSegmentLightMode(int segmentId, Vector3 screenPos, float lightWidth, float pedestrianWidth,
			float zoom, float lightHeight, CustomSegmentLight segmentDict, bool hoveredSegment) {
			SetAlpha(segmentId, 3);

			var myRect4 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightMain) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect4, TextureResources.GreenLightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect4, TextureResources.RedLightTexture2D);
					break;
			}

			if (!myRect4.Contains(Event.current.mousePosition))
				return hoveredSegment;
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 3;

			if (!MainTool.CheckClicked())
				return true;
			segmentDict.ChangeMainLight();
			return true;
		}

		private bool LeftForwardRManualSegmentLightMode(bool hasLeftSegment, int segmentId, Vector3 screenPos, float lightWidth,
			float pedestrianWidth, float zoom, float lightHeight, CustomSegmentLight segmentDict, bool hoveredSegment,
			bool hasForwardSegment, bool hasRightSegment) {
			if (hasLeftSegment) {
				// left arrow light
				SetAlpha(segmentId, 3);

				var myRect4 =
					new Rect(screenPos.x - lightWidth / 2 - lightWidth * 2 - pedestrianWidth + 5f * zoom,
						screenPos.y - lightHeight / 2, lightWidth, lightHeight);

				switch (segmentDict.LightLeft) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect4, TextureResources.GreenLightLeftTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect4, TextureResources.RedLightLeftTexture2D);
						break;
				}

				if (myRect4.Contains(Event.current.mousePosition)) {
					_hoveredButton[0] = segmentId;
					_hoveredButton[1] = 3;
					hoveredSegment = true;

					if (MainTool.CheckClicked()) {
						segmentDict.ChangeLeftLight();
					}
				}
			}

			// forward-right arrow light
			SetAlpha(segmentId, 4);

			var myRect5 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			if (hasForwardSegment && hasRightSegment) {
				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect5, TextureResources.GreenLightForwardRightTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect5, TextureResources.RedLightForwardRightTexture2D);
						break;
				}
			} else if (!hasRightSegment) {
				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect5, TextureResources.GreenLightStraightTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect5, TextureResources.RedLightStraightTexture2D);
						break;
				}
			} else {
				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect5, TextureResources.GreenLightRightTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect5, TextureResources.RedLightRightTexture2D);
						break;
				}
			}

			if (!myRect5.Contains(Event.current.mousePosition))
				return hoveredSegment;
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 4;

			if (!MainTool.CheckClicked())
				return true;
			segmentDict.ChangeMainLight();
			return true;
		}

		private bool RightForwardLSegmentLightMode(int segmentId, Vector3 screenPos, float lightWidth, float pedestrianWidth,
			float zoom, float lightHeight, bool hasForwardSegment, bool hasLeftSegment, CustomSegmentLight segmentDict,
			bool hasRightSegment, bool hoveredSegment) {
			SetAlpha(segmentId, 3);

			var myRect4 = new Rect(screenPos.x - lightWidth / 2 - lightWidth * 2 - pedestrianWidth + 5f * zoom,
				screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			if (hasForwardSegment && hasLeftSegment) {
				switch (segmentDict.LightLeft) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect4, TextureResources.GreenLightForwardLeftTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect4, TextureResources.RedLightForwardLeftTexture2D);
						break;
				}
			} else if (!hasLeftSegment) {
				if (!hasRightSegment) {
					myRect4 = new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
						screenPos.y - lightHeight / 2, lightWidth, lightHeight);
				}

				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect4, TextureResources.GreenLightStraightTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect4, TextureResources.RedLightStraightTexture2D);
						break;
				}
			} else {
				if (!hasRightSegment) {
					myRect4 = new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
						screenPos.y - lightHeight / 2, lightWidth, lightHeight);
				}

				switch (segmentDict.LightMain) {
					case RoadBaseAI.TrafficLightState.Green:
						GUI.DrawTexture(myRect4, TextureResources.GreenLightLeftTexture2D);
						break;
					case RoadBaseAI.TrafficLightState.Red:
						GUI.DrawTexture(myRect4, TextureResources.RedLightLeftTexture2D);
						break;
				}
			}


			if (myRect4.Contains(Event.current.mousePosition)) {
				_hoveredButton[0] = segmentId;
				_hoveredButton[1] = 3;
				hoveredSegment = true;

				if (MainTool.CheckClicked()) {
					segmentDict.ChangeMainLight();
				}
			}

			var guiColor = GUI.color;
			// right arrow light
			if (hasRightSegment)
				guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == 4 ? 0.92f : 0.6f;

			GUI.color = guiColor;

			var myRect5 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightRight) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect5, TextureResources.GreenLightRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect5, TextureResources.RedLightRightTexture2D);
					break;
			}


			if (!myRect5.Contains(Event.current.mousePosition))
				return hoveredSegment;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 4;

			if (!MainTool.CheckClicked())
				return true;
			segmentDict.ChangeRightLight();
			return true;
		}

		private bool LeftArrowLightMode(int segmentId, float lightWidth, bool hasRightSegment,
			bool hasForwardSegment, Vector3 screenPos, float pedestrianWidth, float zoom, float lightHeight,
			CustomSegmentLight segmentDict, bool hoveredSegment) {
			SetAlpha(segmentId, 3);

			var offsetLight = lightWidth;

			if (hasRightSegment)
				offsetLight += lightWidth;

			if (hasForwardSegment)
				offsetLight += lightWidth;

			var myRect4 =
				new Rect(screenPos.x - lightWidth / 2 - offsetLight - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightLeft) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect4, TextureResources.GreenLightLeftTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect4, TextureResources.RedLightLeftTexture2D);
					break;
			}

			if (!myRect4.Contains(Event.current.mousePosition))
				return hoveredSegment;
			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 3;

			if (!MainTool.CheckClicked())
				return true;
			segmentDict.ChangeLeftLight();

			if (!hasForwardSegment) {
				segmentDict.ChangeMainLight();
			}
			return true;
		}

		private bool ForwardArrowLightMode(int segmentId, float lightWidth, bool hasRightSegment,
			Vector3 screenPos, float pedestrianWidth, float zoom, float lightHeight, CustomSegmentLight segmentDict,
			bool hoveredSegment) {
			SetAlpha(segmentId, 4);

			var offsetLight = lightWidth;

			if (hasRightSegment)
				offsetLight += lightWidth;

			var myRect6 =
				new Rect(screenPos.x - lightWidth / 2 - offsetLight - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightMain) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect6, TextureResources.GreenLightStraightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect6, TextureResources.RedLightStraightTexture2D);
					break;
			}

			if (!myRect6.Contains(Event.current.mousePosition))
				return hoveredSegment;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 4;

			if (!MainTool.CheckClicked())
				return true;
			segmentDict.ChangeMainLight();
			return true;
		}

		private bool RightArrowLightMode(int segmentId, Vector3 screenPos, float lightWidth,
			float pedestrianWidth, float zoom, float lightHeight, CustomSegmentLight segmentDict, bool hoveredSegment) {
			SetAlpha(segmentId, 5);

			var myRect5 =
				new Rect(screenPos.x - lightWidth / 2 - lightWidth - pedestrianWidth + 5f * zoom,
					screenPos.y - lightHeight / 2, lightWidth, lightHeight);

			switch (segmentDict.LightRight) {
				case RoadBaseAI.TrafficLightState.Green:
					GUI.DrawTexture(myRect5, TextureResources.GreenLightRightTexture2D);
					break;
				case RoadBaseAI.TrafficLightState.Red:
					GUI.DrawTexture(myRect5, TextureResources.RedLightRightTexture2D);
					break;
			}

			if (!myRect5.Contains(Event.current.mousePosition))
				return hoveredSegment;

			_hoveredButton[0] = segmentId;
			_hoveredButton[1] = 5;

			if (!MainTool.CheckClicked())
				return true;
			segmentDict.ChangeRightLight();
			return true;
		}

		private Vector3 CalculateNodePositionForSegment(NetNode node, int segmentId) {
			var position = node.m_position;

			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
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
			var position = node.m_position;

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
			var guiColor = GUI.color;

			guiColor.a = _hoveredButton[0] == segmentId && _hoveredButton[1] == buttonId ? 0.92f : 0.6f;

			GUI.color = guiColor;
		}

		private void RenderManualSelectionOverlay(RenderManager.CameraInfo cameraInfo) {
			if (HoveredNodeId == 0) return;

			MainTool.DrawNodeCircle(cameraInfo, HoveredNodeId, false, false);
			/*var segment = Singleton<NetManager>.instance.m_segments.m_buffer[Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_segment0];

			//if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) return;
			Bezier3 bezier;
			bezier.a = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;
			bezier.d = Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_position;

			var color = MainTool.GetToolColor(false, false);

			NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection, bezier.d,
				segment.m_endDirection, false, false, out bezier.b, out bezier.c);
			MainTool.DrawOverlayBezier(cameraInfo, bezier, color);*/
		}

		private void RenderManualNodeOverlays(RenderManager.CameraInfo cameraInfo) {
			var nodeSimulation = TrafficLightSimulationManager.Instance.GetNodeSimulation(SelectedNodeId);
			if (nodeSimulation == null || !nodeSimulation.IsManualLight())
				return;

			MainTool.DrawNodeCircle(cameraInfo, SelectedNodeId, true, false);

			/*CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

			NodeGeometry nodeGeometry = NodeGeometry.Get(SelectedNodeId);
			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null)
					continue;

				var colorGray = new Color(0.25f, 0.25f, 0.25f, 0.25f);

				var position = CalculateNodePositionForSegment(Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId], end.SegmentId);

				var width = _hoveredButton[0] == end.SegmentId ? 11.25f : 10f;
				MainTool.DrawOverlayCircle(cameraInfo, colorGray, position, width, end.SegmentId != _hoveredButton[0]);
			}*/
		}

		public override void Cleanup() {
			if (SelectedNodeId == 0) return;
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			var nodeSimulation = tlsMan.GetNodeSimulation(SelectedNodeId);

			if (nodeSimulation == null || !nodeSimulation.IsManualLight()) return;

			tlsMan.RemoveNodeFromSimulation(SelectedNodeId, true, false);
		}
	}
}
