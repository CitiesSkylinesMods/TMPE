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
using TrafficManager.Manager.Impl;
using TrafficManager.Geometry.Impl;
using ColossalFramework.UI;
using TrafficManager.Traffic.Enums;
using TrafficManager.Traffic.Data;

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

			if (!tlsMan.TrafficLightSimulations[HoveredNodeId].IsTimedLight()) {
				if ((Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
					prioMan.RemovePrioritySignsFromNode(HoveredNodeId);
					TrafficLightManager.Instance.AddTrafficLight(HoveredNodeId, ref Singleton<NetManager>.instance.m_nodes.m_buffer[HoveredNodeId]);
				}

				if (tlsMan.SetUpManualTrafficLight(HoveredNodeId)) {
					SelectedNodeId = HoveredNodeId;
				}

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

				/*if (Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode].CountSegments() == 2) {
					_guiManualTrafficLightsCrosswalk(ref Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNode]);
					return;
				}*/ // TODO check

				for (int i = 0; i < 8; ++i) {
					ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId].GetSegment(i);
					if (segmentId == 0) {
						continue;
					}
					bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, SelectedNodeId);

					var position = CalculateNodePositionForSegment(Singleton<NetManager>.instance.m_nodes.m_buffer[SelectedNodeId], ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId]);
					var segmentLights = customTrafficLightsManager.GetSegmentLights(segmentId, startNode, false);
					if (segmentLights == null)
						continue;

					bool showPedLight = segmentLights.PedestrianLightState != null && junctionRestrictionsManager.IsPedestrianCrossingAllowed(segmentLights.SegmentId, segmentLights.StartNode);

					Vector3 screenPos;
					bool visible = MainTool.WorldToScreenPoint(position, out screenPos);

					if (!visible)
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

					if (showPedLight) {
						// pedestrian light

						// SWITCH MANUAL PEDESTRIAN LIGHT BUTTON
						hoveredSegment = RenderManualPedestrianLightSwitch(zoom, segmentId, screenPos, lightWidth, segmentLights, hoveredSegment);

						// SWITCH PEDESTRIAN LIGHT
						guiColor.a = MainTool.GetHandleAlpha(_hoveredButton[0] == segmentId && _hoveredButton[1] == 2 && segmentLights.ManualPedestrianMode);
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

						hoveredSegment = IsPedestrianLightHovered(myRect3, segmentId, hoveredSegment, segmentLights);
					}

					int lightOffset = -1;
					foreach (ExtVehicleType vehicleType in segmentLights.VehicleTypes) {
						++lightOffset;
						ICustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);

						Vector3 offsetScreenPos = screenPos;
						offsetScreenPos.y -= (lightHeight + 10f * zoom) * lightOffset;

						SetAlpha(segmentId, -1);

						var myRect1 = new Rect(offsetScreenPos.x - modeWidth / 2, offsetScreenPos.y - modeHeight / 2 + modeHeight - 7f * zoom, modeWidth, modeHeight);

						GUI.DrawTexture(myRect1, TextureResources.LightModeTexture2D);

						hoveredSegment = GetHoveredSegment(myRect1, segmentId, hoveredSegment, segmentLight);

						// COUNTER
						hoveredSegment = RenderCounter(segmentId, offsetScreenPos, modeWidth, modeHeight, zoom, segmentLights, hoveredSegment);

						if (vehicleType != ExtVehicleType.None) {
							// Info sign
							var infoWidth = 56.125f * zoom;
							var infoHeight = 51.375f * zoom;

							int numInfos = 0;
							for (int k = 0; k < TrafficManagerTool.InfoSignsToDisplay.Length; ++k) {
								if ((TrafficManagerTool.InfoSignsToDisplay[k] & vehicleType) == ExtVehicleType.None)
									continue;
								var infoRect = new Rect(offsetScreenPos.x + modeWidth / 2f + 7f * zoom * (float)(numInfos + 1) + infoWidth * (float)numInfos, offsetScreenPos.y - infoHeight / 2f, infoWidth, infoHeight);
								guiColor.a = MainTool.GetHandleAlpha(false);
								GUI.DrawTexture(infoRect, TextureResources.VehicleInfoSignTextures[TrafficManagerTool.InfoSignsToDisplay[k]]);
								++numInfos;
							}
						}

						ExtSegment seg = segMan.ExtSegments[segmentId];
						ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];
						if (seg.oneWay && segEnd.outgoing) continue;

						bool hasLeftSegment;
						bool hasForwardSegment;
						bool hasRightSegment;
						segEndMan.CalculateOutgoingLeftStraightRightSegments(ref segEnd, ref Singleton<NetManager>.instance.m_nodes.m_buffer[segmentId], out hasLeftSegment, out hasForwardSegment, out hasRightSegment);

						switch (segmentLight.CurrentMode) {
							case LightMode.Simple:
								hoveredSegment = SimpleManualSegmentLightMode(segmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);
								break;
							case LightMode.SingleLeft:
								hoveredSegment = LeftForwardRManualSegmentLightMode(hasLeftSegment, segmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment, hasForwardSegment, hasRightSegment);
								break;
							case LightMode.SingleRight:
								hoveredSegment = RightForwardLSegmentLightMode(segmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, hasForwardSegment, hasLeftSegment, segmentLight, hasRightSegment, hoveredSegment);
								break;
							default:
								// left arrow light
								if (hasLeftSegment)
									hoveredSegment = LeftArrowLightMode(segmentId, lightWidth, hasRightSegment, hasForwardSegment, offsetScreenPos, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);

								// forward arrow light
								if (hasForwardSegment)
									hoveredSegment = ForwardArrowLightMode(segmentId, lightWidth, hasRightSegment, offsetScreenPos, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);

								// right arrow light
								if (hasRightSegment)
									hoveredSegment = RightArrowLightMode(segmentId, offsetScreenPos, lightWidth, pedestrianWidth, zoom, lightHeight, segmentLight, hoveredSegment);
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
			ICustomSegmentLights segmentLights, bool hoveredSegment) {
			if (segmentLights.PedestrianLightState == null)
				return false;

			var guiColor = GUI.color;
			var manualPedestrianWidth = 36f * zoom;
			var manualPedestrianHeight = 35f * zoom;

			guiColor.a = MainTool.GetHandleAlpha(_hoveredButton[0] == segmentId && (_hoveredButton[1] == 1 || _hoveredButton[1] == 2));

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

		private bool IsPedestrianLightHovered(Rect myRect3, int segmentId, bool hoveredSegment, ICustomSegmentLights segmentLights) {
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

		private bool GetHoveredSegment(Rect myRect1, int segmentId, bool hoveredSegment, ICustomSegmentLight segmentDict) {
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
			ICustomSegmentLights segmentLights, bool hoveredSegment) {
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
			float zoom, float lightHeight, ICustomSegmentLight segmentDict, bool hoveredSegment) {
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
			float pedestrianWidth, float zoom, float lightHeight, ICustomSegmentLight segmentDict, bool hoveredSegment,
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
			float zoom, float lightHeight, bool hasForwardSegment, bool hasLeftSegment, ICustomSegmentLight segmentDict,
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
				guiColor.a = MainTool.GetHandleAlpha(_hoveredButton[0] == segmentId && _hoveredButton[1] == 4);

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
			ICustomSegmentLight segmentDict, bool hoveredSegment) {
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
			Vector3 screenPos, float pedestrianWidth, float zoom, float lightHeight, ICustomSegmentLight segmentDict,
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
			float pedestrianWidth, float zoom, float lightHeight, ICustomSegmentLight segmentDict, bool hoveredSegment) {
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

			guiColor.a = MainTool.GetHandleAlpha(_hoveredButton[0] == segmentId && _hoveredButton[1] == buttonId);

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
